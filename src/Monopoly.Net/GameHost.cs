using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monopoly.Core;

namespace Monopoly.Net
{
    public sealed record GameHostOptions
    {
        public int Port { get; init; } = NetDefaults.Port;
        public bool EnableDiscovery { get; init; } = true;
        public TimeSpan TurnTimeout { get; init; } = NetDefaults.TurnTimeout;
        public TimeSpan BotTakeover { get; init; } = NetDefaults.BotTakeover;
        public TimeSpan BotDelay { get; init; } = NetDefaults.BotDelay;
        // Куда сохранять партию после каждого действия; null — не сохранять.
        public SaveStore? Saves { get; init; }
        // Открытый ключ администратора (§14). Тесты подставляют свой.
        public string AdminPublicKey { get; init; } = AdminAuth.OwnerPublicKey;
    }

    // Сервер игры внутри приложения того, кто создал игру. Только он выполняет правила,
    // следит за таймером хода и играет за ботов.
    public sealed class GameHost : IAsyncDisposable
    {
        // Все обращения к лобби и игре — по очереди, вместе с рассылкой: так клиенты получают события по порядку.
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly GameHostOptions options;
        private readonly Lobby lobby;
        private readonly Guid gameId;
        private readonly CancellationTokenSource stop = new();
        private WebApplication? app;
        private IHubContext<GameHub>? hub;
        private DiscoveryResponder? discovery;
        private Game? game;
        // Когда в игре что-то изменилось в последний раз: от этого момента считаются таймер хода и пауза бота.
        private DateTime lastChange = DateTime.UtcNow;
        // Длительность партии: сыграно до этого запуска хоста + с момента запуска; после победы часы стоят.
        private TimeSpan playedBefore;
        private DateTime playStarted = DateTime.UtcNow;
        private TimeSpan? finalDuration;
        // Описание для ответа на поиск; обновляется после каждой операции, читается из потока UDP.
        private volatile DiscoveredGame discoveryInfo;
        // Подключения панели администратора: одноразовое число, вошла ли панель, номер последней команды.
        private readonly Dictionary<string, AdminSession> admins = new();
        private static readonly JsonSerializerOptions Json = ProtocolJson.CreateOptions();

        public int Port => options.Port;
        // Передаётся собственному клиенту хоста, чтобы лобби узнало хоста.
        public string HostToken { get; } = Guid.NewGuid().ToString("N");
        // Имя хоста в продолженной партии: под ним приложение хоста возвращается на своё место.
        public string? HostName => lobby.HostName;

        private GameHost(GameHostOptions options, Guid gameId)
        {
            this.options = options;
            this.gameId = gameId;
            lobby = new Lobby(HostToken, NetDefaults.GameVersion);
            discoveryInfo = DescribeForDiscovery();
        }

        private static DateTime Now => DateTime.UtcNow;

        // Новая игра. Если порт занят — исключение IOException.
        public static Task<GameHost> StartAsync(GameHostOptions? options = null) =>
            LaunchAsync(new GameHost(options ?? new GameHostOptions(), Guid.NewGuid()));

        // Продолжение сохранённой партии: все места ждут своих игроков.
        public static Task<GameHost> ResumeAsync(SaveFile save, GameHostOptions? options = null)
        {
            if (save.Version != NetDefaults.GameVersion)
                throw new InvalidDataException($"Збереження від версії {save.Version}, а гра — {NetDefaults.GameVersion}.");

            var host = new GameHost(options ?? new GameHostOptions(), save.GameId);
            host.game = Game.Replay(save.Seats.Select(s => s.Name).ToList(), save.Seed, save.Actions, save.Theme);
            host.playedBefore = TimeSpan.FromSeconds(save.PlayedSeconds);
            host.playStarted = Now;
            host.lobby.Restore(save.Seats, Now, save.Theme);
            host.discoveryInfo = host.DescribeForDiscovery();
            return LaunchAsync(host);
        }

