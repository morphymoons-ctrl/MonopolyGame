using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Дубли и пєтушатня (RULES.md, §3, §6): попал — пропускаешь следующий ход.
    public class JailTests
    {
        [Fact]
        public void ThreeDoubles_SendToJail_AndSkipNextTurn()
        {
            // Все компании — у Ани, чтобы дубли не останавливались на покупке. Богдан потом кидает 1 + 2.
            var game = Create(1, 1, 2, 2, 3, 3, 1, 2);
            game.Give(0, Enumerable.Range(0, Board.CellCount).Where(i => game.State.Board[i].IsPurchasable).ToArray());

            game.Do(new RollDice(0));
            game.Do(new RollDice(0));
            var third = game.Do(new RollDice(0));

            Assert.Equal(Jail, game.P(0).Position);
            Assert.True(game.P(0).IsInJail);
            Assert.Contains(new SentToJail(0, JailReason.ThreeDoubles), third.Events);
            Assert.DoesNotContain(third.Events, e => e is PlayerMoved);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);

            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));
            var skip = game.Do(new EndTurn(1));

            Assert.Contains(new TurnSkipped(0, SkipReason.Jail), skip.Events);
            Assert.Equal(1, game.State.CurrentPlayer.Id);
            Assert.False(game.P(0).IsInJail);
        }

        [Fact]
        public void LandingOnJail_SkipsNextTurn()
        {
            // Аня: 3 + 5 → клетка 8. Богдан: 1 + 2 → «Сільпо» Ани, платит аренду.
            var game = Create(3, 5, 1, 2);
            game.Give(0, Silpo);

            var landing = game.Do(new RollDice(0));

            Assert.Contains(new SentToJail(0, JailReason.Landed), landing.Events);
            Assert.True(game.P(0).IsInJail);
            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));
            var skip = game.Do(new EndTurn(1));

            Assert.Contains(new TurnSkipped(0, SkipReason.Jail), skip.Events);
            Assert.Equal(new TurnStarted(1), skip.Events[^1]);
        }

        [Fact]
        public void LandingWithDouble_NoMoreRolls()
        {
            var game = Create(2, 2);
            game.P(0).Position = Wog;

            var result = game.Do(new RollDice(0));

            Assert.Equal(Jail, game.P(0).Position);
            Assert.DoesNotContain(result.Events, e => e is RollAgain);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void JailCard_CancelsSkip_AndReturnsToDiscard()
        {
            var game = Create(3, 5, 1, 2);
            game.Give(0, Silpo);
            game.P(0).JailCards = 1;

            var landing = game.Do(new RollDice(0));

            Assert.Contains(new JailCardUsed(0), landing.Events);
            Assert.False(game.P(0).IsInJail);
            Assert.Equal(0, game.P(0).JailCards);
            Assert.Contains(ChanceCard.GetOutOfJail, game.State.ChanceDiscard);

            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));
            var next = game.Do(new EndTurn(1));
            Assert.DoesNotContain(next.Events, e => e is TurnSkipped);
            Assert.Equal(0, game.State.CurrentPlayer.Id);
        }

        [Fact]
        public void JailCard_WithDouble_KeepsExtraRoll()
        {
            var game = Create(2, 2);
            game.P(0).Position = Wog;
            game.P(0).JailCards = 1;

            var result = game.Do(new RollDice(0));

            Assert.Contains(new RollAgain(0), result.Events);
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
        }

        [Fact]
        public void PrisonerStillCollectsRent()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);
            game.P(1).IsInJail = true;
            game.P(1).Position = Jail;

            game.Do(new RollDice(0));

            Assert.Equal(GameRules.StartingBalance + 16_800, game.P(1).Balance);
        }
    }

        // Казино (RULES.md, §8).
    public class CasinoTests
    {
        // Аня: 12 → 16 «Казино», дальше число для исхода.
        private static Game OnCasino(int outcome)
        {
            var game = Create(1, 3, outcome);
            game.P(0).Position = Okko;
            game.Do(new RollDice(0));
            return game;
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(49, 0)]
        [InlineData(50, 1)]
        [InlineData(59, 1)]
        [InlineData(60, 2)]
        [InlineData(94, 2)]
        [InlineData(95, 3)]
        [InlineData(99, 3)]
        public void Outcomes_FollowOdds(int roll, int multiplier)
        {
            var game = OnCasino(roll);

            var result = game.Do(new PlayCasino(0, 100_000));

            Assert.Contains(new CasinoPlayed(0, 100_000, multiplier), result.Events);
            Assert.Equal(GameRules.StartingBalance + 100_000 * multiplier - 100_000, game.P(0).Balance);
        }

        [Fact]
        public void OnlyOncePerLanding()
        {
            var game = OnCasino(0);
            game.Do(new PlayCasino(0, 50_000));

            Assert.Equal("Грати в казино можна лише одразу після потрапляння на клітинку.", game.Error(new PlayCasino(0, 50_000)));
        }

        [Fact]
        public void NotAvailableWithoutLanding()
        {
            var game = Create(3, 5);
            game.Do(new RollDice(0));

            Assert.False(game.CanExecute(new PlayCasino(0, 50_000)));
        }

        [Fact]
        public void BetMustBeFromListAndAffordable()
        {
            var game = OnCasino(0);

            Assert.Equal($"Ставка може бути {M(50_000)}, {M(100_000)}, {M(200_000)}, {M(300_000)}.", game.Error(new PlayCasino(0, 75_000)));
            game.P(0).Balance = 150_000;
            Assert.Equal($"Не вистачає грошей на ставку: у вас {M(150_000)}.", game.Error(new PlayCasino(0, 200_000)));
        }

        [Fact]
        public void SkippingIsJustEndingTurn()
        {
            var game = OnCasino(0);

            game.Do(new EndTurn(0));

            Assert.False(game.State.CasinoAvailable);
        }
    }
}
