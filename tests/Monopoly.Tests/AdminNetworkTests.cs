using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Tests
{
    // Панель администратора по сети (RULES.md, §14): доступ только по подписи ключом автора.
    public partial class NetworkTests
    {
        // Хост с тестовым ключом администратора, хост-игрок и гость в начатой партии.
        private static async Task<(GameHost Host, Inbox HostPlayer, Inbox Guest, int Port)> StartAdminGameAsync(ECDsa adminKey)
        {
            int port = FreePort();
            var host = await GameHost.StartAsync(new GameHostOptions
            {
                Port = port, EnableDiscovery = false, AdminPublicKey = AdminAuth.ExportPublicKey(adminKey),
            });
            var hostPlayer = new Inbox($"127.0.0.1:{port}");
            var guest = new Inbox($"127.0.0.1:{port}");
            await StartTwoPlayerGameAsync(host, hostPlayer, guest);
            return (host, hostPlayer, guest, port);
        }

        [Fact]
        public async Task Admin_WithKey_SeesGameAndChangesBalanceSilently()
        {
            using var key = AdminAuth.CreateKey();
            var (host, hostPlayer, guest, port) = await StartAdminGameAsync(key);
            await using var _h = host;
            await using var _p = hostPlayer;
            await using var _g = guest;

            var views = Channel.CreateUnbounded<AdminView>();
            await using var admin = new AdminClient($"127.0.0.1:{port}", key);
            admin.ViewChanged += v => views.Writer.TryWrite(v);
            Assert.Null(await admin.ConnectAsync());

            var view = await views.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            Assert.Equal("Хост", view.HostName);
            Assert.Equal(2, view.Snapshot!.Players.Count);
            // Панель — не игрок: мест в лобби по-прежнему два.
            Assert.Equal(2, view.Lobby.Seats.Count);

            Assert.Null(await admin.ExecuteAsync(new AdminSetBalance(1, 9_000_000)));

            var update = await guest.WaitUpdateAsync(u => u.Snapshot.FindPlayer(1)!.Balance == 9_000_000);
            Assert.Empty(update.Events);
            var changed = await views.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            Assert.Equal(9_000_000, changed.Snapshot!.FindPlayer(1)!.Balance);

            // Ошибки правил доходят до панели.
            Assert.Equal("Немає такого гравця.", await admin.ExecuteAsync(new AdminSetBalance(7, 100)));
        }

        [Fact]
        public async Task Admin_WrongKey_NoAccess()
        {
            using var ownerKey = AdminAuth.CreateKey();
            using var otherKey = AdminAuth.CreateKey();
            var (host, hostPlayer, guest, port) = await StartAdminGameAsync(ownerKey);
            await using var _h = host;
            await using var _p = hostPlayer;
            await using var _g = guest;

            await using var intruder = new AdminClient($"127.0.0.1:{port}", otherKey);

            Assert.Equal("Немає доступу.", await intruder.ConnectAsync());
        }

        [Fact]
        public async Task Player_CannotSendAdminAction()
        {
            using var key = AdminAuth.CreateKey();
            var (host, hostPlayer, guest, _) = await StartAdminGameAsync(key);
            await using var _h = host;
            await using var _p = hostPlayer;
            await using var _g = guest;

            Assert.Equal("Це дія адміністратора.", await guest.Client.SendActionAsync(new AdminSetBalance(1, 9_000_000)));
        }

        [Fact]
        public async Task Admin_RepeatedOrForgedCommand_Rejected()
        {
            using var key = AdminAuth.CreateKey();
            var (host, hostPlayer, guest, port) = await StartAdminGameAsync(key);
            await using var _h = host;
            await using var _p = hostPlayer;
            await using var _g = guest;

            var json = ProtocolJson.CreateOptions();
            await using var raw = new HubConnectionBuilder()
                .WithUrl($"http://127.0.0.1:{port}{NetDefaults.HubPath}")
                .AddJsonProtocol(o => ProtocolJson.Configure(o.PayloadSerializerOptions))
                .Build();
            await raw.StartAsync();

            // Без входа команды не принимаются.
            string balance = JsonSerializer.Serialize<GameAction>(new AdminSetBalance(1, 5_000_000), json);
            Assert.Equal("Немає доступу.", await raw.InvokeAsync<string?>("AdminExecute", new AdminCommand(1, balance, new byte[64])));

            var hello = await raw.InvokeAsync<AdminChallenge>("AdminHello");
            Assert.Null(await raw.InvokeAsync<string?>("AdminLogin", AdminAuth.Sign(key, AdminAuth.LoginData(hello.GameId, hello.Nonce))));

            var signed = new AdminCommand(1, balance, AdminAuth.Sign(key, AdminAuth.CommandData(hello.GameId, hello.Nonce, 1, balance)));
            Assert.Null(await raw.InvokeAsync<string?>("AdminExecute", signed));
            // Та же команда второй раз — отказ.
            Assert.Equal("Цю команду вже виконано.", await raw.InvokeAsync<string?>("AdminExecute", signed));

            // Подпись от одной команды к другой не подходит.
            string forged = JsonSerializer.Serialize<GameAction>(new AdminSetBalance(1, 99_000_000), json);
            Assert.Equal("Немає доступу.", await raw.InvokeAsync<string?>("AdminExecute", signed with { Sequence = 2, ActionJson = forged }));

            // Обычное действие игрока через панель не проходит.
            string roll = JsonSerializer.Serialize<GameAction>(new RollDice(0), json);
            var rollCommand = new AdminCommand(3, roll, AdminAuth.Sign(key, AdminAuth.CommandData(hello.GameId, hello.Nonce, 3, roll)));
            Assert.Equal("Невідома дія.", await raw.InvokeAsync<string?>("AdminExecute", rollCommand));

            await guest.WaitUpdateAsync(u => u.Snapshot.FindPlayer(1)!.Balance == 5_000_000);
        }
    }
}
