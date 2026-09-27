using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Тематики доски (RULES.md, §15): отличаются только названия.
    public class ThemeTests
    {
        [Fact]
        public void AllThemes_SameLayoutAndPrices()
        {
            var business = Board.Create(BoardTheme.Business);
            foreach (var theme in Enum.GetValues<BoardTheme>())
            {
                var board = Board.Create(theme);
                Assert.Equal(Board.CellCount, board.Count);
                for (int i = 0; i < Board.CellCount; i++)
                {
                    Assert.Equal(business[i].Type, board[i].Type);
                    Assert.Equal(business[i].Price, board[i].Price);
                }
            }
        }

        [Fact]
        public void AllThemes_UniqueNonEmptyNames()
        {
            foreach (var theme in Enum.GetValues<BoardTheme>())
            {
                var names = Board.Create(theme).Select(c => c.Name).ToList();
                Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
                Assert.Equal(names.Count, names.Distinct().Count());
            }
        }

        [Fact]
        public void MilitaryBoard_HasItsOwnNames()
        {
            var board = Board.Create(BoardTheme.Military);

            Assert.Equal("Пункт збору", board[0].Name);
            Assert.Equal("Гауптвахта", board[Jail].Name);
            Assert.Equal("Піхота", board[Atb].Name);
            Assert.Equal("Patriot", board[31].Name);
            Assert.Equal("Військова залізниця", board[NovaPoshta].Name);
        }

        [Fact]
        public void Game_UsesChosenTheme_AndReplayKeepsIt()
        {
            var game = Game.Start(new[] { "Аня", "Богдан" }, seed: 7, BoardTheme.Military);

            Assert.Equal(BoardTheme.Military, game.State.Theme);
            Assert.Equal(BoardTheme.Military, game.State.ToSnapshot().Theme);
            Assert.Equal("Піхота", game.State.Board[Atb].Name);

            game.Do(new RollDice(game.State.CurrentPlayer.Id));
            var copy = Game.Replay(new[] { "Аня", "Богдан" }, 7, game.Actions, BoardTheme.Military);

            Assert.Equal(BoardTheme.Military, copy.State.Theme);
            Assert.Equal(game.State.ToSnapshot().Players, copy.State.ToSnapshot().Players);
        }

        [Fact]
        public void DefaultTheme_IsBusiness()
        {
            var game = Game.Start(new[] { "Аня", "Богдан" }, seed: 7);

            Assert.Equal(BoardTheme.Business, game.State.Theme);
            Assert.Equal("АКБ", game.State.Board[Atb].Name);
        }

        [Fact]
        public void EngineMessages_UseThemeNames()
        {
            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Military });
            game.Give(0, Wog);

            Assert.Equal("На «ОК «Північ»» підрозділи не будуються.", game.Error(new BuildBranch(0, Wog)));
        }

        // На военной доске «філія» — «підрозділ», «головний офіс» — «штаб» (§15); на основной — как было.
        [Fact]
        public void EngineMessages_UseThemeTerms()
        {
            var military = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Military });
            military.Give(0, Atb, Varus);
            Assert.Equal("Підрозділи будуються, лише коли у вас уся група.", military.Error(new BuildBranch(0, Atb)));
            military.Give(0, Silpo);
            Assert.Equal("Тут немає підрозділів.", military.Error(new SellBranch(0, Atb)));
            foreach (var cell in new[] { Atb, Varus, Silpo })
                military.State.Board[cell].Level = GameRules.HeadOfficeLevel;
            Assert.Equal("Тут уже штаб.", military.Error(new BuildBranch(0, Atb)));

            var business = Create();
            business.Give(0, Atb, Varus);
            Assert.Equal("Філії будуються, лише коли у вас уся група.", business.Error(new BuildBranch(0, Atb)));
        }
    }
}
