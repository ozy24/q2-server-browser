using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Q2Browser.Core.Models;
using Q2Browser.Core.Protocol;
using Q2Browser.Core.Services;

namespace Q2Browser.Core.Networking;

public class GameServerProbe
{
    private readonly Settings _settings;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger? _logger;

    public GameServerProbe(Settings settings, ILogger? logger = null)
    {
        _settings = settings;
        _semaphore = new SemaphoreSlim(settings.MaxConcurrentProbes, settings.MaxConcurrentProbes);
        _logger = logger;
    }

    public async Task<ServerEntry?> ProbeServerAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            return await ProbeServerInternalAsync(endPoint, cancellationToken);
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
            await client.SendAsync(packet, packet.Length, endPoint);
            _logger?.LogDebug($"Probing server {endPoint}");

            // Receive response
            var result = await client.ReceiveAsync();
            var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            var data = result.Buffer;
            if (!PacketHeader.HasOobHeader(data))
            {
                _logger?.LogWarning($"Server {endPoint} responded without OOB header");
                return null;
            }

            var payload = PacketHeader.RemoveOobHeader(data);
            var response = Encoding.ASCII.GetString(payload);
            _logger?.LogDebug($"Server {endPoint} responded in {elapsed}ms ({payload.Length} bytes)");
            _logger?.LogDebug($"Response preview: {response.Substring(0, Math.Min(200, response.Length))}");

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

    private void ParseStatusResponse(string response, ServerEntry serverEntry)
    {
        // Quake 2 status response format:
        // Response after OOB header removal: "print\n\infostring\nplayerlist"
        // The "print\n" prefix needs to be skipped
        // Infostring format: \key\value\key\value...
        // Then: player list lines in format: score ping "name"\n
        
        // Skip "print\n" prefix if present
        var startIndex = 0;
        if (response.StartsWith("print\n", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 6; // Length of "print\n"
        }
        
        var remaining = response.Substring(startIndex);
        var firstNewline = remaining.IndexOf('\n');
        if (firstNewline < 0)
        {
            firstNewline = remaining.Length;
        }

        // Parse CVARs from first line (key\value format)
        var infostring = remaining.Substring(0, firstNewline);
        _logger?.LogDebug($"Infostring: {infostring}");
        
        // Parse CVARs: format is \key\value\key\value...
        // The regex should match pairs of backslash-delimited key-value pairs
        // Use a more robust pattern that handles empty values and trailing backslashes
        var cvarRegex = new Regex(@"\\([^\\]+)\\([^\\]*)", RegexOptions.Compiled);
        var cvarMatches = cvarRegex.Matches(infostring);
        
        foreach (Match match in cvarMatches)
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            if (!string.IsNullOrEmpty(key))
            {
                serverEntry.Cvars[key] = value;
                _logger?.LogDebug($"Parsed CVAR: {key} = {value}");
            }
        }
        
        // If no CVARs found, try alternative parsing method
        if (serverEntry.Cvars.Count == 0 && infostring.Length > 0)
        {
            _logger?.LogWarning($"No CVARs parsed with regex, trying manual parse. Infostring: {infostring}");
            
            // Manual parsing: split by backslash and process pairs
            var parts = infostring.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i += 2)
            {
                var key = parts[i];
                var value = i + 1 < parts.Length ? parts[i + 1] : string.Empty;
                if (!string.IsNullOrEmpty(key))
                {
                    serverEntry.Cvars[key] = value;
                    _logger?.LogDebug($"Manually parsed CVAR: {key} = {value}");
                }
            }
        }
        
        if (serverEntry.Cvars.Count == 0)
        {
            _logger?.LogWarning($"Still no CVARs parsed from infostring: {infostring}");
        }

        // Parse player list (everything after first '\n' in remaining string)
        if (firstNewline < remaining.Length - 1)
        {
            var playersSection = remaining.Substring(firstNewline + 1);
            var playerLines = playersSection.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in playerLines)
            {
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
        var endPointList = endPoints.ToList();
        _logger?.LogInfo($"Starting to probe {endPointList.Count} server(s) (max concurrent: {_settings.MaxConcurrentProbes})");
        
        int completed = 0;
        int successful = 0;

        var tasks = endPointList.Select(async endPoint =>
        {
            var entry = await ProbeServerAsync(endPoint, cancellationToken);
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

        var results = await Task.WhenAll(tasks);
        _logger?.LogInfo($"Probing complete: {successful}/{endPointList.Count} servers responded");
        return results.Where(r => r != null).ToList()!;
    }
}

