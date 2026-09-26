using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Tests
{
    public class LobbyTests
    {
        private const string Token = "host-token";
        private const string Version = "1.2.3";

        private static Lobby CreateLobbyWithHost()
        {
            var lobby = new Lobby(Token, Version);
            lobby.Join("host", new JoinRequest("Хост", Version, Token));
            return lobby;
        }

        private static JoinResult Join(Lobby lobby, string connection, string name) =>
            lobby.Join(connection, new JoinRequest(name, Version));

        [Fact]
        public void Join_WithOtherVersion_IsRejected()
        {
            var lobby = CreateLobbyWithHost();

            var result = lobby.Join("c1", new JoinRequest("Аня", "1.2.4"));

            Assert.Equal("Версии игры не совпадают: у хоста 1.2.3, у вас 1.2.4. Нужна одна версия у всех.", result.Error);
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
            Assert.Equal("Все 5 мест заняты.", extra.Error);
        }

        [Theory]
        [InlineData("", "Введите имя.")]
        [InlineData("   ", "Введите имя.")]
        [InlineData("хост", "Имя «хост» уже занято.")]
        [InlineData("Очень-очень длинное имя", "Имя слишком длинное: не больше 20 символов.")]
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
            Assert.Equal("Хост уже в лобби.", lobby.Join("c2", new JoinRequest("Второй", Version, Token)).Error);
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

            Assert.Equal("Этот цвет уже занят.", lobby.SetColor("c1", 0));
            Assert.Equal("Такого цвета нет.", lobby.SetColor("c1", Lobby.ColorCount));
            Assert.Null(lobby.SetColor("c1", 4));
        }

        [Fact]
        public void Start_NeedsTwoPlayers()
        {
            var lobby = CreateLobbyWithHost();

            Assert.Equal("Нужно хотя бы 2 игрока.", lobby.GetState().StartBlockedReason);
            Assert.Equal("Нужно хотя бы 2 игрока.", lobby.Start("host", out _));
        }

        [Fact]
        public void Start_NeedsEveryoneReady()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            Join(lobby, "c2", "Богдан");
            lobby.SetReady("c1", true);

            Assert.Equal("Не готовы: Богдан.", lobby.Start("host", out _));

            lobby.SetReady("c2", true);
            Assert.Null(lobby.GetState().StartBlockedReason);
        }

        [Fact]
        public void Start_OnlyByHost()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");
            lobby.SetReady("c1", true);

            Assert.Equal("Начать игру может только хост.", lobby.Start("c1", out _));
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
            Assert.Equal("Игра уже началась.", Join(lobby, "c2", "Опоздавший").Error);
        }

        [Fact]
        public void Leave_BeforeStart_FreesSeat()
        {
            var lobby = CreateLobbyWithHost();
            Join(lobby, "c1", "Аня");

            lobby.Leave("c1");

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

            lobby.Leave("c1");

            Assert.Equal(2, lobby.PlayerCount);
            Assert.Equal(new[] { "host" }, lobby.Connections().Select(c => c.ConnectionId));
        }
    }
}
