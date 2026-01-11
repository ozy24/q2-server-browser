using System.Net;
using System.Text;
using Q2Browser.Core.Models;
using Q2Browser.Core.Protocol;
using Q2Browser.Core.Services;

namespace Q2Browser.Core.Networking;

public class HttpMasterServerClient
{
    private readonly Settings _settings;
    private readonly ILogger? _logger;
    private readonly HttpClient _httpClient;

    public HttpMasterServerClient(Settings settings, ILogger? logger = null)
    {
        _settings = settings;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<List<IPEndPoint>> QueryServersAsync(CancellationToken cancellationToken = default)
    {
        var servers = new List<IPEndPoint>();

        if (string.IsNullOrEmpty(_settings.HttpMasterServerUrl))
        {
            _logger?.LogWarning("HTTP master server URL is not configured");
            return servers;
        }

        try
        {
            _logger?.LogInfo($"Fetching server list from HTTP master: {_settings.HttpMasterServerUrl}");

            var response = await _httpClient.GetAsync(_settings.HttpMasterServerUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            _logger?.LogDebug($"Response Content-Type: {contentType}");

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            _logger?.LogInfo($"Received {data.Length} bytes from HTTP master server");

            // q2servers.com returns binary format directly (6-byte chunks: 4-byte IP + 2-byte port)
            // Check if it starts with text prefix like "+6" (q2pro format) or is pure binary
            if (data.Length > 0 && (data[0] == (byte)'+' || data[0] == (byte)'-'))
            {
                // Binary format with prefix like "+6" or "-6" (q2pro internal format)
                _logger?.LogDebug("Detected binary format with prefix");
                servers.AddRange(ParseBinaryFormat(data));
            }
            else if (data.Length > 0 && data[0] == 0xFF && data.Length >= 4 && 
                     data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF)
            {
                // OOB header present, remove it and parse as binary
                _logger?.LogDebug("Detected OOB header, removing and parsing");
                var payload = new byte[data.Length - 4];
                Array.Copy(data, 4, payload, 0, payload.Length);
                servers.AddRange(ParseBinaryFormat(payload, 6));
            }
            else
            {
                // Pure binary format (most common for HTTP master servers)
                _logger?.LogDebug("Parsing as pure binary format (6-byte chunks)");
                servers.AddRange(ParseBinaryFormat(data, 6));
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
            var serverEndPoint = ByteReader.ParseServerAddress(data, offset);
            if (serverEndPoint != null)
            {
                servers.Add(serverEndPoint);
            }
            offset += chunkSize;
        }

        return servers;
    }
}

