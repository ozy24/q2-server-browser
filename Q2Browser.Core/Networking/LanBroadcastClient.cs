using System.Net;
using System.Net.Sockets;
using System.Text;
using Q2Browser.Core.Models;
using Q2Browser.Core.Protocol;
using Q2Browser.Core.Services;

namespace Q2Browser.Core.Networking;

public class LanBroadcastClient
{
    private readonly Settings _settings;
    private readonly ILogger? _logger;

    public LanBroadcastClient(Settings settings, ILogger? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<IPEndPoint>> DiscoverServersAsync(CancellationToken cancellationToken = default)
    {
        var servers = new List<IPEndPoint>();

        if (!_settings.EnableLanBroadcast)
        {
            return servers;
        }

        try
        {
            _logger?.LogInfo("Starting LAN broadcast discovery...");

            using var client = new UdpClient();
            client.EnableBroadcast = true;
            client.Client.ReceiveTimeout = 2000;

            // Send broadcast status request
            var query = Encoding.ASCII.GetBytes("status");
            var packet = PacketHeader.PrependOobHeader(query);
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, 27910); // PORT_SERVER

            _logger?.LogDebug($"Sending broadcast to {broadcastEndPoint}");
            await client.SendAsync(packet, packet.Length, broadcastEndPoint);

            var endTime = DateTime.UtcNow.AddSeconds(3);
            var discoveredAddresses = new HashSet<string>();

            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var timeoutTask = Task.Delay(endTime - DateTime.UtcNow, cancellationToken);
                    var receiveTask = client.ReceiveAsync();
                    var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                    if (completedTask == timeoutTask)
                        break;

                    var result = await receiveTask;
                    var addressKey = $"{result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port}";

                    if (discoveredAddresses.Contains(addressKey))
                        continue;

                    discoveredAddresses.Add(addressKey);
                    servers.Add(result.RemoteEndPoint);
                    _logger?.LogDebug($"Discovered LAN server: {result.RemoteEndPoint}");
                }
                catch (SocketException)
                {
                    break;
                }
            }

            _logger?.LogInfo($"LAN broadcast discovered {servers.Count} server(s)");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during LAN broadcast: {ex.Message}", ex.StackTrace);
        }

        return servers;
    }
}

