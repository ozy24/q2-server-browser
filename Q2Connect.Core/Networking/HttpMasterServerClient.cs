using System.Linq;
using System.Net;
using System.Text;
using Q2Connect.Core.Models;
using Q2Connect.Core.Protocol;
using Q2Connect.Core.Services;

namespace Q2Connect.Core.Networking;

public class HttpMasterServerClient : IDisposable
{
    private const int MAX_HTTP_RESPONSE_SIZE = 50 * 1024 * 1024; // 50MB
    private const int MAX_SERVERS_PER_RESPONSE = 10000;

    // Use static HttpClient to prevent socket exhaustion
    // HttpClient is thread-safe and designed to be reused
    private static readonly HttpClient SharedHttpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly Settings _settings;
    private readonly ILogger? _logger;
    private bool _disposed;

    public HttpMasterServerClient(Settings settings, ILogger? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<IPEndPoint>> QueryServersAsync(CancellationToken cancellationToken = default)
    {
        var servers = new List<IPEndPoint>();

        if (string.IsNullOrEmpty(_settings.HttpMasterServerUrl))
        {
            _logger?.LogWarning("HTTP master server URL is not configured");
            return servers;
        }

        // Validate URL format and scheme
        if (!UrlValidator.IsValidHttpUrl(_settings.HttpMasterServerUrl))
        {
            _logger?.LogError($"Invalid HTTP master server URL: {_settings.HttpMasterServerUrl}");
            return servers;
        }

        try
        {
            _logger?.LogInfo($"Fetching server list from HTTP master: {_settings.HttpMasterServerUrl}");

            var response = await SharedHttpClient.GetAsync(_settings.HttpMasterServerUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            _logger?.LogDebug($"Response Content-Type: {contentType}");

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogInfo($"Received {data.Length} bytes from HTTP master server");

            // Validate that we got actual data
            if (data.Length == 0)
            {
                _logger?.LogWarning("HTTP master server returned empty response");
                return servers;
            }

            // Check response size to prevent DoS
            if (data.Length > MAX_HTTP_RESPONSE_SIZE)
            {
                _logger?.LogError($"HTTP master server response too large: {data.Length} bytes (max: {MAX_HTTP_RESPONSE_SIZE})");
                return servers;
            }

            // Check if response looks like HTML/text (common for error pages)
            // HTML typically starts with <html, <HTML, <!DOCTYPE, etc.
            var dataStart = data.Length >= 10 ? Encoding.ASCII.GetString(data, 0, Math.Min(10, data.Length)) : "";
            if (dataStart.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                dataStart.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                dataStart.StartsWith("<HTML", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogError("HTTP master server returned HTML response (likely an error page). Check the URL.");
                return servers;
            }

            // Check Content-Type if available
            if (!string.IsNullOrEmpty(contentType) && 
                !contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Contains("application/", StringComparison.OrdinalIgnoreCase) &&
                (contentType.Contains("text/", StringComparison.OrdinalIgnoreCase) ||
                 contentType.Contains("html", StringComparison.OrdinalIgnoreCase)))
            {
                _logger?.LogError($"HTTP master server returned unexpected content type: {contentType}. Expected binary data.");
                return servers;
            }

            // q2servers.com returns binary format directly (6-byte chunks: 4-byte IP + 2-byte port)
            // Check if it starts with text prefix like "+6" (q2pro format) or is pure binary
            List<IPEndPoint> parsed;
            if (data.Length > 0 && (data[0] == (byte)'+' || data[0] == (byte)'-'))
            {
                // Binary format with prefix like "+6" or "-6" (q2pro internal format)
                _logger?.LogDebug("Detected binary format with prefix");
                parsed = ParseBinaryFormat(data);
            }
            else if (data.Length > 0 && data[0] == 0xFF && data.Length >= 4 && 
                     data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF)
            {
                // OOB header present, remove it and parse as binary
                _logger?.LogDebug("Detected OOB header, removing and parsing");
                var payload = new byte[data.Length - 4];
                Array.Copy(data, 4, payload, 0, payload.Length);
                parsed = ParseBinaryFormat(payload, 6);
            }
            else
            {
                // Pure binary format (most common for HTTP master servers)
                _logger?.LogDebug("Parsing as pure binary format (6-byte chunks)");
                parsed = ParseBinaryFormat(data, 6);
            }

            // Limit number of servers to prevent DoS
            if (parsed.Count > MAX_SERVERS_PER_RESPONSE)
            {
                _logger?.LogWarning($"HTTP master server returned {parsed.Count} servers, limiting to {MAX_SERVERS_PER_RESPONSE}");
                servers.AddRange(parsed.Take(MAX_SERVERS_PER_RESPONSE));
            }
            else
            {
                servers.AddRange(parsed);
            }

            _logger?.LogInfo($"Parsed {servers.Count} server(s) from HTTP master server");
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError($"HTTP error fetching master server: {ex.Message}", ex.StackTrace);
        }
        catch (TaskCanceledException)
        {
            _logger?.LogWarning("HTTP master server request timed out");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error fetching HTTP master server: {ex.Message}", ex.StackTrace);
        }

        return servers;
    }

    private List<IPEndPoint> ParseBinaryFormat(byte[] data, int chunkSize = 6)
    {
        var servers = new List<IPEndPoint>();
        int offset = 0;

        // Skip prefix if present (e.g., "+6" or "-6")
        if (data.Length > 2 && (data[0] == (byte)'+' || data[0] == (byte)'-'))
        {
            // Try to parse chunk size from prefix
            var prefixEnd = 1;
            while (prefixEnd < data.Length && char.IsDigit((char)data[prefixEnd]))
                prefixEnd++;

            if (prefixEnd > 1 && int.TryParse(Encoding.ASCII.GetString(data, 1, prefixEnd - 1), out var parsedChunkSize))
            {
                chunkSize = parsedChunkSize;
                offset = prefixEnd;
            }
            else
            {
                offset = 1; // Skip just the + or -
            }
        }

        // Parse 6-byte blocks (4-byte IP + 2-byte port)
        while (offset + chunkSize <= data.Length)
        {
            // Additional bounds check before parsing
            if (offset < 0 || offset + chunkSize > data.Length)
            {
                _logger?.LogWarning($"Invalid offset {offset} or chunk size {chunkSize} for data length {data.Length}");
                break;
            }

            var serverEndPoint = ByteReader.ParseServerAddress(data, offset);
            if (serverEndPoint != null)
            {
                servers.Add(serverEndPoint);
            }
            offset += chunkSize;
        }

        return servers;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Don't dispose shared HttpClient - it's static and reusable
            _disposed = true;
        }
    }
}

