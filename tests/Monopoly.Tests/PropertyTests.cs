using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Филиалы (RULES.md, §5) и залог (§10). У Ани (0) — все супермаркеты, если не сказано иначе.
    public class PropertyTests
    {
        private static Game WithSupermarkets()
        {
            var game = Create(3, 5);
            game.Give(0, Atb, Varus, Silpo);
            return game;
        }

        private static int Level(Game game, int cell) => game.State.Board[cell].Level;

        [Fact]
        public void Build_BeforeRoll_CostsHalfPrice()
        {
            var game = WithSupermarkets();

            var result = game.Do(new BuildBranch(0, Silpo));

            Assert.Equal(1, Level(game, Silpo));
            Assert.Equal(GameRules.StartingBalance - 70, game.P(0).Balance);
            Assert.Contains(new BranchBuilt(0, Silpo, 1, 70), result.Events);
        }

        [Fact]
        public void Build_NeedsWholeGroup()
        {
            var game = Create(3, 5);
            game.Give(0, Atb, Varus);

            Assert.Equal("Филиалы строятся, только когда у вас вся группа.", game.Error(new BuildBranch(0, Atb)));
        }

        [Fact]
        public void Build_MustBeEven()
        {
            var game = WithSupermarkets();
            game.Do(new BuildBranch(0, Atb));

            Assert.Equal("Стройте равномерно: сначала на других компаниях группы.", game.Error(new BuildBranch(0, Atb)));
            game.Do(new BuildBranch(0, Varus));
            game.Do(new BuildBranch(0, Silpo));
            game.Do(new BuildBranch(0, Atb));
            Assert.Equal(2, Level(game, Atb));
        }

        [Fact]
        public void HeadOffice_IsFifthLevel_AndLast()
        {
            var game = WithSupermarkets();
            foreach (var cell in new[] { Atb, Varus, Silpo })
                game.State.Board[cell].Level = 4;

            var result = game.Do(new BuildBranch(0, Silpo));

            Assert.Contains(new BranchBuilt(0, Silpo, 5, 70), result.Events);
            game.Do(new BuildBranch(0, Atb));
            game.Do(new BuildBranch(0, Varus));
            Assert.Equal("Здесь уже головной офис.", game.Error(new BuildBranch(0, Silpo)));
        }

        [Fact]
        public void Build_NotOnGasStations()
        {
            var game = Create(3, 5);
            game.Give(0, Wog, Okko, Upg, Ukrnafta);

            Assert.Equal("На АЗС и логистике филиалы не строятся.", game.Error(new BuildBranch(0, Wog)));
        }

        [Fact]
        public void Build_BlockedByMortgageInGroup()
        {
            var game = WithSupermarkets();
            game.State.Board[Atb].IsMortgaged = true;

            Assert.Equal("В группе есть заложенная компания — сначала выкупите её.", game.Error(new BuildBranch(0, Silpo)));
        }

        [Fact]
        public void Build_NeedsMoney()
        {
            var game = WithSupermarkets();
            game.P(0).Balance = 60;

            Assert.Equal("Не хватает денег: филиал стоит 70 грн.", game.Error(new BuildBranch(0, Silpo)));
        }

        [Fact]
        public void Build_OnlyInOwnTurn()
        {
            var game = Create(3, 5);
            game.Give(1, Atb, Varus, Silpo);

            Assert.Equal("Сейчас ходит Аня.", game.Error(new BuildBranch(1, Silpo)));
        }

        [Fact]
        public void Sell_EvenlyForHalfOfBranchCost()
        {
            var game = WithSupermarkets();
            game.State.Board[Atb].Level = 1;
            game.State.Board[Silpo].Level = 2;
            game.State.Board[Varus].Level = 2;

            Assert.Equal("Продавайте равномерно: сначала с других компаний группы.", game.Error(new SellBranch(0, Atb)));
            var result = game.Do(new SellBranch(0, Silpo));

            Assert.Equal(1, Level(game, Silpo));
            Assert.Equal(GameRules.StartingBalance + 35, game.P(0).Balance);
            Assert.Contains(new BranchSold(0, Silpo, 1, 35), result.Events);
        }

        [Fact]
        public void Sell_NothingToSell()
        {
            var game = WithSupermarkets();

            Assert.Equal("Здесь нет филиалов.", game.Error(new SellBranch(0, Silpo)));
        }

        [Fact]
        public void Mortgage_HalfPrice_RedeemPlusTenPercent()
        {
            var game = WithSupermarkets();

            game.Do(new MortgageCompany(0, Atb));
            Assert.True(game.State.Board[Atb].IsMortgaged);
            Assert.Equal(GameRules.StartingBalance + 50, game.P(0).Balance);
            Assert.Equal("Компания уже заложена.", game.Error(new MortgageCompany(0, Atb)));

            game.Do(new RedeemCompany(0, Atb));
            Assert.False(game.State.Board[Atb].IsMortgaged);
            Assert.Equal(GameRules.StartingBalance - 5, game.P(0).Balance);
        }

        [Fact]
        public void Mortgage_NeedsNoBranchesInGroup()
        {
            var game = WithSupermarkets();
            game.State.Board[Silpo].Level = 1;

            Assert.Equal("Сначала продайте филиалы в этой группе.", game.Error(new MortgageCompany(0, Atb)));
        }

        [Fact]
        public void Redeem_NeedsMoney()
        {
            var game = WithSupermarkets();
            game.State.Board[Silpo].IsMortgaged = true;
            game.P(0).Balance = 70;

            Assert.Equal("Не хватает денег: выкуп стоит 77 грн.", game.Error(new RedeemCompany(0, Silpo)));
        }

        [Fact]
        public void OnlyOwnCompanies()
        {
            var game = WithSupermarkets();
            game.Give(1, Wog);

            Assert.Equal("Это не ваша компания.", game.Error(new MortgageCompany(0, Wog)));
            Assert.Equal("Нет такой клетки.", game.Error(new MortgageCompany(0, 99)));
        }
    }
}
