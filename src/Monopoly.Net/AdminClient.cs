using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Monopoly.Core;

namespace Monopoly.Net
{
    // Подключение панели администратора к хосту (RULES.md, §14). В лобби панель не входит и игроком не считается.
    // Вход — подпись одноразового числа хоста, каждая команда — тоже с подписью. Ключ — только у автора игры.
    public sealed class AdminClient : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions Json = ProtocolJson.CreateOptions();

        private readonly HubConnection connection;
        private readonly ECDsa key;
        private readonly SemaphoreSlim gate = new(1, 1);
        private AdminChallenge? challenge;
        private long sequence;
        private bool disposing;

        public string Address { get; }

        public event Action<AdminView>? ViewChanged;
        // Связь потеряна окончательно (не по нашей воле).
        public event Action<string>? ConnectionLost;

        public AdminClient(string address, ECDsa key)
        {
            Address = address.Trim();
            this.key = key;
            var (host, port) = GameClient.ParseAddress(Address);
            connection = new HubConnectionBuilder()
                .WithUrl($"http://{host}:{port}{NetDefaults.HubPath}")
                .WithAutomaticReconnect()
                .AddJsonProtocol(options => ProtocolJson.Configure(options.PayloadSerializerOptions))
                .Build();

            connection.On<AdminView>(ClientMethods.AdminView, view => ViewChanged?.Invoke(view));
            // Новое соединение хост не знает — входим заново.
            connection.Reconnected += async _ =>
            {
                var error = await LoginAsync(CancellationToken.None);
                if (error is not null)
                {
                    disposing = true;
                    ConnectionLost?.Invoke(error);
                    await connection.StopAsync();
                }
            };
            connection.Closed += _ =>
            {
                if (!disposing)
                    ConnectionLost?.Invoke("Зв'язок із хостом втрачено: партію закрито або зникла мережа.");
                return Task.CompletedTask;
            };
        }

        public async Task<string?> ConnectAsync(CancellationToken cancellationToken = default)
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
                return $"Не вдалося підключитися до {Address}.";
            }

            var error = await LoginAsync(cancellationToken);
            if (error is not null)
            {
                disposing = true;
                await connection.StopAsync(CancellationToken.None);
            }
            return error;
        }

        private async Task<string?> LoginAsync(CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var hello = await connection.InvokeAsync<AdminChallenge>("AdminHello", cancellationToken);
                if (hello.Version != NetDefaults.GameVersion)
                    return $"Версії не збігаються: у хоста {hello.Version}, у панелі {NetDefaults.GameVersion}.";
                var signature = AdminAuth.Sign(key, AdminAuth.LoginData(hello.GameId, hello.Nonce));
                var error = await connection.InvokeAsync<string?>("AdminLogin", signature, cancellationToken);
                if (error is not null)
                    return error;
                challenge = hello;
                sequence = 0;
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or HubException)
            {
                return "Хост не підтримує панель адміністратора (стара версія гри?).";
            }
            finally
            {
                gate.Release();
            }
        }

        // Выполняет действие администратора. Возвращает текст ошибки или null.
        public async Task<string?> ExecuteAsync(AdminAction action)
        {
            await gate.WaitAsync();
            try
            {
                if (challenge is null)
                    return "Панель ще не увійшла.";
                string json = JsonSerializer.Serialize<GameAction>(action, Json);
                long number = ++sequence;
                var signature = AdminAuth.Sign(key, AdminAuth.CommandData(challenge.GameId, challenge.Nonce, number, json));
                return await connection.InvokeAsync<string?>("AdminExecute", new AdminCommand(number, json, signature));
            }
            catch (Exception ex) when (ex is InvalidOperationException or HubException)
            {
                return "Немає зв'язку з хостом.";
            }
            finally
            {
                gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            disposing = true;
            await connection.DisposeAsync();
        }
    }
}
