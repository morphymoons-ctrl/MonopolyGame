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

            Assert.Equal("Філії будуються, лише коли у вас уся група.", game.Error(new BuildBranch(0, Atb)));
        }

        [Fact]
        public void Build_MustBeEven()
        {
            var game = WithSupermarkets();
            game.Do(new BuildBranch(0, Atb));

            Assert.Equal("Будуйте рівномірно: спершу на інших компаніях групи.", game.Error(new BuildBranch(0, Atb)));
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
            Assert.Equal("Тут уже головний офіс.", game.Error(new BuildBranch(0, Silpo)));
        }

        [Fact]
        public void Build_NotOnGasStations()
        {
            var game = Create(3, 5);
            game.Give(0, Wog, Okko, Upg, Ukrnafta);

            Assert.Equal("На АЗС і логістиці філії не будуються.", game.Error(new BuildBranch(0, Wog)));
        }

        [Fact]
        public void Build_BlockedByMortgageInGroup()
        {
            var game = WithSupermarkets();
            game.State.Board[Atb].IsMortgaged = true;

            Assert.Equal("У групі є закладена компанія — спершу викупіть її.", game.Error(new BuildBranch(0, Silpo)));
        }

        [Fact]
        public void Build_NeedsMoney()
        {
            var game = WithSupermarkets();
            game.P(0).Balance = 60;

            Assert.Equal("Не вистачає грошей: філія коштує 70 грн.", game.Error(new BuildBranch(0, Silpo)));
        }

        [Fact]
        public void Build_OnlyInOwnTurn()
        {
            var game = Create(3, 5);
            game.Give(1, Atb, Varus, Silpo);

            Assert.Equal("Зараз ходить Аня.", game.Error(new BuildBranch(1, Silpo)));
        }

        [Fact]
        public void Sell_EvenlyForHalfOfBranchCost()
        {
            var game = WithSupermarkets();
            game.State.Board[Atb].Level = 1;
            game.State.Board[Silpo].Level = 2;
            game.State.Board[Varus].Level = 2;

            Assert.Equal("Продавайте рівномірно: спершу з інших компаній групи.", game.Error(new SellBranch(0, Atb)));
            var result = game.Do(new SellBranch(0, Silpo));

            Assert.Equal(1, Level(game, Silpo));
            Assert.Equal(GameRules.StartingBalance + 35, game.P(0).Balance);
            Assert.Contains(new BranchSold(0, Silpo, 1, 35), result.Events);
        }

        [Fact]
        public void Sell_NothingToSell()
        {
            var game = WithSupermarkets();

            Assert.Equal("Тут немає філій.", game.Error(new SellBranch(0, Silpo)));
        }

        [Fact]
        public void Mortgage_HalfPrice_RedeemPlusTenPercent()
        {
            var game = WithSupermarkets();

            game.Do(new MortgageCompany(0, Atb));
            Assert.True(game.State.Board[Atb].IsMortgaged);
            Assert.Equal(GameRules.StartingBalance + 50, game.P(0).Balance);
            Assert.Equal("Компанію вже закладено.", game.Error(new MortgageCompany(0, Atb)));

            game.Do(new RedeemCompany(0, Atb));
            Assert.False(game.State.Board[Atb].IsMortgaged);
            Assert.Equal(GameRules.StartingBalance - 5, game.P(0).Balance);
        }

        [Fact]
        public void Mortgage_NeedsNoBranchesInGroup()
        {
            var game = WithSupermarkets();
            game.State.Board[Silpo].Level = 1;

            Assert.Equal("Спершу продайте філії в цій групі.", game.Error(new MortgageCompany(0, Atb)));
        }

        [Fact]
        public void Redeem_NeedsMoney()
        {
            var game = WithSupermarkets();
            game.State.Board[Silpo].IsMortgaged = true;
            game.P(0).Balance = 70;

            Assert.Equal("Не вистачає грошей: викуп коштує 77 грн.", game.Error(new RedeemCompany(0, Silpo)));
        }

        [Fact]
        public void OnlyOwnCompanies()
        {
            var game = WithSupermarkets();
            game.Give(1, Wog);

            Assert.Equal("Це не ваша компанія.", game.Error(new MortgageCompany(0, Wog)));
            Assert.Equal("Немає такої клітинки.", game.Error(new MortgageCompany(0, 99)));
        }
    }
}
