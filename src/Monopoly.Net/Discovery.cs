using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Monopoly.Net
{
    // Поиск игр: клиент рассылает запрос по UDP, хосты отвечают описанием игры.
    // Работает в домашней сети, Radmin VPN и ZeroTier. Tailscale рассылку не пропускает — там вводят IP вручную.
    internal static class DiscoveryProtocol
    {
        public static readonly byte[] Request = Encoding.UTF8.GetBytes("MONOPOLY_DISCOVER");
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    }

    // Отвечает на запросы поиска. Работает на хосте, пока открыто лобби или идёт игра.
    internal sealed class DiscoveryResponder : IDisposable
    {
        private readonly UdpClient udp;
        private readonly Func<DiscoveredGame> describe;
        private readonly CancellationTokenSource stop = new();

        private DiscoveryResponder(UdpClient udp, Func<DiscoveredGame> describe)
        {
            this.udp = udp;
            this.describe = describe;
            _ = ListenAsync();
        }

        // Если UDP-порт занят, поиск просто не работает — к игре можно подключиться по IP.
        public static DiscoveryResponder? TryStart(int port, Func<DiscoveredGame> describe)
        {
            try
            {
                return new DiscoveryResponder(new UdpClient(new IPEndPoint(IPAddress.Any, port)), describe);
            }
            catch (SocketException)
            {
                return null;
            }
        }

        private async Task ListenAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    var request = await udp.ReceiveAsync(stop.Token);
                    if (!request.Buffer.AsSpan().SequenceEqual(DiscoveryProtocol.Request))
                        continue;
                    var reply = JsonSerializer.SerializeToUtf8Bytes(describe(), DiscoveryProtocol.Json);
                    await udp.SendAsync(reply, request.RemoteEndPoint, stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
                {
                    // Windows сообщает об ошибке, если адресат ответа недоступен; слушаем дальше.
                    if (stop.IsCancellationRequested)
                        return;
                }
            }
        }

        public void Dispose()
        {
            stop.Cancel();
            udp.Dispose();
        }
    }

    public static class GameFinder
    {
        // Рассылает запрос во все сети компьютера и собирает ответы за время timeout.
        public static async Task<IReadOnlyList<DiscoveredGame>> FindAsync(
            TimeSpan timeout, int port = NetDefaults.Port, CancellationToken cancellationToken = default)
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            foreach (var address in BroadcastAddresses())
            {
                try
                {
                    await udp.SendAsync(DiscoveryProtocol.Request, new IPEndPoint(address, port), cancellationToken);
                }
                catch (SocketException)
                {
                    // Сеть без рассылки — пропускаем.
                }
            }

            // Одна и та же игра может ответить из нескольких сетей — оставляем первый ответ.
            var found = new Dictionary<Guid, DiscoveredGame>();
            using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timer.CancelAfter(timeout);
            while (true)
            {
                UdpReceiveResult reply;
                try
                {
                    reply = await udp.ReceiveAsync(timer.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    continue;
                }

                DiscoveredGame? game;
                try
                {
                    game = JsonSerializer.Deserialize<DiscoveredGame>(reply.Buffer, DiscoveryProtocol.Json);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (game is not null && !found.ContainsKey(game.GameId))
                    found[game.GameId] = game with { Address = reply.RemoteEndPoint.Address.ToString() };
            }

            cancellationToken.ThrowIfCancellationRequested();
            return found.Values.ToList();
        }

        // Широковещательные адреса всех сетей (у Windows общий 255.255.255.255 уходит только в одну сеть)
        // и 127.0.0.1 — чтобы найти игру, созданную на этом же компьютере.
        private static IEnumerable<IPAddress> BroadcastAddresses()
        {
            var result = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.Broadcast };
            foreach (var unicast in LocalAddresses.ActiveIPv4())
            {
                var mask = unicast.IPv4Mask;
                if (mask is null || mask.Equals(IPAddress.Any))
                    continue;
                var ip = unicast.Address.GetAddressBytes();
                var maskBytes = mask.GetAddressBytes();
                var broadcast = new byte[4];
                for (int i = 0; i < 4; i++)
                    broadcast[i] = (byte)(ip[i] | ~maskBytes[i]);
                result.Add(new IPAddress(broadcast));
            }
            return result;
        }
    }
}
