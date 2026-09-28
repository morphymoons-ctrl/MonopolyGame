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
        public void Build_BeforeRoll_CostsGroupBranchPrice()
        {
            var game = WithSupermarkets();

            var result = game.Do(new BuildBranch(0, Silpo));

            Assert.Equal(1, Level(game, Silpo));
            Assert.Equal(GameRules.StartingBalance - 100_000, game.P(0).Balance);
            Assert.Contains(new BranchBuilt(0, Silpo, 1, 100_000), result.Events);
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
            game.State.Board[Atb].Level = 1;

            Assert.Equal("Будуйте рівномірно: спершу на інших компаніях групи.", game.Error(new BuildBranch(0, Atb)));
            game.Do(new BuildBranch(0, Varus));
            Assert.Equal(1, Level(game, Varus));
        }

        [Fact]
        public void HeadOffice_IsFifthLevel_AndLast()
        {
            var game = WithSupermarkets();
            foreach (var cell in new[] { Atb, Varus, Silpo })
                game.State.Board[cell].Level = 4;

            var result = game.Do(new BuildBranch(0, Silpo));

            Assert.Contains(new BranchBuilt(0, Silpo, 5, 100_000), result.Events);
            Assert.Equal("Тут уже головний офіс.", game.Error(new BuildBranch(0, Silpo)));
        }

        // За ход в группе — одно строительство (§5); в другой группе — можно; следующим ходом — снова можно.
        [Fact]
        public void Build_OnePerGroupPerTurn()
        {
            // Аня: 1 + 2 → свой «Сілько»; Богдан: 1 + 2 → «Сілько» Ани (аренда); Аня снова ходит.
            var game = Create(1, 2, 1, 2);
            game.Give(0, Atb, Varus, Silpo, Tet, 10, 11);

            game.Do(new BuildBranch(0, Atb));
            Assert.Equal("У цій групі вже будували цього ходу — наступне будівництво лише наступного ходу.", game.Error(new BuildBranch(0, Varus)));
            Assert.DoesNotContain(new BuildBranch(0, Varus), game.GetAvailableActions(0));
            game.Do(new BuildBranch(0, Tet));

            // После броска ход тот же — ограничение действует до передачи хода.
            game.Do(new RollDice(0));
            Assert.NotNull(game.Error(new BuildBranch(0, Varus)));
            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));
            game.Do(new EndTurn(1));

            game.Do(new BuildBranch(0, Varus));
            Assert.Equal(1, Level(game, Varus));
        }

        [Fact]
        public void Build_NotOnGasStations()
        {
            var game = Create(3, 5);
            game.Give(0, Wog, Okko, Upg, Ukrnafta);

            Assert.Equal("На «MOG» філії не будуються.", game.Error(new BuildBranch(0, Wog)));
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
            game.P(0).Balance = 90_000;

            Assert.Equal($"Не вистачає грошей: філія коштує {M(100_000)}.", game.Error(new BuildBranch(0, Silpo)));
        }

        [Fact]
        public void Build_OnlyInOwnTurn()
        {
            var game = Create(3, 5);
            game.Give(1, Atb, Varus, Silpo);

            Assert.Equal("Зараз ходить Аня.", game.Error(new BuildBranch(1, Silpo)));
        }

        [Fact]
        public void Sell_Evenly_For75PercentOfBranchCost()
        {
            var game = WithSupermarkets();
            game.State.Board[Atb].Level = 1;
            game.State.Board[Silpo].Level = 2;
            game.State.Board[Varus].Level = 2;

            Assert.Equal("Продавайте рівномірно: спершу з інших компаній групи.", game.Error(new SellBranch(0, Atb)));
            var result = game.Do(new SellBranch(0, Silpo));

            Assert.Equal(1, Level(game, Silpo));
            Assert.Equal(GameRules.StartingBalance + 75_000, game.P(0).Balance);
            Assert.Contains(new BranchSold(0, Silpo, 1, 75_000), result.Events);
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
            Assert.Equal(GameRules.StartingBalance + 50_000, game.P(0).Balance);
            Assert.Equal("Компанію вже закладено.", game.Error(new MortgageCompany(0, Atb)));

            game.Do(new RedeemCompany(0, Atb));
            Assert.False(game.State.Board[Atb].IsMortgaged);
            Assert.Equal(GameRules.StartingBalance - 5_000, game.P(0).Balance);
        }

        // Срок выкупа (§10): 15 своих ходов; не выкупил — в начале 16-го компания уходит банку.
        // Ходы передаём напрямую (этап «после броска»), чтобы не расписывать кубики на 30 ходов.
        private static ActionResult PassTo(Game game, int nextId)
        {
            int current = game.State.CurrentPlayer.Id;
            game.State.Stage = TurnPhase.Manage;
            var result = game.Do(new EndTurn(current));
            Assert.Equal(nextId, game.State.CurrentPlayer.Id);
            return result;
        }

        [Fact]
        public void Mortgage_ExpiresAfterFifteenOwnTurns()
        {
            var game = WithSupermarkets();
            game.Do(new MortgageCompany(0, Atb));
            Assert.Equal(GameRules.MortgageTurns, game.State.Board[Atb].MortgageTurnsLeft);

            for (int turn = 1; turn <= GameRules.MortgageTurns; turn++)
            {
                PassTo(game, 1);
                var started = PassTo(game, 0);
                Assert.DoesNotContain(started.Events, e => e is MortgageExpired);
                Assert.Equal(GameRules.MortgageTurns - turn, game.State.Board[Atb].MortgageTurnsLeft);
            }
            Assert.Equal(0, game.State.Board[Atb].OwnerId);
            Assert.Equal(0, game.State.ToSnapshot().Cells[Atb].MortgageTurnsLeft);

            PassTo(game, 1);
            var expired = PassTo(game, 0);

            Assert.Contains(new MortgageExpired(0, Atb), expired.Events);
            Assert.Null(game.State.Board[Atb].OwnerId);
            Assert.False(game.State.Board[Atb].IsMortgaged);
            // Деньги за залог остаются у игрока.
            Assert.Equal(GameRules.StartingBalance + 50_000, game.P(0).Balance);
        }

        [Fact]
        public void Mortgage_SkippedTurnsDoNotCount_RedeemClearsDeadline()
        {
            var game = WithSupermarkets();
            game.Do(new MortgageCompany(0, Atb));

            game.P(0).IsResting = true;
            PassTo(game, 1);
            PassTo(game, 1); // ход Ани пропущен — срок не уменьшается
            Assert.Equal(GameRules.MortgageTurns, game.State.Board[Atb].MortgageTurnsLeft);

            PassTo(game, 0);
            Assert.Equal(GameRules.MortgageTurns - 1, game.State.Board[Atb].MortgageTurnsLeft);
            game.Do(new RedeemCompany(0, Atb));
            Assert.Equal(0, game.State.Board[Atb].MortgageTurnsLeft);
            Assert.False(game.State.Board[Atb].IsMortgaged);
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
            game.P(0).Balance = 70_000;

            Assert.Equal($"Не вистачає грошей: викуп коштує {M(77_000)}.", game.Error(new RedeemCompany(0, Silpo)));
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
