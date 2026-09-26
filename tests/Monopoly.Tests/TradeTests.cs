using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Обмен (RULES.md, §11). Аня (0) предлагает Богдану (1).
    public class TradeTests
    {
        private static TradeTerms Terms(int money = 0, int jailCards = 0, params int[] cells) => new(cells, money, jailCards);

        private static Game WithProperty()
        {
            var game = Create(3, 5);
            game.Give(0, Atb);
            game.Give(1, Arcelor);
            game.P(1).JailCards = 1;
            return game;
        }

        [Fact]
        public void Accept_ExchangesEverything()
        {
            var game = WithProperty();
            game.Do(new ProposeTrade(0, 1, Terms(100_000, 0, Atb), Terms(0, 1, Arcelor)));
            Assert.Equal(TurnPhase.TradeOffer, game.State.Phase);

            var result = game.Do(new AcceptTrade(1));

            Assert.Equal(1, game.State.Board[Atb].OwnerId);
            Assert.Equal(0, game.State.Board[Arcelor].OwnerId);
            Assert.Equal(GameRules.StartingBalance - 100_000, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 100_000, game.P(1).Balance);
            Assert.Equal(1, game.P(0).JailCards);
            Assert.Equal(0, game.P(1).JailCards);
            Assert.Contains(result.Events, e => e is TradeAccepted);
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
        }

        [Fact]
        public void Reject_ChangesNothing()
        {
            var game = WithProperty();
            game.Do(new ProposeTrade(0, 1, Terms(cells: Atb), Terms(cells: Arcelor)));

            game.Do(new RejectTrade(1));

            Assert.Equal(0, game.State.Board[Atb].OwnerId);
            Assert.Null(game.State.Trade);
        }

        [Fact]
        public void WhilePending_GameWaits_OnlyTargetAnswers_ProposerCancels()
        {
            var game = WithProperty();
            game.Do(new ProposeTrade(0, 1, Terms(cells: Atb), Terms(money: 50_000)));

            Assert.Equal("Чекаємо відповіді від гравця Богдан на пропозицію обміну.", game.Error(new RollDice(0)));
            Assert.Equal("Відповісти на обмін може лише той, кому його запропонували.", game.Error(new AcceptTrade(0)));
            Assert.Equal("Відкликати обмін може лише той, хто його запропонував.", game.Error(new CancelTrade(1)));

            game.Do(new CancelTrade(0));
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
        }

        [Fact]
        public void MortgagedCompany_StaysMortgaged()
        {
            var game = WithProperty();
            game.State.Board[Atb].IsMortgaged = true;
            game.Do(new ProposeTrade(0, 1, Terms(cells: Atb), Terms(money: 10_000)));

            game.Do(new AcceptTrade(1));

            Assert.True(game.State.Board[Atb].IsMortgaged);
            Assert.Equal(1, game.State.Board[Atb].OwnerId);
        }

        [Fact]
        public void Proposal_Checks()
        {
            var game = WithProperty();
            game.Give(0, Varus, Silpo);
            game.State.Board[Silpo].Level = 1;

            Assert.Equal("Обмін порожній: додайте компанії, гроші або картки.",
                game.Error(new ProposeTrade(0, 1, TradeTerms.Empty, TradeTerms.Empty)));
            Assert.Equal("Оберіть, з ким мінятися.", game.Error(new ProposeTrade(0, 0, Terms(money: 10_000), TradeTerms.Empty)));
            Assert.Equal("«АКБ» не можна обміняти: у групі є філії.",
                game.Error(new ProposeTrade(0, 1, Terms(cells: Atb), TradeTerms.Empty)));
            Assert.Equal("«Азовстіль» у гравця Богдан немає.",
                game.Error(new ProposeTrade(0, 1, TradeTerms.Empty, Terms(cells: Azovstal))));
            Assert.Equal($"Стільки грошей у вас немає: {M(1_500_000)}.",
                game.Error(new ProposeTrade(0, 1, Terms(money: 5_000_000), TradeTerms.Empty)));
            Assert.Equal("Стільки карток «Вийти з пєтушатні» у вас немає.",
                game.Error(new ProposeTrade(0, 1, Terms(jailCards: 1), TradeTerms.Empty)));
        }

        [Fact]
        public void Proposal_OnlyInOwnTurn()
        {
            var game = WithProperty();

            Assert.Equal("Зараз ходить Аня.", game.Error(new ProposeTrade(1, 0, Terms(cells: Arcelor), TradeTerms.Empty)));
            Assert.DoesNotContain(game.GetAvailableActions(1), a => a is ProposeTrade);
            Assert.Contains(game.GetAvailableActions(0), a => a is ProposeTrade);
        }
    }
}
