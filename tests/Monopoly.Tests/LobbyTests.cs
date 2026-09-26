using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Tests
{
    public class LobbyTests
    {
        private const string Token = "host-token";
        private const string Version = "1.2.3";
        private static readonly DateTime T0 = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

        private static Lobby CreateLobbyWithHost()
        {
            var lobby = new Lobby(Token, Version);
            lobby.Join("host", new JoinRequest("Хост", Version, Token), T0);
            return lobby;
        }

        private static JoinResult Join(Lobby lobby, string connection, string name) =>
            lobby.Join(connection, new JoinRequest(name, Version), T0);

        [Fact]
        public void Join_WithOtherVersion_IsRejected()
        {
            var lobby = CreateLobbyWithHost();

            var result = lobby.Join("c1", new JoinRequest("Аня", "1.2.4"), T0);

            Assert.Equal("Версії гри не збігаються: у хоста 1.2.3, у вас 1.2.4. Потрібна однакова версія в усіх.", result.Error);
            Assert.Equal(1, lobby.PlayerCount);
        }

        [Fact]
        public void Lobby_HoldsUpToFivePlayers()
        {
            var lobby = CreateLobbyWithHost();
            for (int i = 1; i < GameRules.MaxPlayers; i++)
                Assert.Null(Join(lobby, $"c{i}", $"Игрок {i}").Error);

            var extra = Join(lobby, "c-extra", "Лишний");

            Assert.Equal(5, lobby.PlayerCount);
            Assert.Equal("Усі 5 місць зайнято.", extra.Error);
        }

        [Theory]
        [InlineData("", "Введіть ім'я.")]
        [InlineData("   ", "Введіть ім'я.")]
        [InlineData("хост", "Ім'я «хост» уже зайняте.")]
        [InlineData("Очень-очень длинное имя", "Ім'я задовге: не більше 20 символів.")]
        public void Join_WithBadName_IsRejected(string name, string error)
        {
            var lobby = CreateLobbyWithHost();

            Assert.Equal(error, Join(lobby, "c1", name).Error);
        }

        [Fact]
        public void Join_OnlyTokenMakesHost()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");

            var seats = lobby.GetState().Seats;

            Assert.True(seats[0].IsHost);
            Assert.False(seats[1].IsHost);
            Assert.Equal("Хост уже в лобі.", lobby.Join("c2", new JoinRequest("Второй", Version, Token), T0).Error);
        }

        [Fact]
        public void Join_GetsFirstFreeColor()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            lobby.SetColor("host", 3);

            Join(lobby, "c2", "Богдан");

            Assert.Equal(new[] { 3, 1, 0 }, lobby.GetState().Seats.Select(s => s.ColorIndex));
        }

        [Fact]
        public void SetColor_TakenOrUnknown_IsRejected()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");

            Assert.Equal("Цей колір уже зайнятий.", lobby.SetColor("c1", 0));
            Assert.Equal("Такого кольору немає.", lobby.SetColor("c1", Lobby.ColorCount));
            Assert.Null(lobby.SetColor("c1", 4));
        }

        [Fact]
        public void Start_NeedsTwoPlayers()
        {
            var lobby = CreateLobbyWithHost();

            Assert.Equal("Потрібно хоча б 2 гравці.", lobby.GetState().StartBlockedReason);
            Assert.Equal("Потрібно хоча б 2 гравці.", lobby.Start("host", out _));
        }

        [Fact]
        public void Start_NeedsEveryoneReady()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            Join(lobby, "c2", "Богдан");
            lobby.SetReady("c1", true);

            Assert.Equal("Не готові: Богдан.", lobby.Start("host", out _));

            lobby.SetReady("c2", true);
            Assert.Null(lobby.GetState().StartBlockedReason);
        }

        [Fact]
        public void Start_OnlyByHost()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            lobby.SetReady("c1", true);

            Assert.Equal("Почати гру може лише хост.", lobby.Start("c1", out _));
        }

        [Fact]
        public void Start_GivesPlayerIdsInSeatOrder()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            lobby.SetReady("c1", true);
            lobby.SetColor("c1", 4);

            var error = lobby.Start("host", out var names);

            Assert.Null(error);
            Assert.True(lobby.IsStarted);
            Assert.Equal(new[] { "Хост", "Аня" }, names);
            Assert.Equal(1, lobby.FindPlayerId("c1"));
            Assert.Equal(4, lobby.ColorByPlayerId()[1]);
            Assert.Equal("Гра вже почалася. Повернутися можна лише під своїм ім'ям із цієї партії.", Join(lobby, "c2", "Опоздавший").Error);
        }

        [Fact]
        public void Leave_BeforeStart_FreesSeat()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");

            lobby.Leave("c1", T0);

            Assert.Equal(1, lobby.PlayerCount);
            Assert.Null(Join(lobby, "c2", "Аня").Error);
        }

        [Fact]
        public void Leave_AfterStart_KeepsSeatButStopsMessages()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            lobby.SetReady("c1", true);
            lobby.Start("host", out _);

            lobby.Leave("c1", T0);

            Assert.Equal(2, lobby.PlayerCount);
            Assert.Equal(new[] { "host" }, lobby.Connections().Select(c => c.ConnectionId));
        }

        // --- Этап 5: возвращение, боты, сохранение ---

        private static Lobby StartedWithAnya()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            lobby.SetReady("c1", true);
            lobby.Start("host", out _);
            return lobby;
        }

        [Fact]
        public void Rejoin_ByName_ReturnsToSameSeat()
        {
            var lobby = StartedWithAnya();
            lobby.Leave("c1", T0);

            var result = lobby.Join("c9", new JoinRequest("аня", Version), T0.AddSeconds(30));

            Assert.Null(result.Error);
            Assert.Equal(1, lobby.FindPlayerId("c9"));
            Assert.Equal(SeatConnection.Online, lobby.SeatStatuses(T0, TimeSpan.FromMinutes(2))[1].Connection);
        }

        [Fact]
        public void Rejoin_WhileOnline_IsRejected()
        {
            var lobby = StartedWithAnya();

            Assert.Equal("Гравець «Аня» уже в грі.", Join(lobby, "c9", "Аня").Error);
        }

        [Fact]
        public void Offline_TwoMinutes_BotTakesOver_UntilReturn()
        {
            var lobby = StartedWithAnya();
            var takeover = TimeSpan.FromMinutes(2);
            lobby.Leave("c1", T0);

            Assert.Empty(lobby.Tick(T0.AddSeconds(119), takeover));
            Assert.Equal(new SeatStatus(1, SeatConnection.Offline, 1), lobby.SeatStatuses(T0.AddSeconds(119), takeover)[1]);
            Assert.False(lobby.IsBotControlled(1));

            Assert.Equal(new[] { "Аня" }, lobby.Tick(T0.AddSeconds(120), takeover));
            Assert.True(lobby.IsBotControlled(1));
            Assert.Equal(SeatConnection.Bot, lobby.SeatStatuses(T0.AddSeconds(120), takeover)[1].Connection);

            Join(lobby, "c9", "Аня");
            Assert.False(lobby.IsBotControlled(1));
        }

        [Fact]
        public void Bots_AddedByHost_AreReady_AndRemovable()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");

            Assert.Equal("Це може лише хост.", lobby.AddBot("c1"));
            Assert.Null(lobby.AddBot("host"));
            Assert.Null(lobby.AddBot("host"));

            var bots = lobby.GetState().Seats.Where(s => s.IsBot).ToList();
            Assert.Equal(new[] { "Бот 1", "Бот 2" }, bots.Select(b => b.Name));
            Assert.All(bots, b => Assert.True(b.IsReady));

            Assert.Null(lobby.RemoveBot("host", bots[0].SeatId));
            Assert.Equal("Такого бота немає.", lobby.RemoveBot("host", 999));
            Assert.Equal(3, lobby.PlayerCount);
        }

        [Fact]
        public void BotSeat_IsBotControlled_AndCannotBeTakenByName()
        {
            var lobby = CreateLobbyWithHost();
            lobby.AddBot("host");
            lobby.Start("host", out _);

            Assert.True(lobby.IsBotControlled(1));
            Assert.Equal("Гра вже почалася. Повернутися можна лише під своїм ім'ям із цієї партії.", Join(lobby, "c1", "Бот 1").Error);
        }

        [Fact]
        public void Restore_FromSave_WaitsForEveryone()
        {
            var lobby = new Lobby(Token, Version);
            lobby.Restore(new[]
            {
                new SavedSeat("Хост", 3, true, false),
                new SavedSeat("Аня", 1, false, false),
                new SavedSeat("Бот 1", 0, false, true),
            }, T0);

            Assert.True(lobby.IsStarted);
            Assert.Equal("Хост", lobby.HostName);
            Assert.Equal(new[] { SeatConnection.Offline, SeatConnection.Offline, SeatConnection.Bot },
                lobby.SeatStatuses(T0, TimeSpan.FromMinutes(2)).Select(s => s.Connection));

            Assert.Null(lobby.Join("h", new JoinRequest("Хост", Version, Token), T0).Error);
            Assert.Equal(0, lobby.FindPlayerId("h"));
            Assert.Equal(new SavedSeat("Аня", 1, false, false), lobby.SavedSeats()[1]);
        }
    }
}
