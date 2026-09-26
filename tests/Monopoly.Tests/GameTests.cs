using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Старт партии, ход, проверка действий, покупка (RULES.md, §1, §3, §4, §7).
    public class GameTests
    {
        // --- Старт партии ---

        [Theory]
        [InlineData(1)]
        [InlineData(6)]
        public void Constructor_RejectsWrongPlayerCount(int count)
        {
            var names = Enumerable.Range(1, count).Select(i => $"Игрок {i}").ToArray();
            Assert.Throws<ArgumentException>(() => new Game(names, new ScriptedRandom(), Fixed));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        public void Constructor_AcceptsTwoToFivePlayers(int count)
        {
            var names = Enumerable.Range(1, count).Select(i => $"Игрок {i}").ToArray();

            var game = new Game(names, new ScriptedRandom(), Fixed);

            Assert.Equal(count, game.State.Players.Count);
        }

        [Fact]
        public void Constructor_RejectsEmptyName()
        {
            Assert.Throws<ArgumentException>(() => new Game(new[] { "Аня", " " }, new ScriptedRandom(), Fixed));
        }

        [Fact]
        public void NewGame_EveryoneAtStartWithStartingBalance()
        {
            var game = Create();

            Assert.All(game.State.Players, p =>
            {
                Assert.Equal(GameRules.StartingBalance, p.Balance);
                Assert.Equal(0, p.Position);
            });
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
            Assert.Same(game.P(0), game.State.CurrentPlayer);
            Assert.All(game.State.Board, cell => Assert.Null(cell.OwnerId));
            Assert.Equal(16, game.State.ChanceDeck.Count);
        }

        [Fact]
        public void NewGame_HistoryStartsWithGameStartedAndFirstTurn()
        {
            var game = Create();

            Assert.Collection(game.History,
                e => Assert.Equal(new[] { 0, 1 }, Assert.IsType<GameStarted>(e).TurnOrder),
                e => Assert.Equal(0, Assert.IsType<TurnStarted>(e).PlayerId));
        }

        [Fact]
        public void TurnOrder_IsPermutationOfPlayers()
        {
            var game = Game.Start(new[] { "А", "Б", "В", "Г", "Д" }, seed: 7);

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, game.State.Players.Select(p => p.Id).Order());
        }

        [Fact]
        public void SameSeed_GivesSameGame()
        {
            var names = new[] { "А", "Б", "В", "Г" };
            var first = Game.Start(names, seed: 12345);
            var second = Game.Start(names, seed: 12345);

            Bots.Play(first, new Random(1), maxActions: 300);
            Bots.Play(second, new Random(1), maxActions: 300);

            Assert.Equal(first.History, second.History, new EventComparer());
            Assert.Equal(12345, Assert.IsType<GameStarted>(first.History[0]).Seed);
        }

        // --- Бросок и движение ---

        [Fact]
        public void Roll_MovesBySumOfDice()
        {
            var game = Create(3, 5);

            var result = game.Do(new RollDice(0));

            Assert.Equal(Jail, game.P(0).Position);
            Assert.Equal(new DiceRoll(3, 5), game.State.LastRoll);
            Assert.Contains(new DiceRolled(0, 3, 5), result.Events);
            Assert.Contains(new PlayerMoved(0, 0, Jail), result.Events);
        }

        [Fact]
        public void Roll_OnlyOncePerTurn()
        {
            var game = Create(3, 5, 1, 2);
            game.Do(new RollDice(0));

            Assert.Equal("Кубики в цьому ході вже кинуто.", game.Error(new RollDice(0)));
            Assert.Equal(Jail, game.P(0).Position);
        }

        [Fact]
        public void PassingStart_Gives200()
        {
            var game = Create(2, 3);
            game.P(0).Position = 30;

            var result = game.Do(new RollDice(0));

            Assert.Equal(Silpo, game.P(0).Position);
            Assert.Equal(GameRules.StartingBalance + 200, game.P(0).Balance);
            Assert.Contains(new PassedStart(0, 200), result.Events);
        }

        [Fact]
        public void LandingOnStart_Gives200()
        {
            var game = Create(1, 3);
            game.P(0).Position = 28;

            game.Do(new RollDice(0));

            Assert.Equal(0, game.P(0).Position);
            Assert.Equal(GameRules.StartingBalance + 200, game.P(0).Balance);
        }

        [Fact]
        public void Double_GivesAnotherRoll()
        {
            var game = Create(4, 4, 1, 2);

            var first = game.Do(new RollDice(0));

            Assert.Contains(new RollAgain(0), first.Events);
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
            Assert.Equal("Випав дубль — киньте кубики ще раз.", game.Error(new EndTurn(0)));

            game.Do(new RollDice(0));
            Assert.Equal(Jail + 3, game.P(0).Position);
        }

        [Fact]
        public void Action_OutOfTurn_IsRejected()
        {
            var game = Create(3, 5);

            Assert.Equal("Зараз ходить Аня.", game.Error(new RollDice(1)));
            Assert.Equal(0, game.P(1).Position);
        }

        [Fact]
        public void Action_ByUnknownPlayer_IsRejected()
        {
            var game = Create(3, 5);

            Assert.Equal("Немає такого гравця.", game.Error(new RollDice(42)));
        }

        [Fact]
        public void RejectedAction_DoesNotChangeHistory()
        {
            var game = Create();
            int before = game.History.Count;

            game.Execute(new EndTurn(0));
            game.Execute(new BuyProperty(1));

            Assert.Equal(before, game.History.Count);
        }

        [Fact]
        public void CanExecute_MatchesExecute()
        {
            var game = Create(3, 5);

            Assert.True(game.CanExecute(new RollDice(0)));
            Assert.False(game.CanExecute(new RollDice(1)));
            Assert.False(game.CanExecute(new EndTurn(0)));
            Assert.False(game.CanExecute(new BuyProperty(0)));
        }

        [Fact]
        public void AvailableActions_FollowPhases()
        {
            var game = Create(1, 2);

            Assert.Equal(new[] { typeof(RollDice), typeof(ProposeTrade) }, Kinds(game.GetAvailableActions(0)));
            Assert.Empty(game.GetAvailableActions(1));

            game.Do(new RollDice(0));
            Assert.Equal(new GameAction[] { new BuyProperty(0), new DeclinePurchase(0) }, game.GetAvailableActions(0));

            game.Do(new BuyProperty(0));
            var available = game.GetAvailableActions(0);
            Assert.Contains(new EndTurn(0), available);
            Assert.Contains(new MortgageCompany(0, Silpo), available);
            Assert.DoesNotContain(new BuildBranch(0, Silpo), available);
            Assert.Contains(available, a => a is ProposeTrade);
        }

        [Fact]
        public void Snapshot_ReflectsState()
        {
            var game = Create(1, 2);
            game.Do(new RollDice(0));
            game.Do(new BuyProperty(0));

            var snapshot = game.State.ToSnapshot();

            Assert.Equal(0, snapshot.CurrentPlayerId);
            Assert.Equal(TurnPhase.Manage, snapshot.Phase);
            Assert.Equal(new DiceRoll(1, 2), snapshot.LastRoll);
            Assert.Equal(new CellSnapshot(0, 0, false), snapshot.Cells[Silpo]);
            Assert.Equal(Board.CellCount, snapshot.Cells.Count);
            Assert.Equal(new PlayerSnapshot(0, "Аня", GameRules.StartingBalance - 140, Silpo, false, false, 0, false),
                snapshot.FindPlayer(0));
        }

        // --- Покупка ---

        [Fact]
        public void LandingOnFreeCompany_OffersPurchase()
        {
            var game = Create(1, 2);

            var result = game.Do(new RollDice(0));

            Assert.Equal(TurnPhase.BuyDecision, game.State.Phase);
            Assert.Equal(Silpo, game.State.PendingPurchase);
            Assert.Contains(new PurchaseOffered(0, Silpo, 140), result.Events);
        }

        [Fact]
        public void BuyDecision_MustBeMadeBeforeEndingTurn()
        {
            var game = Create(1, 2);
            game.Do(new RollDice(0));

            Assert.Equal("Спершу вирішіть, чи купувати «Сільпо».", game.Error(new EndTurn(0)));
            Assert.Same(game.P(0), game.State.CurrentPlayer);
        }

        [Fact]
        public void Buy_TransfersCompanyForPrice()
        {
            var game = Create(1, 2);
            game.Do(new RollDice(0));

            var result = game.Do(new BuyProperty(0));

            Assert.Equal(0, game.State.Board[Silpo].OwnerId);
            Assert.Equal(GameRules.StartingBalance - 140, game.P(0).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
            Assert.Contains(new PropertyBought(0, Silpo, 140), result.Events);
        }

        [Fact]
        public void Buy_WithoutEnoughMoney_IsRejected_ButDeclineWorks()
        {
            var game = Create(1, 2);
            game.P(0).Balance = 100;
            game.Do(new RollDice(0));

            Assert.Equal("Не вистачає грошей: «Сільпо» коштує 140 грн, у вас 100 грн.", game.Error(new BuyProperty(0)));
            game.Do(new DeclinePurchase(0));
            Assert.Equal(TurnPhase.Auction, game.State.Phase);
        }

        [Fact]
        public void Buy_WhenNothingToBuy_IsRejected()
        {
            var game = Create(3, 5);

            Assert.Equal("Зараз нічого купувати.", game.Error(new BuyProperty(0)));
            game.Do(new RollDice(0));
            Assert.Equal("Зараз нічого купувати.", game.Error(new BuyProperty(0)));
        }

        // --- Конец хода и особые клетки ---

        [Fact]
        public void EndTurn_BeforeRoll_IsRejected()
        {
            var game = Create();

            Assert.Equal("Спершу киньте кубики.", game.Error(new EndTurn(0)));
        }

        [Fact]
        public void EndTurn_PassesTurnToNextPlayer_AndWrapsAround()
        {
            var game = Create(3, 5, 3, 5);

            game.Do(new RollDice(0));
            var first = game.Do(new EndTurn(0));

            Assert.Same(game.P(1), game.State.CurrentPlayer);
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
            Assert.Null(game.State.LastRoll);
            Assert.Contains(new TurnStarted(1), first.Events);

            game.Do(new RollDice(1));
            game.Do(new EndTurn(1));

            Assert.Same(game.P(0), game.State.CurrentPlayer);
        }

        [Fact]
        public void Rest_SkipsNextTurn()
        {
            // Аня: 12 → 17 «Отдых». Богдан ходит дважды подряд.
            var game = Create(2, 3, 3, 5, 3, 5);
            game.P(0).Position = Okko;

            var landing = game.Do(new RollDice(0));
            Assert.Contains(new RestStarted(0), landing.Events);

            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));
            var skip = game.Do(new EndTurn(1));

            Assert.Contains(new TurnSkipped(0), skip.Events);
            Assert.Same(game.P(1), game.State.CurrentPlayer);

            game.Do(new RollDice(1));
            game.Do(new EndTurn(1));
            Assert.Same(game.P(0), game.State.CurrentPlayer);
        }

        [Fact]
        public void JailCell_IsJustVisiting()
        {
            var game = Create(3, 5);

            game.Do(new RollDice(0));

            Assert.Equal(Jail, game.P(0).Position);
            Assert.False(game.P(0).IsInJail);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        private static IEnumerable<Type> Kinds(IEnumerable<GameAction> actions) => actions.Select(a => a.GetType());

        // Сравнение событий по содержимому: у записей со списками равенство списков по ссылке.
        internal sealed class EventComparer : IEqualityComparer<GameEvent>
        {
            public bool Equals(GameEvent? x, GameEvent? y) => x switch
            {
                GameStarted a when y is GameStarted b => a.Seed == b.Seed && a.TurnOrder.SequenceEqual(b.TurnOrder),
                TradeProposed or TradeAccepted => x.GetType() == y?.GetType(),
                _ => object.Equals(x, y),
            };

            public int GetHashCode(GameEvent obj) => obj.GetType().GetHashCode();
        }
    }
}
