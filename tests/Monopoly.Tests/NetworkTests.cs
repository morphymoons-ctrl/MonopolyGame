using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Tests
{
    // Хост и клиенты по-настоящему соединяются через 127.0.0.1 — как несколько копий игры на одном ПК.
    public class NetworkTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task TwoPlayers_JoinStartAndPlay()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(port, enableDiscovery: false);
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            await using var guest = new Inbox($"127.0.0.1:{port}");

            Assert.Null(await hostPlayer.Client.ConnectAsync("Хост", host.HostToken));
            Assert.Null(await guest.Client.ConnectAsync("Гость"));

            var lobby = await guest.WaitLobbyAsync(s => s.Seats.Count == 2);
            Assert.Equal("Не готовы: Гость.", lobby.StartBlockedReason);
            Assert.Equal("Начать игру может только хост.", await guest.Client.StartGameAsync());

            Assert.Null(await guest.Client.SetReadyAsync(true));
            Assert.Null(await hostPlayer.Client.StartGameAsync());

            var hostInfo = await hostPlayer.Starts.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            var guestInfo = await guest.Starts.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            Assert.Equal(0, hostInfo.MyPlayerId);
            Assert.Equal(1, guestInfo.MyPlayerId);
            Assert.Equal(2, guestInfo.ColorByPlayerId.Count);

            var hostFirst = await hostPlayer.NextUpdateAsync();
            var guestFirst = await guest.NextUpdateAsync();
            Assert.IsType<GameStarted>(guestFirst.Events[0]);

            // Кто ходит первым, решает жребий на хосте.
            int currentId = guestFirst.Snapshot.CurrentPlayerId;
            var (current, waiting) = currentId == 0 ? (hostPlayer, guest) : (guest, hostPlayer);
            var (currentFirst, waitingFirst) = currentId == 0 ? (hostFirst, guestFirst) : (guestFirst, hostFirst);
            int waitingId = 1 - currentId;
            string currentName = currentId == 0 ? "Хост" : "Гость";

            Assert.Contains(new RollDice(currentId), currentFirst.AvailableActions);
            Assert.Contains(currentFirst.AvailableActions, a => a is ProposeTrade);
            Assert.Empty(waitingFirst.AvailableActions);

            // Не в свой ход — отказ. Подставить чужой Id тоже нельзя: хост берёт Id по подключению.
            Assert.Equal($"Сейчас ходит {currentName}.", await waiting.Client.SendActionAsync(new RollDice(waitingId)));
            Assert.Equal($"Сейчас ходит {currentName}.", await waiting.Client.SendActionAsync(new RollDice(currentId)));

            Assert.Null(await current.Client.SendActionAsync(new RollDice(currentId)));

            var seenByWaiting = await waiting.NextUpdateAsync();
            var rolled = Assert.IsType<DiceRolled>(seenByWaiting.Events[0]);
            Assert.Equal(currentId, rolled.PlayerId);
            Assert.Equal(rolled.Total, seenByWaiting.Snapshot.FindPlayer(currentId)!.Position);
        }

        [Fact]
        public async Task SecondHost_OnSamePort_Fails()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(port, enableDiscovery: false);

            await Assert.ThrowsAsync<IOException>(() => GameHost.StartAsync(port, enableDiscovery: false));
        }

        [Fact]
        public async Task Connect_ToNobody_ReturnsError()
        {
            int port = FreePort();
            await using var client = new GameClient($"127.0.0.1:{port}");

            var error = await client.ConnectAsync("Аня");

            Assert.StartsWith($"Не удалось подключиться к 127.0.0.1:{port}.", error);
        }

        [Fact]
        public async Task Finder_FindsGameOnThisComputer()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(port);
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            Assert.Null(await hostPlayer.Client.ConnectAsync("Хост", host.HostToken));

            var games = await GameFinder.FindAsync(TimeSpan.FromSeconds(1), port);

            var game = Assert.Single(games);
            Assert.Equal("Хост", game.HostName);
            Assert.Equal(1, game.Players);
            Assert.Equal(GameRules.MaxPlayers, game.MaxPlayers);
            Assert.Equal(NetDefaults.GameVersion, game.Version);
            Assert.False(game.InProgress);
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        // Клиент, который складывает всё пришедшее в очереди.
        private sealed class Inbox : IAsyncDisposable
        {
            public GameClient Client { get; }
            public Channel<LobbyState> Lobbies { get; } = Channel.CreateUnbounded<LobbyState>();
            public Channel<GameStartInfo> Starts { get; } = Channel.CreateUnbounded<GameStartInfo>();
            public Channel<GameUpdate> Updates { get; } = Channel.CreateUnbounded<GameUpdate>();

            public Inbox(string address)
            {
                Client = new GameClient(address);
                Client.LobbyChanged += s => Lobbies.Writer.TryWrite(s);
                Client.GameStarted += s => Starts.Writer.TryWrite(s);
                Client.GameUpdated += u => Updates.Writer.TryWrite(u);
            }

            public async Task<LobbyState> WaitLobbyAsync(Func<LobbyState, bool> condition)
            {
                while (true)
                {
                    var state = await Lobbies.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
                    if (condition(state))
                        return state;
                }
            }

            public Task<GameUpdate> NextUpdateAsync() => Updates.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

            public ValueTask DisposeAsync() => Client.DisposeAsync();
        }
    }
}
