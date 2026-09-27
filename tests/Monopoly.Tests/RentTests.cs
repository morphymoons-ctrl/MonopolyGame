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
        public void OrdinaryCompany_TwelvePercent()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);

            var result = game.Do(new RollDice(0));

            Assert.Equal(16_800, RentPaidBy(game, result));
            Assert.Equal(GameRules.StartingBalance - 16_800, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 16_800, game.P(1).Balance);
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

            Assert.Equal(33_600, RentPaidBy(game, game.Do(new RollDice(0))));
        }

        [Theory]
        [InlineData(1, 84_000)]
        [InlineData(2, 252_000)]
        [InlineData(3, 588_000)]
        [InlineData(4, 756_000)]
        [InlineData(5, 924_000)]
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
        [InlineData(new[] { Wog }, 25_000)]
        [InlineData(new[] { Wog, Okko }, 50_000)]
        [InlineData(new[] { Wog, Okko, Upg }, 100_000)]
        [InlineData(new[] { Wog, Okko, Upg, Ukrnafta }, 200_000)]
        public void GasStations_DependOnCount(int[] owned, int rent)
        {
            var game = Create(1, 3);
            game.Give(1, owned);

            Assert.Equal(rent, RentPaidBy(game, game.Do(new RollDice(0))));
        }

        // Аренда на клетке у игроков — короткой записью, чтобы отличать от цены покупки.
        [Theory]
        [InlineData(4_000, "4к")]
        [InlineData(16_800, "16,8к")]
        [InlineData(84_000, "84к")]
        [InlineData(924_000, "924к")]
        [InlineData(1_050_000, "1,05м")]
        [InlineData(2_000_000, "2м")]
        public void ShortMoney_ForRentOnCells(int amount, string text)
        {
            Assert.Equal(text, GameRules.ShortMoney(amount));
        }

        // Клиент считает аренду на клетках по своему полю, обновлённому из снимка, — она совпадает с арендой движка.
        [Fact]
        public void BoardFromSnapshot_GivesSameRentAsEngine()
        {
            var game = Create();
            game.Give(1, Atb, Varus, Silpo, Wog, Okko, NovaPoshta, Ukrposhta, Tet);
            game.State.Board[Silpo].Level = 3;
            game.State.Board[Varus].Level = 3;
            game.State.Board[Atb].Level = 2;
            game.State.Board[Tet].IsMortgaged = true;

            var clientBoard = Board.CreateDefault();
            game.State.ToSnapshot().ApplyTo(clientBoard);

            for (int i = 0; i < Board.CellCount; i++)
                Assert.Equal(GameRules.Rent(game.State.Board, i, 7), GameRules.Rent(clientBoard, i, 7));
        }

        [Theory]
        [InlineData(new[] { NovaPoshta }, 16_000)]
        [InlineData(new[] { NovaPoshta, Ukrposhta }, 40_000)]
        public void Logistics_DiceTimesFourOrTen(int[] owned, int rent)
        {
            var game = Create(1, 3);
            game.P(0).Position = Massage;
            game.Give(1, owned);

            Assert.Equal(rent, RentPaidBy(game, game.Do(new RollDice(0))));
        }
    }
}
