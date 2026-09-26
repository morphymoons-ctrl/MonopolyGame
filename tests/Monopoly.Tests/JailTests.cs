using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Дубли и тюрьма (RULES.md, §3, §6).
    public class JailTests
    {
        private static Game InJail(params int[] dice)
        {
            var game = Create(dice);
            game.P(0).Position = Jail;
            game.P(0).IsInJail = true;
            return game;
        }

        [Fact]
        public void ThreeDoubles_SendToJailWithoutThirdMove()
        {
            // Все компании — у Ани, чтобы дубли не останавливались на покупке.
            var game = Create(1, 1, 2, 2, 3, 3);
            game.Give(0, Enumerable.Range(0, Board.CellCount).Where(i => game.State.Board[i].IsPurchasable).ToArray());

            game.Do(new RollDice(0));
            game.Do(new RollDice(0));
            var third = game.Do(new RollDice(0));

            Assert.Equal(Jail, game.P(0).Position);
            Assert.True(game.P(0).IsInJail);
            Assert.Contains(new SentToJail(0, JailReason.ThreeDoubles), third.Events);
            Assert.DoesNotContain(third.Events, e => e is PlayerMoved);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void PayBail_ThenNormalRoll()
        {
            var game = InJail(1, 2);

            var result = game.Do(new PayBail(0));

            Assert.False(game.P(0).IsInJail);
            Assert.Equal(GameRules.StartingBalance - 50, game.P(0).Balance);
            Assert.Contains(new LeftJail(0, JailExit.Bail), result.Events);
            game.Do(new RollDice(0));
            Assert.Equal(Jail + 3, game.P(0).Position);
        }

        [Fact]
        public void JailCard_FreesAndReturnsToDiscard()
        {
            var game = InJail();
            game.P(0).JailCards = 1;
            game.State.ChanceDeck.Remove(ChanceCard.GetOutOfJail);

            game.Do(new UseJailCard(0));

            Assert.False(game.P(0).IsInJail);
            Assert.Equal(0, game.P(0).JailCards);
            Assert.Contains(ChanceCard.GetOutOfJail, game.State.ChanceDiscard);
        }

        [Fact]
        public void JailCard_WithoutCard_IsRejected()
        {
            var game = InJail();

            Assert.Equal("У вас немає картки «Вийти з пєтушатні».", game.Error(new UseJailCard(0)));
        }

        [Fact]
        public void PayBail_NotInJail_IsRejected()
        {
            var game = Create();

            Assert.Equal("Ви не у пєтушатні.", game.Error(new PayBail(0)));
        }

        [Fact]
        public void DoubleInJail_FreesAndMoves_WithoutExtraRoll()
        {
            var game = InJail(2, 2);

            var result = game.Do(new RollDice(0));

            Assert.False(game.P(0).IsInJail);
            Assert.Equal(Okko, game.P(0).Position);
            Assert.Contains(new LeftJail(0, JailExit.Double), result.Events);
            Assert.DoesNotContain(result.Events, e => e is RollAgain);
            game.Do(new BuyProperty(0));
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void FailedAttempt_StaysInJail()
        {
            var game = InJail(1, 2);

            var result = game.Do(new RollDice(0));

            Assert.True(game.P(0).IsInJail);
            Assert.Equal(Jail, game.P(0).Position);
            Assert.Equal(1, game.P(0).JailTurns);
            Assert.Contains(new JailRollFailed(0, 1), result.Events);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
            // Залог платят до броска; после неудачной попытки ход только заканчивается.
            Assert.Equal("Кубики в цьому ході вже кинуто.", game.Error(new PayBail(0)));
        }

        [Fact]
        public void ThirdFailedAttempt_ForcesBailAndMoves()
        {
            var game = InJail(1, 2);
            game.P(0).JailTurns = 2;

            var result = game.Do(new RollDice(0));

            Assert.False(game.P(0).IsInJail);
            Assert.Equal(Jail + 3, game.P(0).Position);
            Assert.Equal(GameRules.StartingBalance - 50, game.P(0).Balance);
            Assert.Contains(new LeftJail(0, JailExit.ForcedBail), result.Events);
            Assert.Contains(new PaidToBank(0, 50), result.Events);
        }

        [Fact]
        public void PrisonerStillCollectsRent()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);
            game.P(1).IsInJail = true;
            game.P(1).Position = Jail;

            game.Do(new RollDice(0));

            Assert.Equal(GameRules.StartingBalance + 14, game.P(1).Balance);
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

            var result = game.Do(new PlayCasino(0, 100));

            Assert.Contains(new CasinoPlayed(0, 100, multiplier), result.Events);
            Assert.Equal(GameRules.StartingBalance + 100 * multiplier - 100, game.P(0).Balance);
        }

        [Fact]
        public void OnlyOncePerLanding()
        {
            var game = OnCasino(0);
            game.Do(new PlayCasino(0, 50));

            Assert.Equal("Грати в казино можна лише одразу після потрапляння на клітинку.", game.Error(new PlayCasino(0, 50)));
        }

        [Fact]
        public void NotAvailableWithoutLanding()
        {
            var game = Create(3, 5);
            game.Do(new RollDice(0));

            Assert.False(game.CanExecute(new PlayCasino(0, 50)));
        }

        [Fact]
        public void BetMustBeFromListAndAffordable()
        {
            var game = OnCasino(0);

            Assert.Equal("Ставка може бути 50, 100, 200, 300 грн.", game.Error(new PlayCasino(0, 75)));
            game.P(0).Balance = 150;
            Assert.Equal("Не вистачає грошей на ставку: у вас 150 грн.", game.Error(new PlayCasino(0, 200)));
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
