using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Долги и банкротство (RULES.md, §12). Аня (0) встаёт на «Сильпо» Богдана (1): аренда 14.
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
            game.P(0).Balance = 10;
            game.Give(0, Atb);

            var roll = game.Do(new RollDice(0));

            Assert.Equal(TurnPhase.Debt, game.State.Phase);
            Assert.Contains(new DebtIncurred(0, 1, 14), roll.Events);
            Assert.Equal("Сначала закройте долг: продайте филиалы или заложите компании.", game.Error(new EndTurn(0)));
            Assert.Contains(new MortgageCompany(0, Atb), game.GetAvailableActions(0));

            var mortgage = game.Do(new MortgageCompany(0, Atb));

            Assert.Contains(new DebtPaid(0, 1, 14), mortgage.Events);
            Assert.Equal(10 + 50 - 14, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 14, game.P(1).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void CannotCover_BankruptToCreditor_AndGameOver()
        {
            var game = RentDue();
            game.P(0).Balance = 5;

            var result = game.Do(new RollDice(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Equal(0, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 5, game.P(1).Balance);
            Assert.Contains(new PlayerBankrupt(0, 1), result.Events);
            Assert.Contains(new GameOver(1), result.Events);
            Assert.Equal(TurnPhase.GameOver, game.State.Phase);
            Assert.Equal("Игра окончена.", game.Error(new RollDice(1)));
        }

        [Fact]
        public void BankruptToPlayer_PropertyGoesToCreditor_TurnPasses()
        {
            var game = RentDue(players: 3);
            game.P(0).Balance = 5;
            game.P(0).JailCards = 1;
            game.Give(0, Atb, Varus);
            game.State.Board[Atb].IsMortgaged = true;
            game.State.Board[Varus].IsMortgaged = true;

            var result = game.Do(new RollDice(0));

            Assert.Equal(1, game.State.Board[Atb].OwnerId);
            Assert.True(game.State.Board[Atb].IsMortgaged);
            Assert.Equal(1, game.P(1).JailCards);
            Assert.Contains(new TurnStarted(1), result.Events);
            Assert.Same(game.P(1), game.State.CurrentPlayer);
        }

        [Fact]
        public void BankruptToBank_CompaniesReturnFree()
        {
            // Аня: 20 → 24 «Шанс», «Подписка на стриминги» (−100). Наличных нет, собрать можно только 50 (залог «Розетки»).
            var game = CreateFor(3, 1, 3);
            game.P(0).Position = Olx;
            game.P(0).Balance = 0;
            game.P(0).JailCards = 1;
            game.Give(0, Atb, Rozetka);
            game.State.Board[Atb].IsMortgaged = true;
            game.PutOnTop(ChanceCard.Streaming);

            var result = game.Do(new RollDice(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Contains(new PlayerBankrupt(0, null), result.Events);
            Assert.All(new[] { Atb, Rozetka }, cell =>
            {
                Assert.Null(game.State.Board[cell].OwnerId);
                Assert.False(game.State.Board[cell].IsMortgaged);
            });
            Assert.Contains(ChanceCard.GetOutOfJail, game.State.ChanceDiscard);
        }

        [Fact]
        public void Bankruptcy_SellsBranchesForCreditor()
        {
            // Долг Богдану больше, чем можно собрать; филиал Ани продаётся за 25, деньги уходят Богдану.
            var game = RentDue(players: 3);
            game.State.Board[Silpo].Level = 5; // аренда 980
            game.P(0).Balance = 100;
            game.Give(0, Tet, 10, 11);
            game.State.Board[Tet].Level = 1;

            game.Do(new RollDice(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Equal(0, game.State.Board[Tet].Level);
            Assert.Equal(GameRules.StartingBalance + 100 + 25, game.P(1).Balance);
        }

        [Fact]
        public void DeclareBankruptcy_Voluntarily()
        {
            var game = RentDue(players: 3);
            game.P(0).Balance = 10;
            game.Give(0, Atb);
            game.Do(new RollDice(0));

            var result = game.Do(new DeclareBankruptcy(0));

            Assert.True(game.P(0).IsBankrupt);
            Assert.Equal(1, game.State.Board[Atb].OwnerId);
            Assert.Contains(new PlayerBankrupt(0, 1), result.Events);
        }

        [Fact]
        public void Bankruptcy_OnlyWithDebt()
        {
            var game = Create();

            Assert.Equal("Объявить банкротство можно, только когда нечем заплатить долг.", game.Error(new DeclareBankruptcy(0)));
        }

        [Fact]
        public void DebtOnSomeoneElsesTurn_Birthday()
        {
            // Аня: 20 → 24 «Шанс», «День рождения». У Богдана нет наличных, но есть АТБ; Вика платит сразу.
            var game = CreateFor(3, 1, 3);
            game.P(0).Position = Olx;
            game.PutOnTop(ChanceCard.Birthday);
            game.P(1).Balance = 0;
            game.Give(1, Atb);

            game.Do(new RollDice(0));

            Assert.Equal(TurnPhase.Debt, game.State.Phase);
            Assert.Equal(GameRules.StartingBalance - 20, game.P(2).Balance);
            Assert.Equal("Ждём, пока Богдан закроет долг.", game.Error(new EndTurn(0)));

            game.Do(new MortgageCompany(1, Atb));

            Assert.Equal(30, game.P(1).Balance);
            Assert.Equal(GameRules.StartingBalance + 40, game.P(0).Balance);
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
            Assert.Equal("Вы выбыли из игры.", game.Error(new RollDice(1)));
        }
    }
}
