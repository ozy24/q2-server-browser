using System.Net;
using System.Net.Sockets;
using System.Text;
using Q2Connect.Core.Models;
using Q2Connect.Core.Protocol;
using Q2Connect.Core.Services;

namespace Q2Connect.Core.Networking;

public class MasterServerClient
{
    private readonly Settings _settings;
    private readonly ILogger? _logger;

    public MasterServerClient(Settings settings, ILogger? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<IPEndPoint>> QueryServersAsync(CancellationToken cancellationToken = default)
    {
        var servers = new List<IPEndPoint>();
        
        _logger?.LogInfo($"Querying UDP master server: {_settings.MasterServerAddress}:{_settings.MasterServerPort}");
        
        using var client = new UdpClient();
        // Use ProbeTimeoutMs setting, but ensure minimum 1000ms for master server queries
        var receiveTimeoutMs = Math.Max(_settings.ProbeTimeoutMs, 1000);
        client.Client.ReceiveTimeout = receiveTimeoutMs;

        try
        {
            IPAddress[] addresses;
            try
            {
                addresses = Dns.GetHostAddresses(_settings.MasterServerAddress);
                _logger?.LogDebug($"Resolved {_settings.MasterServerAddress} to {addresses.Length} address(es)");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to resolve master server address: {_settings.MasterServerAddress}", ex.Message);
                return servers;
            }

            var masterServerEndPoint = new IPEndPoint(addresses[0], _settings.MasterServerPort);
            _logger?.LogInfo($"Connecting to master server: {masterServerEndPoint}");

            // Send getservers quake2 34
            var query = Encoding.ASCII.GetBytes("getservers quake2 34");
            var packet = PacketHeader.PrependOobHeader(query);
            
            _logger?.LogDebug($"Sending query packet ({packet.Length} bytes)");
            await client.SendAsync(packet, packet.Length, masterServerEndPoint).ConfigureAwait(false);
            _logger?.LogDebug("Query packet sent, waiting for response...");

            var receivedData = new List<byte>();
            // Use ProbeTimeoutMs setting, but ensure minimum 1000ms for master server queries
            var queryTimeoutMs = Math.Max(_settings.ProbeTimeoutMs, 1000);
            var timeout = TimeSpan.FromMilliseconds(queryTimeoutMs);
            var endTime = DateTime.UtcNow.Add(timeout);
            int packetCount = 0;
            bool receivedEndMarker = false;

            _logger?.LogDebug($"Waiting for response (timeout: {timeout.TotalSeconds}s)...");

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested && !receivedEndMarker)
            {
                try
                {
                    // Use Task.WhenAny to implement timeout
                    var receiveTask = client.ReceiveAsync();
                    var timeoutTask = Task.Delay(endTime - DateTime.UtcNow, cancellationToken);
                    var completedTask = await Task.WhenAny(receiveTask, timeoutTask).ConfigureAwait(false);

                    if (completedTask == timeoutTask)
                    {
                        _logger?.LogWarning("Timeout waiting for master server response");
                        break;
                    }

                    if (completedTask != receiveTask)
                        continue;

                    var result = await receiveTask.ConfigureAwait(false);
                    var data = result.Buffer;
                    packetCount++;

                    _logger?.LogDebug($"Received packet #{packetCount} ({data.Length} bytes)");

                    if (!PacketHeader.HasOobHeader(data))
                    {
                        _logger?.LogWarning("Received packet without OOB header, ignoring");
                        continue;
                    }

                    var payload = PacketHeader.RemoveOobHeader(data);
                    
                    // Check for end marker: \EOT (0x04) or empty response
                    if (payload.Length == 0 || payload[0] == 0x04)
                    {
                        _logger?.LogDebug("Received end marker, stopping reception");
                        receivedEndMarker = true;
                        break;
                    }

                    receivedData.AddRange(payload);
                    _logger?.LogDebug($"Added {payload.Length} bytes to buffer (total: {receivedData.Count} bytes)");
                }
                catch (SocketException ex)
                {
                    _logger?.LogWarning($"Socket exception while receiving: {ex.Message} (ErrorCode: {ex.SocketErrorCode})");
                    break;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInfo("Master server query cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Unexpected error receiving packet: {ex.Message}", ex.StackTrace);
                    break;
                }
            }

            _logger?.LogInfo($"Received {packetCount} packet(s), total data: {receivedData.Count} bytes");

            // Parse 6-byte blocks (4-byte IP + 2-byte port)
            int parsedCount = 0;
            for (int i = 0; i <= receivedData.Count - 6; i += 6)
            {
                var serverEndPoint = ByteReader.ParseServerAddress(receivedData.ToArray(), i);
                if (serverEndPoint != null)
                {
                    servers.Add(serverEndPoint);
                    parsedCount++;
                }
            }

            _logger?.LogInfo($"Parsed {parsedCount} server address(es) from master server response");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error querying master server: {ex.Message}", ex.StackTrace);
        }

        _logger?.LogInfo($"Master server query complete. Found {servers.Count} server(s)");
        return servers;
    }
}

