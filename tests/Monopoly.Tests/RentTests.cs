using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Аренда (RULES.md, §5). Во всех тестах Аня (0) платит Богдану (1).
    public class RentTests
    {
        private static int RentPaidBy(Game game, ActionResult result) =>
            Assert.Single(result.Events.OfType<RentPaid>()).Amount;

        [Fact]
        public void OrdinaryCompany_TenPercent()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);

            var result = game.Do(new RollDice(0));

            Assert.Equal(14, RentPaidBy(game, result));
            Assert.Equal(GameRules.StartingBalance - 14, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 14, game.P(1).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void OwnCompany_PaysNothing()
        {
            var game = Create(1, 2);
            game.Give(0, Silpo);

            var result = game.Do(new RollDice(0));

            Assert.Empty(result.Events.OfType<RentPaid>());
            Assert.Equal(GameRules.StartingBalance, game.P(0).Balance);
        }

        [Fact]
        public void Monopoly_DoublesRent_EvenWithMortgagedCompany()
        {
            var game = Create(1, 2);
            game.Give(1, Atb, Varus, Silpo);
            game.State.Board[Atb].IsMortgaged = true;

            Assert.Equal(28, RentPaidBy(game, game.Do(new RollDice(0))));
        }

        [Theory]
        [InlineData(1, 70)]
        [InlineData(2, 210)]
        [InlineData(3, 560)]
        [InlineData(4, 770)]
        [InlineData(5, 980)]
        public void Branches_MultiplyBaseRent(int level, int rent)
        {
            var game = Create(1, 2);
            game.Give(1, Atb, Varus, Silpo);
            game.State.Board[Silpo].Level = level;

            Assert.Equal(rent, RentPaidBy(game, game.Do(new RollDice(0))));
        }

        [Fact]
        public void MortgagedCompany_NoRent()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);
            game.State.Board[Silpo].IsMortgaged = true;

            var result = game.Do(new RollDice(0));

            Assert.Contains(new RentSkipped(0, Silpo), result.Events);
            Assert.Equal(GameRules.StartingBalance, game.P(0).Balance);
        }

        [Theory]
        [InlineData(new[] { Wog }, 25)]
        [InlineData(new[] { Wog, Okko }, 50)]
        [InlineData(new[] { Wog, Okko, Upg }, 100)]
        [InlineData(new[] { Wog, Okko, Upg, Ukrnafta }, 200)]
        public void GasStations_DependOnCount(int[] owned, int rent)
        {
            var game = Create(1, 3);
            game.Give(1, owned);

            Assert.Equal(rent, RentPaidBy(game, game.Do(new RollDice(0))));
        }

        [Theory]
        [InlineData(new[] { NovaPoshta }, 16)]
        [InlineData(new[] { NovaPoshta, Ukrposhta }, 40)]
        public void Logistics_DiceTimesFourOrTen(int[] owned, int rent)
        {
            var game = Create(1, 3);
            game.P(0).Position = Rozetka;
            game.Give(1, owned);

            Assert.Equal(rent, RentPaidBy(game, game.Do(new RollDice(0))));
        }
    }
}
