using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Тематики доски (RULES.md, §15): отличаются только названия.
    public class ThemeTests
    {
        // Раскладка у всех досок одна. Цены внутри группы свои, но сумма по группе — как на основной доске:
        // экономика партии не зависит от выбора доски (§15).
        [Fact]
        public void AllThemes_SameLayout_AndSameGroupTotals()
        {
            var business = Board.Create(BoardTheme.Business);
            foreach (var theme in Enum.GetValues<BoardTheme>())
            {
                var board = Board.Create(theme);
                Assert.Equal(Board.CellCount, board.Count);
                for (int i = 0; i < Board.CellCount; i++)
                    Assert.Equal(business[i].Type, board[i].Type);

                foreach (var type in Enum.GetValues<CellType>())
                {
                    int expected = business.Where(c => c.Type == type).Sum(c => c.Price);
                    Assert.True(expected == board.Where(c => c.Type == type).Sum(c => c.Price), $"{theme}: сумма цен группы {type} другая");
                }
                Assert.All(board.Where(c => c.IsPurchasable), c => Assert.InRange(c.Price, 80_000, 280_000));
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
        public void GovernmentBoard_HasItsOwnNames()
        {
            var board = Board.Create(BoardTheme.Government);

            Assert.Equal("Банкова", board[0].Name);
            Assert.Equal("Лук'янівське СІЗО", board[Jail].Name);
            Assert.Equal("Патрульна поліція", board[Atb].Name);
            Assert.Equal("НАБУ", board[11].Name);
            Assert.Equal("Офіс Президента", board[31].Name);
        }

        [Fact]
        public void GovernmentBoard_UsesDepartmentTerms()
        {
            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Government });
            game.Give(0, Atb, Varus);

            Assert.Equal("Відділи будуються, лише коли у вас уся група.", game.Error(new BuildBranch(0, Atb)));
        }

        [Fact]
        public void CryptoBoard_HasItsOwnNamesAndTerms()
        {
            var board = Board.Create(BoardTheme.Crypto);
            Assert.Equal("Генезис-блок", board[0].Name);
            Assert.Equal("Блокування акаунта", board[Jail].Name);
            Assert.Equal("Dogecoin", board[Atb].Name);
            Assert.Equal("Bitcoin", board[31].Name);

            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Crypto });
            game.Give(0, Atb, Varus);
            Assert.Equal("Ноди будуються, лише коли у вас уся група.", game.Error(new BuildBranch(0, Atb)));
        }

        // На доске «Криптовалюти» — доллары: те же суммы, другой знак (§15).
        [Fact]
        public void CryptoBoard_UsesDollars()
        {
            // Между тысячами в игре — неразрывный пробел; здесь сравниваем с обычным.
            static string Plain(string text) => text.Replace(' ', ' ');

            Assert.Equal("$1 500 000", Plain(GameRules.Money(1_500_000, BoardTheme.Crypto)));
            Assert.Equal("1 500 000 грн", Plain(GameRules.Money(1_500_000, BoardTheme.Business)));
            Assert.Equal("1 500 млн грн", Plain(GameRules.Money(1_500_000, BoardTheme.Military)));
            Assert.Equal("$252к", GameRules.ShortMoney(252_000, BoardTheme.Crypto));
            Assert.Equal("252к", GameRules.ShortMoney(252_000, BoardTheme.Business));

            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Crypto });
            game.Give(0, Atb, Varus, Silpo);
            game.P(0).Balance = 50_000;
            Assert.Equal("Не вистачає грошей: нода коштує $100 000.", Plain(game.Error(new BuildBranch(0, Atb))!));
        }

        [Fact]
        public void GamesBoard_HasItsOwnNamesAndTerms()
        {
            var board = Board.Create(BoardTheme.Games);
            Assert.Equal("Головне меню", board[0].Name);
            Assert.Equal("Бан за читерство", board[Jail].Name);
            Assert.Equal("Steam", board[Wog].Name);
            Assert.Equal("Dota 2", board[31].Name);

            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Games });
            game.Give(0, Atb, Varus);
            Assert.Equal("Сервери будуються, лише коли у вас уся група.", game.Error(new BuildBranch(0, Atb)));
        }

        [Fact]
        public void OligarchsBoard_HasItsOwnNamesAndTerms()
        {
            var board = Board.Create(BoardTheme.Oligarchs);
            Assert.Equal("Ваучер", board[0].Name);
            Assert.Equal("Санкції", board[Jail].Name);
            Assert.Equal("Полтавське родовище", board[Wog].Name);
            Assert.Equal("Фінпромгрупа", board[31].Name);

            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Oligarchs });
            game.Give(0, Atb, Varus);
            Assert.Equal("Дочки будуються, лише коли у вас уся група.", game.Error(new BuildBranch(0, Atb)));
        }

        [Fact]
        public void KyivBoard_HasItsOwnNamesAndTerms()
        {
            var board = Board.Create(BoardTheme.Kyiv);
            Assert.Equal("Нульовий кілометр", board[0].Name);
            Assert.Equal("Затор на мосту Патона", board[Jail].Name);
            Assert.Equal("Центральний вокзал", board[Wog].Name);
            Assert.Equal("101 Tower", board[31].Name);

            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Kyiv });
            game.Give(0, Atb, Varus);
            Assert.Equal("Поверхи будуються, лише коли у вас уся група.", game.Error(new BuildBranch(0, Atb)));
        }

        [Fact]
        public void ClassicBoard_HasItsOwnNamesAndTerms()
        {
            var board = Board.Create(BoardTheme.Classic);
            Assert.Equal("Старт", board[0].Name);
            Assert.Equal("В'язниця", board[Jail].Name);
            Assert.Equal("Shell", board[Wog].Name);
            Assert.Equal("Apple", board[31].Name);

            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(), Fixed with { Theme = BoardTheme.Classic });
            game.Give(0, Atb, Varus);
            Assert.Equal("Будинки будуються, лише коли у вас уся група.", game.Error(new BuildBranch(0, Atb)));
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

            Assert.Equal("На «ОК „Північ“» підрозділи не будуються.", game.Error(new BuildBranch(0, Wog)));
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
