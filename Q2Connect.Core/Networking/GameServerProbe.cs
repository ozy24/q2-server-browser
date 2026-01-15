using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Q2Connect.Core.Models;
using Q2Connect.Core.Protocol;
using Q2Connect.Core.Services;

namespace Q2Connect.Core.Networking;

public class GameServerProbe : IDisposable
{
    private const int MAX_STATUS_RESPONSE_SIZE = 64 * 1024; // 64KB
    private const int MAX_CVARS = 256;
    private const int MAX_PLAYERS = 128;

    private readonly Settings _settings;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger? _logger;
    private bool _disposed;

    public GameServerProbe(Settings settings, ILogger? logger = null)
    {
        _settings = settings;
        _semaphore = new SemaphoreSlim(settings.MaxConcurrentProbes, settings.MaxConcurrentProbes);
        _logger = logger;
    }

    public async Task<ServerEntry?> ProbeServerAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GameServerProbe));
            
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        
        try
        {
            return await ProbeServerInternalAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<ServerEntry?> ProbeServerInternalAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        using var client = new UdpClient();
        client.Client.ReceiveTimeout = _settings.ProbeTimeoutMs;

        try
        {
            // Send status command
            var query = Encoding.ASCII.GetBytes("status");
            var packet = PacketHeader.PrependOobHeader(query);

            var startTime = DateTime.UtcNow;
            await client.SendAsync(packet, packet.Length, endPoint).ConfigureAwait(false);
            _logger?.LogDebug($"Probing server {endPoint}");

            // Receive response with timeout using Task.WhenAny
            UdpReceiveResult result;
            try
            {
                var receiveTask = client.ReceiveAsync();
                var timeoutTask = Task.Delay(_settings.ProbeTimeoutMs, cancellationToken);
                var completedTask = await Task.WhenAny(receiveTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    _logger?.LogDebug($"Probe for server {endPoint} timed out after {_settings.ProbeTimeoutMs}ms");
                    return null;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogDebug($"Probe for server {endPoint} was cancelled");
                    return null;
                }

                result = await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug($"Probe for server {endPoint} was cancelled");
                return null;
            }
            
            var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            var data = result.Buffer;
            if (!PacketHeader.HasOobHeader(data))
            {
                _logger?.LogWarning($"Server {endPoint} responded without OOB header");
                return null;
            }

            var payload = PacketHeader.RemoveOobHeader(data);
            
            // Check payload size to prevent DoS
            if (payload.Length > MAX_STATUS_RESPONSE_SIZE)
            {
                _logger?.LogWarning($"Server {endPoint} response too large: {payload.Length} bytes (max: {MAX_STATUS_RESPONSE_SIZE}), truncating");
                var truncated = new byte[MAX_STATUS_RESPONSE_SIZE];
                Array.Copy(payload, truncated, MAX_STATUS_RESPONSE_SIZE);
                payload = truncated;
            }
            
            var response = Encoding.ASCII.GetString(payload);
            _logger?.LogDebug($"Server {endPoint} responded in {elapsed}ms ({payload.Length} bytes)");
            if (response.Length > 0)
            {
                // Use AsSpan for better performance when logging preview
                var previewLength = Math.Min(200, response.Length);
                var preview = response.AsSpan(0, previewLength).ToString();
                _logger?.LogDebug($"Response preview: {preview}");
            }
            else
            {
                _logger?.LogDebug("Response preview: (empty)");
            }

            var serverEntry = new ServerEntry
            {
                Address = endPoint.Address.ToString(),
                Port = endPoint.Port,
                Ping = elapsed
            };

            ParseStatusResponse(response, serverEntry);
            _logger?.LogDebug($"Parsed server {endPoint}: {serverEntry.Hostname} ({serverEntry.CurrentPlayers}/{serverEntry.MaxClients} players)");
            _logger?.LogDebug($"CVARs found: {string.Join(", ", serverEntry.Cvars.Keys)}");
            return serverEntry;
        }
        catch (SocketException ex)
        {
            _logger?.LogDebug($"Server {endPoint} did not respond: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error probing server {endPoint}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a Quake II server status response.
    /// Format after OOB header removal: "print\n\infostring\nplayerlist"
    /// - The "print\n" prefix is skipped
    /// - Infostring format: \key\value\key\value... (backslash-delimited key-value pairs)
    /// - Player list format: score ping "name"\n (one per line)
    /// </summary>
    /// <param name="response">The status response string (OOB header already removed)</param>
    /// <param name="serverEntry">The server entry to populate with parsed data</param>
    private void ParseStatusResponse(string response, ServerEntry serverEntry)
    {
        
        // Skip "print\n" prefix if present
        var startIndex = 0;
        if (response.StartsWith("print\n", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 6; // Length of "print\n"
        }
        
        // Use Span for better performance when parsing
        var remainingSpan = response.AsSpan(startIndex);
        var firstNewline = remainingSpan.IndexOf('\n');
        if (firstNewline < 0)
        {
            firstNewline = remainingSpan.Length;
        }

        // Parse CVARs from first line (key\value format)
        var infostring = remainingSpan.Slice(0, firstNewline).ToString();
        _logger?.LogDebug($"Infostring: {infostring}");
        
        // Parse CVARs: format is \key\value\key\value...
        // The regex should match pairs of backslash-delimited key-value pairs
        // Use a more robust pattern that handles empty values and trailing backslashes
        var cvarRegex = new Regex(@"\\([^\\]+)\\([^\\]*)", RegexOptions.Compiled);
        var cvarMatches = cvarRegex.Matches(infostring);
        
        // Limit CVAR count to prevent DoS
        var cvarCount = 0;
        foreach (Match match in cvarMatches)
        {
            if (cvarCount >= MAX_CVARS)
            {
                _logger?.LogWarning($"CVAR limit reached ({MAX_CVARS}), truncating remaining CVARs");
                break;
            }
            
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (!string.IsNullOrEmpty(key))
            {
                serverEntry.Cvars[key] = value;
                _logger?.LogDebug($"Parsed CVAR: {key} = {value}");
                cvarCount++;
            }
        }
        
        // If no CVARs found, try alternative parsing method
        if (serverEntry.Cvars.Count == 0 && infostring.Length > 0)
        {
            _logger?.LogWarning($"No CVARs parsed with regex, trying manual parse. Infostring: {infostring}");
            
            // Manual parsing: split by backslash and process pairs
            var parts = infostring.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1 && serverEntry.Cvars.Count < MAX_CVARS; i += 2)
            {
                var key = parts[i];
                var value = i + 1 < parts.Length ? parts[i + 1] : string.Empty;
                if (!string.IsNullOrEmpty(key))
                {
                    serverEntry.Cvars[key] = value;
                    _logger?.LogDebug($"Manually parsed CVAR: {key} = {value}");
                }
            }
            
            if (serverEntry.Cvars.Count >= MAX_CVARS)
            {
                _logger?.LogWarning($"CVAR limit reached during manual parsing ({MAX_CVARS})");
            }
        }
        
        if (serverEntry.Cvars.Count == 0)
        {
            _logger?.LogWarning($"Still no CVARs parsed from infostring: {infostring}");
        }

        // Parse player list (everything after first '\n' in remaining string)
        if (firstNewline < remainingSpan.Length - 1)
        {
            var playersSection = remainingSpan.Slice(firstNewline + 1).ToString();
            var playerLines = playersSection.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Limit player count to prevent DoS
            var playerCount = 0;
            foreach (var line in playerLines)
            {
                if (playerCount >= MAX_PLAYERS)
                {
                    _logger?.LogWarning($"Player limit reached ({MAX_PLAYERS}), truncating remaining players");
                    break;
                }
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Format: score ping "name" (name is in quotes)
                // Use regex to parse: number number "quoted string"
                var playerRegex = new Regex(@"^(\d+)\s+(\d+)\s+""([^""]*)""", RegexOptions.Compiled);
                var match = playerRegex.Match(trimmed);
                
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out var score) && 
                        int.TryParse(match.Groups[2].Value, out var ping))
                    {
                        var name = match.Groups[3].Value;
                        serverEntry.Players.Add(new PlayerInfo
                        {
                            Score = score,
                            Ping = ping,
                            Name = name
                        });
                        playerCount++;
                    }
                }
                else
                {
                    // Fallback: try parsing without quotes (some servers might not use quotes)
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        if (int.TryParse(parts[0], out var score) && 
                            int.TryParse(parts[1], out var ping))
                        {
                            var name = string.Join(" ", parts.Skip(2)).Trim('"');
                            serverEntry.Players.Add(new PlayerInfo
                            {
                                Score = score,
                                Ping = ping,
                                Name = name
                            });
                            playerCount++;
                        }
                    }
                }
            }
        }
    }

    public async Task<List<ServerEntry>> ProbeServersAsync(
        IEnumerable<IPEndPoint> endPoints,
        IProgress<ServerEntry> progress,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GameServerProbe));
            
        var endPointList = endPoints.ToList();
        _logger?.LogInfo($"Starting to probe {endPointList.Count} server(s) (max concurrent: {_settings.MaxConcurrentProbes})");
        
        int completed = 0;
        int successful = 0;

        var tasks = endPointList.Select(async endPoint =>
        {
            var entry = await ProbeServerAsync(endPoint, cancellationToken).ConfigureAwait(false);
            completed++;
            
            if (entry != null)
            {
                successful++;
                progress.Report(entry);
            }

            if (completed % 10 == 0 || completed == endPointList.Count)
            {
                _logger?.LogInfo($"Probing progress: {completed}/{endPointList.Count} completed, {successful} successful");
            }

            return entry;
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger?.LogInfo($"Probing complete: {successful}/{endPointList.Count} servers responded");
        return results.Where(r => r != null).ToList()!;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _semaphore?.Dispose();
            _disposed = true;
        }
    }
}

