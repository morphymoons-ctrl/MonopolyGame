using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Итоги партии (RULES.md, §16).
    public class StatsTests
    {
        private static StatsSnapshot Stats(Game game) => game.State.ToSnapshot().Stats!;

        [Fact]
        public void Rent_Jail_Turns_AreCounted()
        {
            // Аня: 3 + 5 → пєтушатня. Богдан: 1 + 2 → «Сільпо» Ани, платит 16 800. Ход Ани пропускается.
            var game = Create(3, 5, 1, 2);
            game.Give(0, Silpo);
            game.Do(new RollDice(0));
            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));
            game.Do(new EndTurn(1));

            var stats = Stats(game);
            Assert.Equal(1, stats.For(0)!.TimesJailed);
            Assert.Equal(16_800, stats.For(1)!.RentPaid);
            Assert.Equal(16_800, stats.For(0)!.RentReceived);
            Assert.Equal(16_800, stats.CellIncome[Silpo]);
            // Ход Ани, ход Богдана, пропуск Ани — снова Богдан.
            Assert.Equal(3, stats.Turns);
        }

        [Fact]
        public void JailCard_IsNotCountedAsJail()
        {
            var game = Create(3, 5);
            game.P(0).JailCards = 1;

            game.Do(new RollDice(0));

            Assert.Equal(0, Stats(game).For(0)!.TimesJailed);
        }

        [Fact]
        public void RentThroughDebt_IsCounted()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);
            game.Give(0, Atb);
            game.P(0).Balance = 10_000;

            game.Do(new RollDice(0));

            Assert.Equal(TurnPhase.Debt, game.State.Phase);
            Assert.Equal(16_800, Stats(game).For(0)!.RentPaid);
        }

        [Theory]
        [InlineData(95, 200_000)]
        [InlineData(55, 0)]
        [InlineData(10, -100_000)]
        public void Casino_NetResult(int roll, int net)
        {
            var game = Create(1, 3, roll);
            game.P(0).Position = Okko;
            game.Do(new RollDice(0));

            game.Do(new PlayCasino(0, 100_000));

            Assert.Equal(net, Stats(game).For(0)!.CasinoNet);
        }

        [Fact]
        public void Purchases_AreCounted()
        {
            var game = Create(1, 2);
            game.Do(new RollDice(0));
            game.Do(new BuyProperty(0));

            Assert.Equal(1, Stats(game).For(0)!.CompaniesBought);
        }

        [Fact]
        public void Standings_WinnerThenNetWorth_ThenEliminatedLatestFirst()
        {
            // Аня не может заплатить Богдану за «Сільпо» и выбывает; у Богдана «Сільпо» — капитал больше, чем у Вики.
            var game = CreateFor(3, 1, 2);
            game.Give(1, Silpo);
            game.P(0).Balance = 5_000;

            game.Do(new RollDice(0));

            var snapshot = game.State.ToSnapshot();
            Assert.Equal(new[] { 0 }, snapshot.Stats!.Eliminated);
            Assert.Equal(GameRules.StartingBalance + 5_000 + 140_000, GameRules.NetWorth(game.State.Board, 1, game.P(1).Balance));
            Assert.Equal(new[] { 1, 2, 0 }, GameRules.Standings(game.State.Board, snapshot, snapshot.Stats));
        }

        [Fact]
        public void NetWorth_CountsMortgageAsHalf_AndBranches()
        {
            var game = Create();
            game.Give(0, Atb, Varus, Silpo);
            game.State.Board[Silpo].Level = 2;
            game.State.Board[Atb].IsMortgaged = true;

            int expected = GameRules.StartingBalance
                + game.State.Board[Atb].MortgageValue + game.State.Board[Varus].Price
                + game.State.Board[Silpo].Price + 2 * game.State.Board[Silpo].BranchCost;
            Assert.Equal(expected, GameRules.NetWorth(game.State.Board, 0, game.P(0).Balance));
        }

        [Fact]
        public void Replay_RestoresStats()
        {
            var names = new[] { "Аня", "Богдан", "Віка" };
            var game = Game.Start(names, seed: 11);
            for (int step = 0; step < 60 && game.State.Phase != TurnPhase.GameOver; step++)
            {
                var player = game.AwaitedPlayers().First();
                game.Do(game.GetAvailableActions(player).First());
            }

            var copy = Game.Replay(names, 11, game.Actions);

            var a = Stats(game);
            var b = Stats(copy);
            Assert.Equal(a.Turns, b.Turns);
            Assert.Equal(a.Players, b.Players);
            Assert.Equal(a.CellIncome, b.CellIncome);
            Assert.True(a.Turns > 1);
        }
    }
}
