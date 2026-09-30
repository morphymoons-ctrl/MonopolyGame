using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Встречное предложение в обмене (RULES.md, §11). Аня (0) предлагает Богдану (1) АТБ за 50 000.
    public class CounterTradeTests
    {
        private static readonly int[] None = Array.Empty<int>();

        private static Game Offered()
        {
            var game = Create();
            game.Give(0, Atb);
            game.Give(1, Varus);
            game.Do(new ProposeTrade(0, 1, new TradeTerms(new[] { Atb }, 0, 0), new TradeTerms(None, 50_000, 0)));
            return game;
        }

        // Богдан хочет АТБ, но платить готов 30 000.
        private static CounterTrade CheaperCounter(int counterer = 1) =>
            new(counterer, new TradeTerms(None, 30_000, 0), new TradeTerms(new[] { Atb }, 0, 0));

        [Fact]
        public void Receiver_CanCounter_AndNowProposerAnswers()
        {
            var game = Offered();
            Assert.Contains(game.GetAvailableActions(1), a => a is CounterTrade);

            var result = game.Do(CheaperCounter());

            var offer = game.State.Trade!;
            Assert.Equal(1, offer.FromId);
            Assert.Equal(0, offer.ToId);
            Assert.Equal(30_000, offer.Give.Money);
            Assert.Equal(1, offer.Counters);
            Assert.Contains(result.Events, e => e is TradeCountered);
            Assert.Equal(TurnPhase.TradeOffer, game.State.Phase);
            Assert.Equal(new[] { 0 }, game.AwaitedPlayers());
        }

        [Fact]
        public void AcceptedCounter_UsesNewTerms()
        {
            var game = Offered();
            game.Do(CheaperCounter());

            game.Do(new AcceptTrade(0));

            Assert.Equal(1, game.State.Board[Atb].OwnerId);
            Assert.Equal(GameRules.StartingBalance + 30_000, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance - 30_000, game.P(1).Balance);
            Assert.Null(game.State.Trade);
        }

        [Fact]
        public void OnlyReceiver_CanCounter()
        {
            var game = Offered();

            Assert.Equal("Відповісти на обмін може лише той, кому його запропонували.", game.Error(CheaperCounter(counterer: 0)));
            Assert.DoesNotContain(game.GetAvailableActions(0), a => a is CounterTrade);
        }

        [Fact]
        public void Counter_IsCheckedLikeOffer()
        {
            var game = Offered();

            // Клетка 2 и так у Богдана — просить её у Ани нельзя.
            var wrong = new CounterTrade(1, TradeTerms.Empty, new TradeTerms(new[] { Varus }, 0, 0));

            Assert.Equal($"«{game.State.Board[Varus].Name}» у гравця Аня немає.", game.Error(wrong));
        }

        [Fact]
        public void AtMostThreeCounters()
        {
            var game = Offered();
            game.Do(CheaperCounter());
            game.Do(new CounterTrade(0, new TradeTerms(new[] { Atb }, 0, 0), new TradeTerms(None, 40_000, 0)));
            game.Do(CheaperCounter());
            Assert.Equal(GameRules.MaxTradeCounters, game.State.Trade!.Counters);

            var fourth = new CounterTrade(0, new TradeTerms(new[] { Atb }, 0, 0), new TradeTerms(None, 35_000, 0));

            Assert.Equal("Змінювати умови можна не більше 3 разів — прийміть обмін або відмовтеся.", game.Error(fourth));
            Assert.DoesNotContain(game.GetAvailableActions(0), a => a is CounterTrade);
            Assert.Contains(new AcceptTrade(0), game.GetAvailableActions(0));
        }

        [Fact]
        public void Counterer_CanWithdraw_TradeEnds()
        {
            var game = Offered();
            game.Do(CheaperCounter());

            game.Do(new CancelTrade(1));

            Assert.Null(game.State.Trade);
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
        }
    }
}
