using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Колода «Шанса» (RULES.md, §9). Аня (0) идёт со «Стрипклуба» (20) на «Шанс» (24) броском 1 + 3.
    public class ChanceTests
    {
        private static Game Drawing(ChanceCard card, int players = 3)
        {
            var game = CreateFor(players, 1, 3);
            game.P(0).Position = StripClub;
            game.PutOnTop(card);
            return game;
        }

        [Fact]
        public void Deck_HasSeventeenDifferentCards()
        {
            Assert.Equal(17, Enum.GetValues<ChanceCard>().Length);
        }

        [Theory]
        [InlineData(ChanceCard.TaxRefund, 150)]
        [InlineData(ChanceCard.ProjectBonus, 100)]
        [InlineData(ChanceCard.DancerRefund, 60)]
        [InlineData(ChanceCard.Cashback, 50)]
        [InlineData(ChanceCard.ParkingFine, -50)]
        [InlineData(ChanceCard.Utilities, -80)]
        [InlineData(ChanceCard.Streaming, -100)]
        [InlineData(ChanceCard.MassageFinish, -50)]
        public void MoneyCards(ChanceCard card, int change)
        {
            var game = Drawing(card);

            var result = game.Do(new RollDice(0));

            Assert.Contains(new ChanceCardDrawn(0, card), result.Events);
            Assert.Equal(GameRules.StartingBalance + change, game.P(0).Balance);
        }

        [Fact]
        public void Birthday_EveryonePaysTwenty()
        {
            var game = Drawing(ChanceCard.Birthday);

            game.Do(new RollDice(0));

            Assert.Equal(GameRules.StartingBalance + 40, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance - 20, game.P(1).Balance);
            Assert.Equal(GameRules.StartingBalance - 20, game.P(2).Balance);
        }

        [Fact]
        public void Charity_PayEveryoneTwentyFive()
        {
            var game = Drawing(ChanceCard.Charity);

            game.Do(new RollDice(0));

            Assert.Equal(GameRules.StartingBalance - 50, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 25, game.P(1).Balance);
        }

        [Fact]
        public void TaxAudit_BranchesAndHeadOffices()
        {
            var game = Drawing(ChanceCard.TaxAudit);
            game.Give(0, Atb, Varus, Silpo);
            game.State.Board[Atb].Level = 2;
            game.State.Board[Varus].Level = 2;
            game.State.Board[Silpo].Level = 5;

            game.Do(new RollDice(0));

            Assert.Equal(GameRules.StartingBalance - (4 * 25 + 100), game.P(0).Balance);
        }

        [Fact]
        public void GoToStart_Gives200()
        {
            var game = Drawing(ChanceCard.GoToStart);

            game.Do(new RollDice(0));

            Assert.Equal(0, game.P(0).Position);
            Assert.Equal(GameRules.StartingBalance + 200, game.P(0).Balance);
        }

        [Fact]
        public void NovaPoshta_PassesStart_AndOffersPurchase()
        {
            var game = Drawing(ChanceCard.NovaPoshta);

            game.Do(new RollDice(0));

            Assert.Equal(NovaPoshta, game.P(0).Position);
            Assert.Equal(GameRules.StartingBalance + 200, game.P(0).Balance);
            Assert.Equal(TurnPhase.BuyDecision, game.State.Phase);
        }

        [Fact]
        public void Taxi_NearestGasStation_DoubleRent()
        {
            var game = Drawing(ChanceCard.Taxi);
            game.Give(1, Ukrnafta);

            var result = game.Do(new RollDice(0));

            Assert.Equal(Ukrnafta, game.P(0).Position);
            Assert.Contains(new RentPaid(0, 1, Ukrnafta, 50), result.Events);
        }

        [Fact]
        public void Train_ThreeBack_NoStartBonus()
        {
            var game = Drawing(ChanceCard.Train);

            game.Do(new RollDice(0));

            Assert.Equal(Upg, game.P(0).Position);
            Assert.Equal(GameRules.StartingBalance, game.P(0).Balance);
            Assert.Equal(TurnPhase.BuyDecision, game.State.Phase);
        }

        [Fact]
        public void GoToJail_EvenAfterDouble_NoExtraRoll()
        {
            var game = CreateFor(3, 2, 2);
            game.P(0).Position = StripClub;
            game.PutOnTop(ChanceCard.GoToJail);

            var result = game.Do(new RollDice(0));

            Assert.True(game.P(0).IsInJail);
            Assert.Equal(Jail, game.P(0).Position);
            Assert.Contains(new SentToJail(0, JailReason.Card), result.Events);
            Assert.DoesNotContain(result.Events, e => e is RollAgain);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void GetOutOfJail_IsKeptByPlayer()
        {
            var game = Drawing(ChanceCard.GetOutOfJail);

            game.Do(new RollDice(0));

            Assert.Equal(1, game.P(0).JailCards);
            Assert.DoesNotContain(ChanceCard.GetOutOfJail, game.State.ChanceDeck);
            Assert.DoesNotContain(ChanceCard.GetOutOfJail, game.State.ChanceDiscard);
        }

        [Fact]
        public void UsedCards_GoToDiscard_AndComeBackWhenDeckEnds()
        {
            var game = Drawing(ChanceCard.Cashback);
            game.State.ChanceDeck.Clear();
            game.State.ChanceDiscard.Add(ChanceCard.DancerRefund);

            var result = game.Do(new RollDice(0));

            Assert.Contains(new ChanceCardDrawn(0, ChanceCard.DancerRefund), result.Events);
            Assert.Equal(new[] { ChanceCard.DancerRefund }, game.State.ChanceDiscard);
        }
    }

    // Случайные партии ботов: движок не падает, не зависает и держит инварианты.
    // «Бедные» партии (мало денег на старте) проверяют долги, банкротства и конец игры.
    public class FuzzTests
    {
        [Theory]
        [InlineData(1, 2, 1500)]
        [InlineData(2, 3, 1500)]
        [InlineData(3, 4, 1500)]
        [InlineData(4, 5, 1500)]
        [InlineData(5, 2, 150)]
        [InlineData(6, 3, 150)]
        [InlineData(7, 4, 150)]
        [InlineData(8, 5, 150)]
        [InlineData(9, 5, 150)]
        public void RandomGames_KeepInvariants(int seed, int players, int startingBalance)
        {
            var names = Enumerable.Range(1, players).Select(i => $"Бот {i}").ToArray();
            var game = Game.Start(names, seed);
            foreach (var player in game.State.Players)
                player.Balance = startingBalance;

            Bots.Play(game, new Random(seed), maxActions: 4000, CheckInvariants);
        }

        [Fact]
        public void PoorGames_ReachDebtsBankruptcyAndWinner()
        {
            var seen = new HashSet<Type>();
            int finished = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var game = Game.Start(new[] { "А", "Б", "В" }, seed);
                foreach (var player in game.State.Players)
                    player.Balance = 150;
                Bots.Play(game, new Random(seed), maxActions: 4000);
                if (game.State.Phase == TurnPhase.GameOver)
                    finished++;
                seen.UnionWith(game.History.Select(e => e.GetType()));
            }

            Assert.True(finished > 0, "ни одна «бедная» партия не закончилась");
            Assert.Contains(typeof(DebtIncurred), seen);
            Assert.Contains(typeof(DebtPaid), seen);
            Assert.Contains(typeof(PlayerBankrupt), seen);
            Assert.Contains(typeof(GameOver), seen);
        }

        private static void CheckInvariants(Game game)
        {
            var state = game.State;
            Assert.All(state.Players, p => Assert.True(p.Balance >= 0, $"{p.Name}: баланс {p.Balance}"));
            Assert.All(state.Players.Where(p => p.IsBankrupt), p =>
            {
                Assert.Equal(0, p.Balance);
                Assert.Empty(state.CellsOf(p.Id));
            });
            foreach (var cell in state.Board)
            {
                Assert.InRange(cell.Level, 0, GameRules.HeadOfficeLevel);
                if (cell.Level > 0)
                {
                    Assert.True(GameRules.IsMonopoly(state.Board, cell.Type, cell.OwnerId!.Value));
                    Assert.DoesNotContain(state.GroupOf(cell), c => c.IsMortgaged);
                    Assert.True(state.GroupOf(cell).Max(c => c.Level) - state.GroupOf(cell).Min(c => c.Level) <= 1);
                }
                if (cell.OwnerId is null)
                    Assert.False(cell.IsMortgaged);
            }
            // Все карточки «Выйти из тюрьмы» где-то есть — ровно одна.
            int jailCards = state.Players.Sum(p => p.JailCards)
                + state.ChanceDeck.Count(c => c == ChanceCard.GetOutOfJail)
                + state.ChanceDiscard.Count(c => c == ChanceCard.GetOutOfJail);
            Assert.Equal(1, jailCards);
            Assert.Equal(Enum.GetValues<ChanceCard>().Length, state.ChanceDeck.Count + state.ChanceDiscard.Count + state.Players.Sum(p => p.JailCards));

            if (state.Phase != TurnPhase.GameOver)
            {
                Assert.True(state.ActivePlayers.Count() >= 2);
                Assert.False(state.CurrentPlayer.IsBankrupt);
            }
        }
    }
}
