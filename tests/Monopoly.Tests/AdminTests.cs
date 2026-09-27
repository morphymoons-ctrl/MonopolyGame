using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Действия администратора (RULES.md, §14).
    public class AdminTests
    {
        private static ActionResult Admin(Game game, AdminAction action)
        {
            var result = game.ExecuteAdmin(action);
            Assert.True(result.Success, result.Error);
            return result;
        }

        [Fact]
        public void SetBalance_ChangesMoneySilently()
        {
            var game = Create();

            var result = Admin(game, new AdminSetBalance(1, 5_000_000));

            Assert.Equal(5_000_000, game.P(1).Balance);
            Assert.Empty(result.Events);
            Assert.Contains(new AdminSetBalance(1, 5_000_000), game.Actions);
        }

        [Fact]
        public void SetBalance_OutOfRange_Rejected()
        {
            var game = Create();

            Assert.NotNull(game.ExecuteAdmin(new AdminSetBalance(0, -1)).Error);
            Assert.NotNull(game.ExecuteAdmin(new AdminSetBalance(0, Game.AdminMaxBalance + 1)).Error);
            Assert.NotNull(game.ExecuteAdmin(new AdminSetBalance(7, 100)).Error);
            Assert.Empty(game.Actions);
        }

        [Fact]
        public void SetBalance_ClosesDebtWhenEnough()
        {
            // Аня на «Сільпо» Богдана без денег: долг.
            var game = Create(1, 2);
            game.Give(1, Silpo);
            game.P(0).Balance = 0;
            game.Give(0, Atb);
            game.Do(new RollDice(0));
            Assert.Equal(TurnPhase.Debt, game.State.Phase);

            var result = Admin(game, new AdminSetBalance(0, 1_000_000));

            Assert.Equal(TurnPhase.Manage, game.State.Phase);
            Assert.Contains(result.Events, e => e is DebtPaid);
        }

        [Fact]
        public void SetPrice_ChangesPurchaseRentAndMortgage()
        {
            var game = Create(1, 2);

            Admin(game, new AdminSetPrice(Silpo, 500_000));
            game.Do(new RollDice(0));
            game.Do(new BuyProperty(0));

            Assert.Equal(GameRules.StartingBalance - 500_000, game.P(0).Balance);
            Assert.Equal(60_000, GameRules.Rent(game.State.Board, Silpo, 0));
            Assert.Equal(250_000, game.State.Board[Silpo].MortgageValue);
            Assert.Equal(500_000, game.State.ToSnapshot().Cells[Silpo].Price);
        }

        [Fact]
        public void SetPrice_OwnedOrNotCompany_Rejected()
        {
            var game = Create();
            game.Give(1, Silpo);

            Assert.Contains("вже куплена", game.ExecuteAdmin(new AdminSetPrice(Silpo, 500_000)).Error);
            Assert.NotNull(game.ExecuteAdmin(new AdminSetPrice(Jail, 500_000)).Error);
            Assert.NotNull(game.ExecuteAdmin(new AdminSetPrice(Atb, 10)).Error);
        }

        [Fact]
        public void SetOwner_GivesAndReturnsToBank()
        {
            var game = Create();

            Admin(game, new AdminSetOwner(Silpo, 1));
            Assert.Equal(1, game.State.Board[Silpo].OwnerId);

            game.State.Board[Silpo].IsMortgaged = true;
            Admin(game, new AdminSetOwner(Silpo, 0));
            Assert.Equal(0, game.State.Board[Silpo].OwnerId);
            Assert.True(game.State.Board[Silpo].IsMortgaged);

            Admin(game, new AdminSetOwner(Silpo, null));
            Assert.Null(game.State.Board[Silpo].OwnerId);
            Assert.False(game.State.Board[Silpo].IsMortgaged);
        }

        [Fact]
        public void SetOwner_BranchesInGroupOrOnSale_Rejected()
        {
            var game = Create(1, 2);
            game.Give(1, Atb, Varus, Silpo);
            game.State.Board[Atb].Level = 1;
            Assert.Contains("філії", game.ExecuteAdmin(new AdminSetOwner(Silpo, 0)).Error);

            // Аня стоит на свободном «Arcelor» и решает, покупать ли.
            var sale = Create(2, 3);
            sale.Do(new RollDice(0));
            Assert.Equal(Arcelor, sale.State.PendingPurchase);
            Assert.Contains("продається", sale.ExecuteAdmin(new AdminSetOwner(Arcelor, 1)).Error);
        }

        [Fact]
        public void SetJail_PutsAndReleases()
        {
            var game = Create();

            Admin(game, new AdminSetJail(1, true));
            Assert.True(game.P(1).IsInJail);
            Assert.Equal(Jail, game.P(1).Position);

            Admin(game, new AdminSetJail(1, false));
            Assert.False(game.P(1).IsInJail);
            Assert.Equal(Jail, game.P(1).Position);
            Assert.NotNull(game.ExecuteAdmin(new AdminSetJail(1, false)).Error);
        }

        [Fact]
        public void SetJail_CurrentPlayerAfterDouble_NoMoreRolls()
        {
            var game = Create(1, 1);
            game.Give(1, Varus);
            game.Do(new RollDice(0));
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);

            Admin(game, new AdminSetJail(0, true));

            Assert.Equal(TurnPhase.Manage, game.State.Phase);
            Assert.NotNull(game.Error(new RollDice(0)));
        }

        [Fact]
        public void PlayerCannotSendAdminAction()
        {
            var game = Create();

            Assert.Equal("Це дія адміністратора.", game.Error(new AdminSetBalance(0, 9_000_000)));
            Assert.Equal(GameRules.StartingBalance, game.P(0).Balance);
        }

        [Fact]
        public void Replay_RestoresAdminChanges()
        {
            var game = Game.Start(new[] { "Аня", "Богдан" }, seed: 42);
            game.Do(new RollDice(game.State.CurrentPlayer.Id));
            Admin(game, new AdminSetBalance(1, 7_000_000));
            Admin(game, new AdminSetPrice(Ukrnafta, 900_000));

            var copy = Game.Replay(new[] { "Аня", "Богдан" }, 42, game.Actions);

            Assert.Equal(7_000_000, copy.P(1).Balance);
            Assert.Equal(900_000, copy.State.Board[Ukrnafta].Price);
        }

        [Fact]
        public void GameOver_NothingChanges()
        {
            var game = Create();
            game.P(1).Balance = 0;
            game.Give(0, Silpo);
            game.State.WinnerId = 0;

            Assert.Equal("Гру закінчено.", game.ExecuteAdmin(new AdminSetBalance(1, 100)).Error);
        }
    }
}
