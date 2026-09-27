using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Tests
{
    // Хост и клиенты по-настоящему соединяются через 127.0.0.1 — как несколько копий игры на одном ПК.
    public partial class NetworkTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task TwoPlayers_JoinStartAndPlay()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(new GameHostOptions { Port = port, EnableDiscovery = false });
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            await using var guest = new Inbox($"127.0.0.1:{port}");

            Assert.Null(await hostPlayer.Client.ConnectAsync("Хост", host.HostToken));
            Assert.Null(await guest.Client.ConnectAsync("Гість"));

            var lobby = await guest.WaitLobbyAsync(s => s.Seats.Count == 2);
            Assert.Equal("Не готові: Гість.", lobby.StartBlockedReason);
            Assert.Equal("Почати гру може лише хост.", await guest.Client.StartGameAsync());

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
            string currentName = currentId == 0 ? "Хост" : "Гість";

            Assert.Contains(new RollDice(currentId), currentFirst.AvailableActions);
            Assert.Contains(currentFirst.AvailableActions, a => a is ProposeTrade);
            Assert.Empty(waitingFirst.AvailableActions);

            // Не в свой ход — отказ. Подставить чужой Id тоже нельзя: хост берёт Id по подключению.
            Assert.Equal($"Зараз ходить {currentName}.", await waiting.Client.SendActionAsync(new RollDice(waitingId)));
            Assert.Equal($"Зараз ходить {currentName}.", await waiting.Client.SendActionAsync(new RollDice(currentId)));

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
            await using var host = await GameHost.StartAsync(new GameHostOptions { Port = port, EnableDiscovery = false });

            await Assert.ThrowsAsync<IOException>(() => GameHost.StartAsync(new GameHostOptions { Port = port, EnableDiscovery = false }));
        }

        [Fact]
        public async Task Connect_ToNobody_ReturnsError()
        {
            int port = FreePort();
            await using var client = new GameClient($"127.0.0.1:{port}");

            var error = await client.ConnectAsync("Аня");

            Assert.StartsWith($"Не вдалося підключитися до 127.0.0.1:{port}.", error);
        }

        [Fact]
        public async Task Finder_FindsGameOnThisComputer()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(new GameHostOptions { Port = port });
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

        // --- Этап 5: таймер, возвращение, боты, сохранение ---

        // Хост и гость в начатой партии. Возвращает, кто ходит первым.
        private static async Task<int> StartTwoPlayerGameAsync(GameHost host, Inbox hostPlayer, Inbox guest)
        {
            Assert.Null(await hostPlayer.Client.ConnectAsync("Хост", host.HostToken));
            Assert.Null(await guest.Client.ConnectAsync("Гість"));
            await guest.WaitLobbyAsync(s => s.Seats.Count == 2);
            Assert.Null(await guest.Client.SetReadyAsync(true));
            Assert.Null(await hostPlayer.Client.StartGameAsync());
            var first = await guest.NextUpdateAsync();
            await hostPlayer.NextUpdateAsync();
            return first.Snapshot.CurrentPlayerId;
        }

        [Fact]
        public async Task TimerRunsOut_GameRollsForPlayer()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(new GameHostOptions
            {
                Port = port, EnableDiscovery = false, TurnTimeout = TimeSpan.FromMilliseconds(400),
            });
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            await using var guest = new Inbox($"127.0.0.1:{port}");
            int first = await StartTwoPlayerGameAsync(host, hostPlayer, guest);

            var rolled = await guest.WaitUpdateAsync(u => u.Events.OfType<DiceRolled>().Any());

            Assert.Equal(first, rolled.Events.OfType<DiceRolled>().First().PlayerId);
            Assert.NotNull(rolled.Timer);
        }

        [Fact]
        public async Task Update_CarriesTimerAndSeats()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(new GameHostOptions { Port = port, EnableDiscovery = false });
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            await using var guest = new Inbox($"127.0.0.1:{port}");
            Assert.Null(await hostPlayer.Client.ConnectAsync("Хост", host.HostToken));
            Assert.Null(await guest.Client.ConnectAsync("Гість"));
            await guest.WaitLobbyAsync(s => s.Seats.Count == 2);
            await guest.Client.SetReadyAsync(true);
            await hostPlayer.Client.StartGameAsync();

            var update = await guest.NextUpdateAsync();

            Assert.Equal(new[] { update.Snapshot.CurrentPlayerId }, update.Timer!.AwaitedIds);
            Assert.InRange(update.Timer.SecondsLeft, 75, 80);
            Assert.All(update.Seats!, s => Assert.Equal(SeatConnection.Online, s.Connection));
            Assert.True(update.Duration!.Running);
            Assert.InRange(update.Duration.Seconds, 0, 5);
        }

        [Fact]
        public async Task Disconnected_Player_ComesBackByName()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(new GameHostOptions { Port = port, EnableDiscovery = false });
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            var guest = new Inbox($"127.0.0.1:{port}");
            await StartTwoPlayerGameAsync(host, hostPlayer, guest);

            await guest.DisposeAsync();
            await hostPlayer.WaitNoticeAsync("Гість відключився");
            var offline = await hostPlayer.WaitUpdateAsync(u => u.Seats!.Any(s => s.Connection == SeatConnection.Offline));
            Assert.Equal(SeatConnection.Offline, offline.Seats!.Single(s => s.PlayerId == 1).Connection);

            await using var back = new Inbox($"127.0.0.1:{port}");
            Assert.Null(await back.Client.ConnectAsync("Гість"));

            var info = await back.Starts.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            var resync = await back.NextUpdateAsync();
            Assert.Equal(1, info.MyPlayerId);
            Assert.True(resync.IsResync);
            Assert.IsType<GameStarted>(resync.Events[0]);
            await hostPlayer.WaitNoticeAsync("Гість повернувся");
        }

        [Fact]
        public async Task Disconnected_Player_IsReplacedByBot()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(new GameHostOptions
            {
                Port = port, EnableDiscovery = false, BotTakeover = TimeSpan.FromMilliseconds(300), BotDelay = TimeSpan.FromMilliseconds(50),
            });
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            var guest = new Inbox($"127.0.0.1:{port}");
            await StartTwoPlayerGameAsync(host, hostPlayer, guest);

            await guest.DisposeAsync();

            await hostPlayer.WaitNoticeAsync("за нього грає бот");
            var update = await hostPlayer.WaitUpdateAsync(u => u.Seats!.Any(s => s.Connection == SeatConnection.Bot));
            Assert.Equal(SeatConnection.Bot, update.Seats!.Single(s => s.PlayerId == 1).Connection);
        }

        [Fact]
        public async Task LobbyBot_PlaysItsTurns()
        {
            int port = FreePort();
            await using var host = await GameHost.StartAsync(new GameHostOptions
            {
                Port = port, EnableDiscovery = false, BotDelay = TimeSpan.FromMilliseconds(50), TurnTimeout = TimeSpan.FromMilliseconds(300),
            });
            await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
            Assert.Null(await hostPlayer.Client.ConnectAsync("Хост", host.HostToken));
            Assert.Null(await hostPlayer.Client.AddBotAsync());
            await hostPlayer.WaitLobbyAsync(s => s.Seats.Count == 2);
            Assert.Null(await hostPlayer.Client.StartGameAsync());

            // За хоста ходит таймер, за бота — бот.
            var botMove = await hostPlayer.WaitUpdateAsync(u => u.Events.OfType<DiceRolled>().Any(d => d.PlayerId == 1));

            Assert.Contains(botMove.Seats!, s => s.PlayerId == 1 && s.Connection == SeatConnection.Bot);
        }

        [Fact]
        public async Task SavedGame_ResumesWhereItStopped()
        {
            var store = new SaveStore(Path.Combine(Path.GetTempPath(), "monopoly-tests", Guid.NewGuid().ToString("N")));
            int port = FreePort();
            GameSnapshot before;
            await using (var host = await GameHost.StartAsync(new GameHostOptions { Port = port, EnableDiscovery = false, Saves = store }))
            {
                await using var hostPlayer = new Inbox($"127.0.0.1:{port}");
                await using var guest = new Inbox($"127.0.0.1:{port}");
                int current = await StartTwoPlayerGameAsync(host, hostPlayer, guest);
                var mover = current == 0 ? hostPlayer : guest;
                Assert.Null(await mover.Client.SendActionAsync(new RollDice(current)));
                before = (await hostPlayer.WaitUpdateAsync(u => u.Events.OfType<DiceRolled>().Any())).Snapshot;
            }

            var save = Assert.Single(store.ListUnfinished());
            Assert.Equal("Хост", save.HostName);
            Assert.NotEmpty(save.Actions);
            Assert.True(save.PlayedSeconds >= 0);

            int port2 = FreePort();
            await using var resumed = await GameHost.ResumeAsync(save, new GameHostOptions { Port = port2, EnableDiscovery = false, Saves = store });
            await using var hostAgain = new Inbox($"127.0.0.1:{port2}");
            Assert.Null(await hostAgain.Client.ConnectAsync(resumed.HostName!, resumed.HostToken));

            var info = await hostAgain.Starts.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            var resync = await hostAgain.NextUpdateAsync();
            Assert.Equal(0, info.MyPlayerId);
            Assert.Equal(before.Players, resync.Snapshot.Players);
            Assert.Equal(before.Cells, resync.Snapshot.Cells);
            Assert.Contains(resync.Seats!, s => s.PlayerId == 1 && s.Connection == SeatConnection.Offline);
            Assert.True(resync.Duration!.Seconds >= save.PlayedSeconds);

            await resumed.DisposeAsync();
            Directory.Delete(store.Directory, recursive: true);
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
            public Channel<string> Notices { get; } = Channel.CreateUnbounded<string>();

            public Inbox(string address)
            {
                Client = new GameClient(address);
                Client.LobbyChanged += s => Lobbies.Writer.TryWrite(s);
                Client.GameStarted += s => Starts.Writer.TryWrite(s);
                Client.GameUpdated += u => Updates.Writer.TryWrite(u);
                Client.Notice += n => Notices.Writer.TryWrite(n);
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

            // Ждёт обновление, в котором выполнено условие; остальные пропускает.
            public async Task<GameUpdate> WaitUpdateAsync(Func<GameUpdate, bool> condition)
            {
                while (true)
                {
                    var update = await NextUpdateAsync();
                    if (condition(update))
                        return update;
                }
            }

            public async Task<string> WaitNoticeAsync(string part)
            {
                while (true)
                {
                    var notice = await Notices.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
                    if (notice.Contains(part))
                        return notice;
                }
            }

            public ValueTask DisposeAsync() => Client.DisposeAsync();
        }
    }
}
