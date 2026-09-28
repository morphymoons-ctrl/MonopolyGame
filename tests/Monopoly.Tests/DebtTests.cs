using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Долги и банкротство (RULES.md, §12). Аня (0) встаёт на «Сілько» Богдана (1): аренда 16 800.
    public class DebtTests
    {
        private static Game RentDue(int players = 2)
        {
            var game = CreateFor(players, 1, 2);
            game.Give(1, Silpo);
            return game;
        }

        [Fact]
        public void NotEnoughCash_ButCanMortgage_BecomesDebt_PaidAutomatically()
        {
            var game = RentDue();
            game.P(0).Balance = 10_000;
            game.Give(0, Atb);

            var roll = game.Do(new RollDice(0));

            Assert.Equal(TurnPhase.Debt, game.State.Phase);
            Assert.Contains(new DebtIncurred(0, 1, 16_800), roll.Events);
            Assert.Equal("Спершу закрийте борг: продайте філії або закладіть компанії.", game.Error(new EndTurn(0)));
            Assert.Contains(new MortgageCompany(0, Atb), game.GetAvailableActions(0));

            var mortgage = game.Do(new MortgageCompany(0, Atb));

            Assert.Contains(new DebtPaid(0, 1, 16_800), mortgage.Events);
            Assert.Equal(10_000 + 50_000 - 16_800, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 16_800, game.P(1).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        // Должник может предложить обмен, чтобы собрать деньги (§11); принятый обмен сам закрывает долг.
        [Fact]
        public void Debtor_CanProposeTrade_AcceptedTradeClosesDebt()
        {
            var game = RentDue(players: 3);
            game.P(0).Balance = 10_000;
            game.Give(0, Atb);
            game.Do(new RollDice(0));
            Assert.Equal(TurnPhase.Debt, game.State.Phase);
            Assert.Contains(game.GetAvailableActions(0), a => a is ProposeTrade);

            var offer = new ProposeTrade(0, 2, new TradeTerms(new[] { Atb }, 0, 0), new TradeTerms(Array.Empty<int>(), 50_000, 0));
            Assert.NotNull(game.Error(offer with { PlayerId = 1, TargetId = 2 }));
            game.Do(offer);
            Assert.Equal(TurnPhase.TradeOffer, game.State.Phase);
            Assert.Equal(new[] { 2 }, game.AwaitedPlayers());

            var accepted = game.Do(new AcceptTrade(2));

            Assert.Contains(new DebtPaid(0, 1, 16_800), accepted.Events);
            Assert.Equal(2, game.State.Board[Atb].OwnerId);
            Assert.Equal(10_000 + 50_000 - 16_800, game.P(0).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void RejectedTrade_BackToDebt()
        {
            var game = RentDue(players: 3);
            game.P(0).Balance = 10_000;
            game.Give(0, Atb);
            game.Do(new RollDice(0));

            game.Do(new ProposeTrade(0, 2, new TradeTerms(new[] { Atb }, 0, 0), new TradeTerms(Array.Empty<int>(), 50_000, 0)));
            game.Do(new RejectTrade(2));

            Assert.Equal(TurnPhase.Debt, game.State.Phase);
            Assert.Equal(new[] { 0 }, game.AwaitedPlayers());
        }

        [Fact]
        public void CannotCover_BankruptToCreditor_AndGameOver()
        {
            var game = RentDue();
            game.P(0).Balance = 5_000;

            var result = game.Do(new RollDice(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Equal(0, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 5_000, game.P(1).Balance);
            Assert.Contains(new PlayerBankrupt(0, 1), result.Events);
            Assert.Contains(new GameOver(1), result.Events);
            Assert.Equal(TurnPhase.GameOver, game.State.Phase);
            Assert.Equal("Гру закінчено.", game.Error(new RollDice(1)));
        }

        [Fact]
        public void BankruptToPlayer_CompaniesReturnToBank_MoneyAndCardsToCreditor()
        {
            var game = RentDue(players: 3);
            game.P(0).Balance = 5_000;
            game.P(0).JailCards = 1;
            game.Give(0, Atb, Varus);
            game.State.Board[Atb].IsMortgaged = true;
            game.State.Board[Varus].IsMortgaged = true;

            var result = game.Do(new RollDice(0));

            // Компании кредитору не достаются — снова свободны для покупки (§12).
            Assert.All(new[] { Atb, Varus }, cell =>
            {
                Assert.Null(game.State.Board[cell].OwnerId);
                Assert.False(game.State.Board[cell].IsMortgaged);
            });
            Assert.Equal(GameRules.StartingBalance + 5_000, game.P(1).Balance);
            Assert.Equal(1, game.P(1).JailCards);
            Assert.Contains(new TurnStarted(1), result.Events);
            Assert.Same(game.P(1), game.State.CurrentPlayer);
        }

        [Fact]
        public void BankruptToBank_CompaniesReturnFree()
        {
            // Аня: 20 → 24 «Шанс», «Подписка на стриминги» (−100 000). Наличных нет, собрать можно только 50 000 (залог «Масажки»).
            var game = CreateFor(3, 1, 3);
            game.P(0).Position = FourBeforeChance;
            game.P(0).Balance = 0;
            game.P(0).JailCards = 1;
            game.Give(0, Atb, Massage);
            game.State.Board[Atb].IsMortgaged = true;
            game.PutOnTop(ChanceCard.Streaming);

            var result = game.Do(new RollDice(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Contains(new PlayerBankrupt(0, null), result.Events);
            Assert.All(new[] { Atb, Massage }, cell =>
            {
                Assert.Null(game.State.Board[cell].OwnerId);
                Assert.False(game.State.Board[cell].IsMortgaged);
            });
            Assert.Contains(ChanceCard.GetOutOfJail, game.State.ChanceDiscard);
        }

        [Fact]
        public void Bankruptcy_SellsBranchesForCreditor()
        {
            // Долг Богдану больше, чем можно собрать; филиал Ани продаётся за 75 000, деньги уходят Богдану.
            var game = RentDue(players: 3);
            game.State.Board[Silpo].Level = 5; // аренда 924 000
            game.P(0).Balance = 100_000;
            game.Give(0, Tet, 10, 11);
            game.State.Board[Tet].Level = 1;

            game.Do(new RollDice(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Equal(0, game.State.Board[Tet].Level);
            Assert.Null(game.State.Board[Tet].OwnerId);
            Assert.Equal(GameRules.StartingBalance + 100_000 + 75_000, game.P(1).Balance);
        }

        [Fact]
        public void DeclareBankruptcy_Voluntarily()
        {
            var game = RentDue(players: 3);
            game.P(0).Balance = 10_000;
            game.Give(0, Atb);
            game.Do(new RollDice(0));

            var result = game.Do(new DeclareBankruptcy(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Null(game.State.Board[Atb].OwnerId);
            Assert.Contains(new PlayerBankrupt(0, 1), result.Events);
        }

        [Fact]
        public void Bankruptcy_OnlyWithDebt()
        {
            var game = Create();

            Assert.Equal("Оголосити банкрутство можна, лише коли нема чим сплатити борг.", game.Error(new DeclareBankruptcy(0)));
        }

        [Fact]
        public void DebtOnSomeoneElsesTurn_Birthday()
        {
            // Аня: 20 → 24 «Шанс», «День рождения». У Богдана нет наличных, но есть АКБ; Вика платит сразу.
            var game = CreateFor(3, 1, 3);
            game.P(0).Position = FourBeforeChance;
            game.PutOnTop(ChanceCard.Birthday);
            game.P(1).Balance = 0;
            game.Give(1, Atb);

            game.Do(new RollDice(0));

            Assert.Equal(TurnPhase.Debt, game.State.Phase);
            Assert.Equal(GameRules.StartingBalance - 20_000, game.P(2).Balance);
            Assert.Equal("Чекаємо, поки Богдан закриє борг.", game.Error(new EndTurn(0)));

            game.Do(new MortgageCompany(1, Atb));

            Assert.Equal(30_000, game.P(1).Balance);
            Assert.Equal(GameRules.StartingBalance + 40_000, game.P(0).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void BankruptPlayer_IsSkipped()
        {
            var game = CreateFor(3, 1, 2);
            game.P(1).IsBankrupt = true;
            game.Do(new RollDice(0));
            game.Do(new BuyProperty(0));

            game.Do(new EndTurn(0));

            Assert.Same(game.P(2), game.State.CurrentPlayer);
            Assert.Equal("Ви вибули з гри.", game.Error(new RollDice(1)));
        }
    }
}
