using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Monopoly.Core;

namespace Monopoly.Net
{
    // Подключение игрока к хосту. У хоста его собственный клиент подключается к 127.0.0.1.
    // При обрыве связи клиент сам переподключается и возвращается на своё место.
    // События приходят из потоков сети — интерфейс сам переносит их в свой поток.
    public sealed class GameClient : IAsyncDisposable
    {
        private readonly HubConnection connection;
        private string name = "";
        private string? hostToken;
        private bool disposing;

        public string Address { get; }
        public int SeatId { get; private set; } = -1;

        public event Action<LobbyState>? LobbyChanged;
        public event Action<GameStartInfo>? GameStarted;
        public event Action<GameUpdate>? GameUpdated;
        public event Action<string>? Notice;
        // Связь пропала, клиент пробует вернуться.
        public event Action? Reconnecting;
        public event Action? Reconnected;
        // Связь с хостом потеряна окончательно (не по нашей воле).
        public event Action<string>? ConnectionLost;

        // Подписываться на события нужно до ConnectAsync: первые сообщения приходят сразу после входа.
        public GameClient(string address, TimeSpan? reconnectFor = null)
        {
            Address = address.Trim();
            var (host, port) = ParseAddress(Address);
            connection = new HubConnectionBuilder()
                .WithUrl($"http://{host}:{port}{NetDefaults.HubPath}")
                .WithAutomaticReconnect(new RetryPolicy(reconnectFor ?? NetDefaults.BotTakeover))
                .AddJsonProtocol(options => ProtocolJson.Configure(options.PayloadSerializerOptions))
                .Build();

            connection.On<LobbyState>(ClientMethods.Lobby, state => LobbyChanged?.Invoke(state));
            connection.On<GameStartInfo>(ClientMethods.GameStart, info => GameStarted?.Invoke(info));
            connection.On<GameUpdate>(ClientMethods.Update, update => GameUpdated?.Invoke(update));
            connection.On<string>(ClientMethods.Notice, text => Notice?.Invoke(text));
            connection.Reconnecting += _ =>
            {
                Reconnecting?.Invoke();
                return Task.CompletedTask;
            };
            // Новое соединение хост не знает — входим заново под тем же именем.
            connection.Reconnected += async _ =>
            {
                var error = await JoinAsync(CancellationToken.None);
                if (error is null)
                {
                    Reconnected?.Invoke();
                    return;
                }
                disposing = true;
                ConnectionLost?.Invoke(error);
                await connection.StopAsync();
            };
            connection.Closed += _ =>
            {
                if (!disposing)
                    ConnectionLost?.Invoke("Зв'язок із хостом втрачено: хост вийшов із гри або зникла мережа.");
                return Task.CompletedTask;
            };
        }

        // Подключается и входит в лобби (или возвращается в идущую партию). Возвращает текст ошибки или null.
        public async Task<string?> ConnectAsync(string playerName, string? token = null, CancellationToken cancellationToken = default)
        {
            name = playerName;
            hostToken = token;
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
                return $"Не вдалося підключитися до {Address}. Перевірте адресу, чи створено гру, і брандмауер на комп'ютері хоста.";
            }

            var error = await JoinAsync(cancellationToken);
            if (error is not null)
            {
                disposing = true;
                await connection.StopAsync(CancellationToken.None);
            }
            return error;
        }

        private async Task<string?> JoinAsync(CancellationToken cancellationToken)
        {
            JoinResult result;
            try
            {
                result = await connection.InvokeAsync<JoinResult>("Join",
                    new JoinRequest(name, NetDefaults.GameVersion, hostToken), cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HubException)
            {
                return "Зв'язок із хостом обірвався під час входу.";
            }
            if (result.Error is null)
                SeatId = result.SeatId;
            return result.Error;
        }

        public Task<string?> SetReadyAsync(bool ready) => InvokeAsync("SetReady", ready);

        public Task<string?> SetColorAsync(int colorIndex) => InvokeAsync("SetColor", colorIndex);

        public Task<string?> AddBotAsync() => InvokeAsync("AddBot");

        // Только хост и только до старта (RULES.md, §15).
        public Task<string?> SetThemeAsync(BoardTheme theme) => InvokeAsync("SetTheme", theme);

        public Task<string?> RemoveBotAsync(int seatId) => InvokeAsync("RemoveBot", seatId);

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
                return "Немає зв'язку з хостом.";
            }
            catch (HubException)
            {
                return "Хост не зміг виконати запит.";
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

        // Переподключение: попытка каждые 2 секунды, пока место ждёт игрока.
        private sealed class RetryPolicy : IRetryPolicy
        {
            private readonly TimeSpan limit;

            public RetryPolicy(TimeSpan limit)
            {
                this.limit = limit;
            }

            public TimeSpan? NextRetryDelay(RetryContext context) =>
                context.ElapsedTime < limit ? TimeSpan.FromSeconds(context.PreviousRetryCount == 0 ? 0 : 2) : null;
        }
    }
}
