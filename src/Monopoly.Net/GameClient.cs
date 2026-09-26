using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Monopoly.Core;

namespace Monopoly.Net
{
    // Подключение игрока к хосту. У хоста его собственный клиент подключается к 127.0.0.1.
    // События приходят из потоков сети — интерфейс сам переносит их в свой поток.
    public sealed class GameClient : IAsyncDisposable
    {
        private readonly HubConnection connection;
        private bool disposing;

        public string Address { get; }
        public int SeatId { get; private set; } = -1;

        public event Action<LobbyState>? LobbyChanged;
        public event Action<GameStartInfo>? GameStarted;
        public event Action<GameUpdate>? GameUpdated;
        public event Action<string>? Notice;
        // Связь с хостом потеряна (не по нашей воле).
        public event Action<string>? ConnectionLost;

        // Подписываться на события нужно до ConnectAsync: первые сообщения приходят сразу после входа.
        public GameClient(string address)
        {
            Address = address.Trim();
            var (host, port) = ParseAddress(Address);
            connection = new HubConnectionBuilder()
                .WithUrl($"http://{host}:{port}{NetDefaults.HubPath}")
                .AddJsonProtocol(options => ProtocolJson.Configure(options.PayloadSerializerOptions))
                .Build();

            connection.On<LobbyState>(ClientMethods.Lobby, state => LobbyChanged?.Invoke(state));
            connection.On<GameStartInfo>(ClientMethods.GameStart, info => GameStarted?.Invoke(info));
            connection.On<GameUpdate>(ClientMethods.Update, update => GameUpdated?.Invoke(update));
            connection.On<string>(ClientMethods.Notice, text => Notice?.Invoke(text));
            connection.Closed += error =>
            {
                if (!disposing)
                    ConnectionLost?.Invoke("Связь с хостом потеряна: хост вышел из игры или пропала сеть.");
                return Task.CompletedTask;
            };
        }

        // Подключается и входит в лобби. Возвращает текст ошибки или null.
        public async Task<string?> ConnectAsync(string name, string? hostToken = null, CancellationToken cancellationToken = default)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                await connection.StartAsync(timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TimeoutException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;
                return $"Не удалось подключиться к {Address}. Проверьте адрес, что игра создана, и брандмауэр на компьютере хоста.";
            }

            JoinResult result;
            try
            {
                result = await connection.InvokeAsync<JoinResult>("Join",
                    new JoinRequest(name, NetDefaults.GameVersion, hostToken), cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HubException)
            {
                return "Связь с хостом оборвалась при входе в лобби.";
            }
            if (result.Error is not null)
            {
                disposing = true;
                await connection.StopAsync(CancellationToken.None);
                return result.Error;
            }
            SeatId = result.SeatId;
            return null;
        }

        public Task<string?> SetReadyAsync(bool ready) => InvokeAsync("SetReady", ready);

        public Task<string?> SetColorAsync(int colorIndex) => InvokeAsync("SetColor", colorIndex);

        public Task<string?> StartGameAsync() => InvokeAsync("StartGame");

        public Task<string?> SendActionAsync(GameAction action) => InvokeAsync("SendAction", new ActionRequest(action));

        private async Task<string?> InvokeAsync(string method, params object?[] args)
        {
            try
            {
                return await connection.InvokeCoreAsync<string?>(method, args);
            }
            catch (InvalidOperationException)
            {
                return "Нет связи с хостом.";
            }
            catch (HubException)
            {
                return "Хост не смог выполнить запрос.";
            }
        }

        // «адрес» или «адрес:порт»; без порта — 7777.
        public static (string Host, int Port) ParseAddress(string address)
        {
            int colon = address.LastIndexOf(':');
            if (colon > 0 && address.IndexOf(':') == colon && int.TryParse(address[(colon + 1)..], out int port))
                return (address[..colon], port);
            return (address, NetDefaults.Port);
        }

        public async ValueTask DisposeAsync()
        {
            disposing = true;
            await connection.DisposeAsync();
        }
    }
}