        private static async Task<GameHost> LaunchAsync(GameHost host)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Any, host.Port));
            builder.Services.AddSingleton(host);
            builder.Services.AddSignalR()
                .AddJsonProtocol(json => ProtocolJson.Configure(json.PayloadSerializerOptions));

            var app = builder.Build();
            app.MapHub<GameHub>(NetDefaults.HubPath);
            await app.StartAsync();

            host.app = app;
            host.hub = app.Services.GetRequiredService<IHubContext<GameHub>>();
            if (host.options.EnableDiscovery)
                host.discovery = DiscoveryResponder.TryStart(host.Port, () => host.discoveryInfo);
            _ = host.RunClockAsync();
            return host;
        }

        // --- Вызовы клиентов ---

        internal Task<JoinResult> JoinAsync(string connectionId, JoinRequest request) => Locked(async () =>
        {
            var result = lobby.Join(connectionId, request, Now);
            if (result.Error is not null)
                return result;

            if (game is null)
            {
                await SendLobbyAsync();
                return result;
            }

            // Вернулся в идущую партию: кто он, вся история и новости для остальных.
            int playerId = lobby.FindPlayerId(connectionId)!.Value;
            await Hub.Clients.Client(connectionId).SendAsync(ClientMethods.GameStart, new GameStartInfo(playerId, lobby.ColorByPlayerId(), game.State.Theme));
            await Hub.Clients.Client(connectionId).SendAsync(ClientMethods.Update, MakeUpdate(game.History, playerId, resync: true));
            await SendToAllAsync(ClientMethods.Notice, $"{lobby.FindName(connectionId)} повернувся до гри.");
            await SendUpdateAsync(Array.Empty<GameEvent>());
            return result;
        });

        internal Task<string?> SetReadyAsync(string connectionId, bool ready) => LobbyChange(() => lobby.SetReady(connectionId, ready));

        internal Task<string?> SetColorAsync(string connectionId, int colorIndex) => LobbyChange(() => lobby.SetColor(connectionId, colorIndex));

        internal Task<string?> AddBotAsync(string connectionId) => LobbyChange(() => lobby.AddBot(connectionId));

        internal Task<string?> SetThemeAsync(string connectionId, BoardTheme theme) => LobbyChange(() => lobby.SetTheme(connectionId, theme));

        internal Task<string?> RemoveBotAsync(string connectionId, int seatId) => LobbyChange(() => lobby.RemoveBot(connectionId, seatId));

        internal Task<string?> StartGameAsync(string connectionId) => Locked(async () =>
        {
            var error = lobby.Start(connectionId, out var names);
            if (error is not null)
                return error;

            game = Game.Start(names, theme: lobby.Theme);
            playStarted = Now;
            lastChange = Now;
            Save();
            var colors = lobby.ColorByPlayerId();
            foreach (var (connection, playerId) in lobby.Connections())
                await Hub.Clients.Client(connection).SendAsync(ClientMethods.GameStart, new GameStartInfo(playerId!.Value, colors, game.State.Theme));
            await SendUpdateAsync(game.History);
            return null;
        });

        internal Task<string?> ExecuteAsync(string connectionId, GameAction action) => Locked(async () =>
        {
            if (game is null)
                return "Гра ще не почалася.";
            if (lobby.FindPlayerId(connectionId) is not int playerId)
                return "Ви не берете участі в цій грі.";

            // Действует всегда тот, кто прислал действие, — чужой PlayerId подставить нельзя.
            return await ApplyAsync(action with { PlayerId = playerId });
        });

        // --- Панель администратора (RULES.md, §14) ---

        // Приветствие: новое одноразовое число. Случайность здесь криптографическая, к правилам игры не относится.
        internal Task<AdminChallenge> AdminHelloAsync(string connectionId) => Locked(() =>
        {
            var nonce = RandomNumberGenerator.GetBytes(32);
            admins[connectionId] = new AdminSession(nonce);
            return Task.FromResult(new AdminChallenge(gameId, NetDefaults.GameVersion, nonce));
        });

        // Вход: подпись одноразового числа. Одна попытка на число — при ошибке нужно новое приветствие.
        internal Task<string?> AdminLoginAsync(string connectionId, byte[] signature) => Locked(async () =>
        {
            if (!admins.TryGetValue(connectionId, out var session))
                return "Немає доступу.";
            if (!AdminAuth.Verify(options.AdminPublicKey, AdminAuth.LoginData(gameId, session.Nonce), signature))
            {
                admins.Remove(connectionId);
                return "Немає доступу.";
            }
            session.LoggedIn = true;
            await Hub.Clients.Client(connectionId).SendAsync(ClientMethods.AdminView, MakeAdminView());
            return (string?)null;
        });

        internal Task<string?> AdminExecuteAsync(string connectionId, AdminCommand command) => Locked(async () =>
        {
            if (!admins.TryGetValue(connectionId, out var session) || !session.LoggedIn)
                return "Немає доступу.";
            if (command.Sequence <= session.LastSequence)
                return "Цю команду вже виконано.";
            if (!AdminAuth.Verify(options.AdminPublicKey,
                    AdminAuth.CommandData(gameId, session.Nonce, command.Sequence, command.ActionJson ?? ""), command.Signature))
                return "Немає доступу.";
            session.LastSequence = command.Sequence;

            if (game is null)
                return "Гра ще не почалася.";
            AdminAction? action;
            try
            {
                action = JsonSerializer.Deserialize<GameAction>(command.ActionJson!, Json) as AdminAction;
            }
            catch (JsonException)
            {
                action = null;
            }
            if (action is null)
                return "Невідома дія.";

            // Тихо и без сброса таймера хода: lastChange не трогаем.
            var result = game.ExecuteAdmin(action);
            if (!result.Success)
                return result.Error;
            if (game.State.Phase == TurnPhase.GameOver)
                finalDuration ??= Played(Now);
            Save();
            await SendUpdateAsync(result.Events);
            return null;
        });

        internal Task DisconnectedAsync(string connectionId) => Locked(async () =>
        {
            admins.Remove(connectionId);
            var name = lobby.FindName(connectionId);
            lobby.Leave(connectionId, Now);
            if (name is null)
                return 0;
            if (game is not null)
            {
                int minutes = (int)Math.Round(options.BotTakeover.TotalMinutes);
                await SendToAllAsync(ClientMethods.Notice, $"{name} відключився. Місце чекає на нього {minutes} хв, потім за нього зіграє бот.");
                await SendUpdateAsync(Array.Empty<GameEvent>());
            }
            else
            {
                await SendLobbyAsync();
            }
            return 0;
        });

        // --- Часы: таймер хода, боты, передача места боту ---

        private async Task RunClockAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            try
            {
                while (await timer.WaitForNextTickAsync(stop.Token))
                    await Locked(TickAsync);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<int> TickAsync()
        {
            var now = Now;
            foreach (var name in lobby.Tick(now, options.BotTakeover))
            {
                await SendToAllAsync(ClientMethods.Notice, $"{name} не повернувся — за нього грає бот.");
                await SendUpdateAsync(Array.Empty<GameEvent>());
            }
            if (game is null || game.State.Phase == TurnPhase.GameOver)
                return 0;

            var awaited = game.AwaitedPlayers();

            // Бот ходит с паузой, чтобы люди успевали следить. По одному действию за такт.
            if (now - lastChange >= options.BotDelay)
            {
                foreach (int id in awaited.Where(lobby.IsBotControlled))
                {
                    if (Bot.Choose(game, id) is { } action)
                    {
                        await ApplyAsync(action);
                        return 0;
                    }
                }
            }

            // Время вышло — автодействие за всех, кого ждали (RULES.md, §13).
            if (now - lastChange >= options.TurnTimeout)
            {
                foreach (int id in awaited)
                {
                    // Долг закрывается целиком, остальное — одним действием.
                    for (int step = 0; step < 100; step++)
                    {
                        var action = game.TimeoutAction(id);
                        if (action is null || await ApplyAsync(action) is not null)
                            break;
                        if (action is not (SellBranch or MortgageCompany))
                            break;
                    }
                }
            }
            return 0;
        }

        // --- Общее ---

        private async Task<string?> ApplyAsync(GameAction action)
        {
            var result = game!.Execute(action);
            if (!result.Success)
                return result.Error;
            lastChange = Now;
            if (game.State.Phase == TurnPhase.GameOver)
                finalDuration ??= Played(lastChange);
            Save();
            await SendUpdateAsync(result.Events);
            return null;
        }

        private TimeSpan Played(DateTime now) => finalDuration ?? playedBefore + (now - playStarted);

        private void Save()
        {
            if (options.Saves is null || game?.Seed is not int seed)
                return;
            options.Saves.Write(new SaveFile(gameId, NetDefaults.GameVersion, seed, lobby.SavedSeats(),
                game.Actions.ToList(), Now, game.State.Phase == TurnPhase.GameOver, (int)Played(Now).TotalSeconds, game.State.Theme));
        }

        private IHubContext<GameHub> Hub => hub ?? throw new InvalidOperationException("Хост не запущено.");

        private Task<string?> LobbyChange(Func<string?> change) => Locked(async () =>
        {
            var error = change();
            if (error is null)
                await SendLobbyAsync();
            return error;
        });

        private async Task SendLobbyAsync()
        {
            await SendToAllAsync(ClientMethods.Lobby, lobby.GetState());
            await SendAdminViewAsync();
        }

        // Панели администратора — всё состояние после каждого изменения.
        private async Task SendAdminViewAsync()
        {
            var connections = admins.Where(a => a.Value.LoggedIn).Select(a => a.Key).ToList();
            if (connections.Count > 0)
                await Hub.Clients.Clients(connections).SendAsync(ClientMethods.AdminView, MakeAdminView());
        }

        private AdminView MakeAdminView() => new(gameId, lobby.HostName, lobby.GetState(), game?.State.ToSnapshot(),
            lobby.ColorByPlayerId(), game is null ? null : lobby.SeatStatuses(Now, options.BotTakeover));

        private async Task SendToAllAsync(string method, object message)
        {
            var connections = lobby.Connections().Select(c => c.ConnectionId).ToList();
            await Hub.Clients.Clients(connections).SendAsync(method, message);
        }

        // Каждому — свои доступные действия, остальное общее.
        private async Task SendUpdateAsync(IReadOnlyList<GameEvent> events)
        {
            if (game is null)
                return;
            foreach (var (connection, playerId) in lobby.Connections())
                await Hub.Clients.Client(connection).SendAsync(ClientMethods.Update, MakeUpdate(events, playerId!.Value));
            await SendAdminViewAsync();
        }

        private GameUpdate MakeUpdate(IReadOnlyList<GameEvent> events, int playerId, bool resync = false)
        {
            var now = Now;
            var awaited = game!.AwaitedPlayers();
            var timer = awaited.Count == 0
                ? null
                : new TurnTimer(awaited, Math.Max(0, (int)Math.Ceiling((lastChange + options.TurnTimeout - now).TotalSeconds)));
            return new GameUpdate(events, game.State.ToSnapshot(), game.GetAvailableActions(playerId), timer,
                lobby.SeatStatuses(now, options.BotTakeover), resync,
                new GameDuration((int)Played(now).TotalSeconds, finalDuration is null));
        }

        private DiscoveredGame DescribeForDiscovery() =>
            new(gameId, lobby.HostName ?? "?", NetDefaults.GameVersion, Port, lobby.PlayerCount, GameRules.MaxPlayers, lobby.IsStarted,
                Theme: lobby.Theme);

        private async Task<T> Locked<T>(Func<Task<T>> action)
        {
            await gate.WaitAsync();
            try
            {
                return await action();
            }
            finally
            {
                discoveryInfo = DescribeForDiscovery();
                gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            discovery?.Dispose();
            if (app is not null)
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }
    }

    // Точка входа SignalR: переадресует вызовы клиентов хосту.
    internal sealed class GameHub : Hub
    {
        private readonly GameHost host;

        public GameHub(GameHost host)
        {
            this.host = host;
        }

        public Task<JoinResult> Join(JoinRequest request) => host.JoinAsync(Context.ConnectionId, request);

        public Task<string?> SetReady(bool ready) => host.SetReadyAsync(Context.ConnectionId, ready);

        public Task<string?> SetColor(int colorIndex) => host.SetColorAsync(Context.ConnectionId, colorIndex);

        public Task<string?> AddBot() => host.AddBotAsync(Context.ConnectionId);

        public Task<string?> SetTheme(BoardTheme theme) => host.SetThemeAsync(Context.ConnectionId, theme);

        public Task<string?> RemoveBot(int seatId) => host.RemoveBotAsync(Context.ConnectionId, seatId);

        public Task<string?> StartGame() => host.StartGameAsync(Context.ConnectionId);

        public Task<string?> SendAction(ActionRequest request) => host.ExecuteAsync(Context.ConnectionId, request.Action);

        public Task<AdminChallenge> AdminHello() => host.AdminHelloAsync(Context.ConnectionId);

        public Task<string?> AdminLogin(byte[] signature) => host.AdminLoginAsync(Context.ConnectionId, signature);

        public Task<string?> AdminExecute(AdminCommand command) => host.AdminExecuteAsync(Context.ConnectionId, command);

        public override Task OnDisconnectedAsync(Exception? exception) => host.DisconnectedAsync(Context.ConnectionId);
    }

    internal sealed class AdminSession
    {
        public byte[] Nonce { get; }
        public bool LoggedIn { get; set; }
        public long LastSequence { get; set; }

        public AdminSession(byte[] nonce)
        {
            Nonce = nonce;
        }
    }
}
