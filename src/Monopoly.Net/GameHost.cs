using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monopoly.Core;

namespace Monopoly.Net
{
    // Сервер игры внутри приложения того, кто создал игру. Только он выполняет правила.
    public sealed class GameHost : IAsyncDisposable
    {
        // Все обращения к лобби и игре — по очереди, вместе с рассылкой: так клиенты получают события по порядку.
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Lobby lobby;
        private readonly Guid gameId = Guid.NewGuid();
        private WebApplication? app;
        private IHubContext<GameHub>? hub;
        private DiscoveryResponder? discovery;
        private Game? game;
        // Описание для ответа на поиск; обновляется после каждой операции, читается из потока UDP.
        private volatile DiscoveredGame discoveryInfo;

        public int Port { get; }
        // Передаётся собственному клиенту хоста, чтобы лобби узнало хоста.
        public string HostToken { get; } = Guid.NewGuid().ToString("N");

        private GameHost(int port)
        {
            Port = port;
            lobby = new Lobby(HostToken, NetDefaults.GameVersion);
            discoveryInfo = DescribeForDiscovery();
        }

        // Запускает сервер на всех адресах компьютера. Если порт занят — исключение IOException.
        public static async Task<GameHost> StartAsync(int port = NetDefaults.Port, bool enableDiscovery = true)
        {
            var host = new GameHost(port);

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Any, port));
            builder.Services.AddSingleton(host);
            builder.Services.AddSignalR()
                .AddJsonProtocol(options => ProtocolJson.Configure(options.PayloadSerializerOptions));

            var app = builder.Build();
            app.MapHub<GameHub>(NetDefaults.HubPath);
            await app.StartAsync();

            host.app = app;
            host.hub = app.Services.GetRequiredService<IHubContext<GameHub>>();
            if (enableDiscovery)
                host.discovery = DiscoveryResponder.TryStart(port, () => host.discoveryInfo);
            return host;
        }

        internal Task<JoinResult> JoinAsync(string connectionId, JoinRequest request) => Locked(async () =>
        {
            var result = lobby.Join(connectionId, request);
            if (result.Error is null)
                await SendLobbyAsync();
            return result;
        });

        internal Task<string?> SetReadyAsync(string connectionId, bool ready) => Locked(async () =>
        {
            var error = lobby.SetReady(connectionId, ready);
            if (error is null)
                await SendLobbyAsync();
            return error;
        });

        internal Task<string?> SetColorAsync(string connectionId, int colorIndex) => Locked(async () =>
        {
            var error = lobby.SetColor(connectionId, colorIndex);
            if (error is null)
                await SendLobbyAsync();
            return error;
        });

        internal Task<string?> StartGameAsync(string connectionId) => Locked(async () =>
        {
            var error = lobby.Start(connectionId, out var names);
            if (error is not null)
                return error;

            game = Game.Start(names);
            var colors = lobby.ColorByPlayerId();
            foreach (var (connection, playerId) in lobby.Connections())
                await Hub.Clients.Client(connection).SendAsync(ClientMethods.GameStart, new GameStartInfo(playerId!.Value, colors));
            await SendUpdateAsync(game.History);
            return null;
        });

        internal Task<string?> ExecuteAsync(string connectionId, GameAction action) => Locked(async () =>
        {
            if (game is null)
                return "Игра ещё не началась.";
            if (lobby.FindPlayerId(connectionId) is not int playerId)
                return "Вы не участвуете в этой игре.";

            // Действует всегда тот, кто прислал действие, — чужой PlayerId подставить нельзя.
            var result = game.Execute(action with { PlayerId = playerId });
            if (!result.Success)
                return result.Error;
            await SendUpdateAsync(result.Events);
            return null;
        });

        internal Task DisconnectedAsync(string connectionId) => Locked(async () =>
        {
            var name = lobby.FindName(connectionId);
            lobby.Leave(connectionId);
            if (name is null)
                return 0;
            if (lobby.IsStarted)
                await SendToAllAsync(ClientMethods.Notice, $"{name} отключился.");
            else
                await SendLobbyAsync();
            return 0;
        });

        private IHubContext<GameHub> Hub => hub ?? throw new InvalidOperationException("Хост не запущен.");

        private Task SendLobbyAsync() => SendToAllAsync(ClientMethods.Lobby, lobby.GetState());

        private async Task SendToAllAsync(string method, object message)
        {
            var connections = lobby.Connections().Select(c => c.ConnectionId).ToList();
            await Hub.Clients.Clients(connections).SendAsync(method, message);
        }

        // Каждому — свои доступные действия, остальное общее.
        private async Task SendUpdateAsync(IReadOnlyList<GameEvent> events)
        {
            var snapshot = game!.State.ToSnapshot();
            foreach (var (connection, playerId) in lobby.Connections())
            {
                var update = new GameUpdate(events, snapshot, game.GetAvailableActions(playerId!.Value));
                await Hub.Clients.Client(connection).SendAsync(ClientMethods.Update, update);
            }
        }

        private DiscoveredGame DescribeForDiscovery()
        {
            return new DiscoveredGame(gameId, lobby.HostName ?? "?", NetDefaults.GameVersion, Port,
                lobby.PlayerCount, GameRules.MaxPlayers, lobby.IsStarted);
        }

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

        public Task<string?> StartGame() => host.StartGameAsync(Context.ConnectionId);

        public Task<string?> SendAction(ActionRequest request) => host.ExecuteAsync(Context.ConnectionId, request.Action);

        public override Task OnDisconnectedAsync(Exception? exception) => host.DisconnectedAsync(Context.ConnectionId);
    }
}
