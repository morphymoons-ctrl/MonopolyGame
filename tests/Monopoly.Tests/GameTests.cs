using Monopoly.Core;

namespace Monopoly.Tests
{
    public class GameTests
    {
        // Номера клеток из RULES.md, §2.
        private const int Silpo = 3;
        private const int Wog = 4;
        private const int Jail = 8;
        private const int Rest = 17;

        // Два игрока, порядок ходов без перемешивания; dice — значения кубиков по порядку.
        private static Game CreateGame(params int[] dice) =>
            new(new[] { "Аня", "Богдан" }, new ScriptedRandom(dice), shuffleTurnOrder: false);

        private static Player Anya(Game game) => game.State.FindPlayer(0)!;
        private static Player Bohdan(Game game) => game.State.FindPlayer(1)!;

        // --- Старт партии ---

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void Constructor_RejectsWrongPlayerCount(int count)
        {
            var names = Enumerable.Range(1, count).Select(i => $"Игрок {i}").ToArray();
            Assert.Throws<ArgumentException>(() => new Game(names, new ScriptedRandom(), shuffleTurnOrder: false));
        }

        [Fact]
        public void Constructor_RejectsEmptyName()
        {
            Assert.Throws<ArgumentException>(() => new Game(new[] { "Аня", " " }, new ScriptedRandom(), shuffleTurnOrder: false));
        }

        [Fact]
        public void NewGame_EveryoneAtStartWithStartingBalance()
        {
            var game = CreateGame();

            Assert.All(game.State.Players, p =>
            {
                Assert.Equal(GameRules.StartingBalance, p.Balance);
                Assert.Equal(0, p.Position);
            });
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
            Assert.Same(Anya(game), game.State.CurrentPlayer);
            Assert.All(game.State.Board, cell => Assert.Null(cell.OwnerId));
        }

        [Fact]
        public void NewGame_HistoryStartsWithGameStartedAndFirstTurn()
        {
            var game = CreateGame();

            Assert.Collection(game.History,
                e => Assert.Equal(new[] { 0, 1 }, Assert.IsType<GameStarted>(e).TurnOrder),
                e => Assert.Equal(0, Assert.IsType<TurnStarted>(e).PlayerId));
        }

        [Fact]
        public void TurnOrder_IsPermutationOfPlayers()
        {
            var game = Game.Start(new[] { "А", "Б", "В", "Г" }, seed: 7);

            Assert.Equal(new[] { 0, 1, 2, 3 }, game.State.Players.Select(p => p.Id).Order());
        }

        [Fact]
        public void SameSeed_GivesSameGame()
        {
            var names = new[] { "А", "Б", "В", "Г" };
            var first = Game.Start(names, seed: 12345);
            var second = Game.Start(names, seed: 12345);

            for (int turn = 0; turn < 8; turn++)
            {
                foreach (var game in new[] { first, second })
                {
                    int id = game.State.CurrentPlayer.Id;
                    game.Execute(new RollDice(id));
                    game.Execute(new DeclinePurchase(id));
                    game.Execute(new EndTurn(id));
                }
            }

            Assert.Equal(first.History, second.History, new EventComparer());
            Assert.Equal(12345, Assert.IsType<GameStarted>(first.History[0]).Seed);
        }

        // --- Бросок и движение ---

        [Fact]
        public void Roll_MovesBySumOfDice()
        {
            var game = CreateGame(3, 5);

            var result = game.Execute(new RollDice(0));

            Assert.True(result.Success);
            Assert.Equal(8, Anya(game).Position);
            Assert.Equal(new DiceRoll(3, 5), game.State.LastRoll);
            Assert.Contains(new DiceRolled(0, 3, 5), result.Events);
            Assert.Contains(new PlayerMoved(0, 0, 8), result.Events);
        }

        [Fact]
        public void Roll_WrapsAroundBoard()
        {
            var game = CreateGame(2, 3);
            Anya(game).Position = 30;

            game.Execute(new RollDice(0));

            Assert.Equal(3, Anya(game).Position);
        }

        [Fact]
        public void Roll_OnlyOncePerTurn()
        {
            var game = CreateGame(3, 5, 1, 1);
            game.Execute(new RollDice(0));

            var result = game.Execute(new RollDice(0));

            Assert.False(result.Success);
            Assert.Equal("Кубики в этом ходу уже брошены.", result.Error);
            Assert.Equal(8, Anya(game).Position);
        }

        [Fact]
        public void Action_OutOfTurn_IsRejected()
        {
            var game = CreateGame(3, 5);

            var result = game.Execute(new RollDice(1));

            Assert.False(result.Success);
            Assert.Equal("Сейчас ходит Аня.", result.Error);
            Assert.Equal(0, Bohdan(game).Position);
        }

        [Fact]
        public void Action_ByUnknownPlayer_IsRejected()
        {
            var game = CreateGame(3, 5);

            var result = game.Execute(new RollDice(42));

            Assert.False(result.Success);
            Assert.Equal("Нет такого игрока.", result.Error);
        }

        [Fact]
        public void RejectedAction_DoesNotChangeHistory()
        {
            var game = CreateGame();
            int before = game.History.Count;

            game.Execute(new EndTurn(0));
            game.Execute(new BuyProperty(1));

            Assert.Equal(before, game.History.Count);
        }

        [Fact]
        public void CanExecute_MatchesExecute()
        {
            var game = CreateGame(3, 5);

            Assert.True(game.CanExecute(new RollDice(0)));
            Assert.False(game.CanExecute(new RollDice(1)));
            Assert.False(game.CanExecute(new EndTurn(0)));
            Assert.False(game.CanExecute(new BuyProperty(0)));
        }

        // --- Покупка ---

        [Fact]
        public void LandingOnFreeCompany_OffersPurchase()
        {
            var game = CreateGame(1, 2);

            var result = game.Execute(new RollDice(0));

            Assert.Equal(TurnPhase.BuyDecision, game.State.Phase);
            Assert.Contains(new PurchaseOffered(0, Silpo, 140), result.Events);
        }

        [Fact]
        public void BuyDecision_MustBeMadeBeforeEndingTurn()
        {
            var game = CreateGame(1, 2);
            game.Execute(new RollDice(0));

            var result = game.Execute(new EndTurn(0));

            Assert.False(result.Success);
            Assert.Equal("Сначала решите, покупать ли «Сильпо».", result.Error);
            Assert.Same(Anya(game), game.State.CurrentPlayer);
        }

        [Fact]
        public void Buy_TransfersCompanyForPrice()
        {
            var game = CreateGame(1, 2);
            game.Execute(new RollDice(0));

            var result = game.Execute(new BuyProperty(0));

            Assert.True(result.Success);
            Assert.Equal(0, game.State.Board[Silpo].OwnerId);
            Assert.Equal(GameRules.StartingBalance - 140, Anya(game).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
            Assert.Contains(new PropertyBought(0, Silpo, 140), result.Events);
        }

        [Fact]
        public void Buy_WithoutEnoughMoney_IsRejected_ButDeclineWorks()
        {
            var game = CreateGame(1, 2);
            Anya(game).Balance = 100;
            game.Execute(new RollDice(0));

            var buy = game.Execute(new BuyProperty(0));
            var decline = game.Execute(new DeclinePurchase(0));

            Assert.Equal("Не хватает денег: «Сильпо» стоит 140 грн, у вас 100 грн.", buy.Error);
            Assert.True(decline.Success);
            Assert.Equal(100, Anya(game).Balance);
        }

        [Fact]
        public void Decline_LeavesCompanyWithBank()
        {
            var game = CreateGame(1, 2);
            game.Execute(new RollDice(0));

            var result = game.Execute(new DeclinePurchase(0));

            Assert.True(result.Success);
            Assert.Null(game.State.Board[Silpo].OwnerId);
            Assert.Equal(GameRules.StartingBalance, Anya(game).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void Buy_WhenNothingToBuy_IsRejected()
        {
            var game = CreateGame(3, 5);

            Assert.Equal("Сейчас нечего покупать.", game.Execute(new BuyProperty(0)).Error);
            game.Execute(new RollDice(0));
            Assert.Equal("Сейчас нечего покупать.", game.Execute(new BuyProperty(0)).Error);
        }

        [Fact]
        public void Buy_AfterDecision_IsRejected()
        {
            var game = CreateGame(1, 2);
            game.Execute(new RollDice(0));
            game.Execute(new DeclinePurchase(0));

            Assert.False(game.Execute(new BuyProperty(0)).Success);
        }

        // --- Аренда ---

        [Fact]
        public void LandingOnOthersCompany_PaysTenPercentRent()
        {
            var game = CreateGame(1, 3);
            game.State.Board[Wog].OwnerId = 1;

            var result = game.Execute(new RollDice(0));

            Assert.Equal(GameRules.StartingBalance - 16, Anya(game).Balance);
            Assert.Equal(GameRules.StartingBalance + 16, Bohdan(game).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
            Assert.Contains(new RentPaid(0, 1, Wog, 16), result.Events);
        }

        [Fact]
        public void LandingOnOwnCompany_PaysNothing()
        {
            var game = CreateGame(1, 3);
            game.State.Board[Wog].OwnerId = 0;

            game.Execute(new RollDice(0));

            Assert.Equal(GameRules.StartingBalance, Anya(game).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void Rent_NeverMakesBalanceNegative()
        {
            var game = CreateGame(1, 3);
            game.State.Board[Wog].OwnerId = 1;
            Anya(game).Balance = 5;

            game.Execute(new RollDice(0));

            Assert.Equal(0, Anya(game).Balance);
            Assert.Equal(GameRules.StartingBalance + 5, Bohdan(game).Balance);
        }

        // --- Конец хода ---

        [Fact]
        public void EndTurn_BeforeRoll_IsRejected()
        {
            var game = CreateGame();

            Assert.Equal("Сначала бросьте кубики.", game.Execute(new EndTurn(0)).Error);
        }

        [Fact]
        public void EndTurn_PassesTurnToNextPlayer_AndWrapsAround()
        {
            var game = CreateGame(3, 5, 3, 5);

            game.Execute(new RollDice(0));
            var first = game.Execute(new EndTurn(0));

            Assert.Same(Bohdan(game), game.State.CurrentPlayer);
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
            Assert.Null(game.State.LastRoll);
            Assert.Contains(new TurnStarted(1), first.Events);

            game.Execute(new RollDice(1));
            game.Execute(new EndTurn(1));

            Assert.Same(Anya(game), game.State.CurrentPlayer);
        }

        // --- Особые клетки ---

        [Fact]
        public void Rest_SkipsNextTurn()
        {
            // Аня: 12 → 17 «Отдых». Богдан ходит дважды подряд.
            var game = CreateGame(2, 3, 3, 5, 3, 5, 1, 1);
            Anya(game).Position = 12;

            var landing = game.Execute(new RollDice(0));
            Assert.Equal(Rest, Anya(game).Position);
            Assert.Contains(new RestStarted(0), landing.Events);

            game.Execute(new EndTurn(0));
            game.Execute(new RollDice(1));
            var skip = game.Execute(new EndTurn(1));

            Assert.Contains(new TurnSkipped(0), skip.Events);
            Assert.Same(Bohdan(game), game.State.CurrentPlayer);
            Assert.False(Anya(game).IsResting);

            game.Execute(new RollDice(1));
            game.Execute(new EndTurn(1));
            Assert.Same(Anya(game), game.State.CurrentPlayer);
        }

        [Fact]
        public void JailCell_IsJustVisiting()
        {
            var game = CreateGame(3, 5);

            game.Execute(new RollDice(0));

            Assert.Equal(Jail, Anya(game).Position);
            Assert.False(Anya(game).IsInJail);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        // Сравнение событий по содержимому: GameStarted хранит список, а у записей равенство списков по ссылке.
        private sealed class EventComparer : IEqualityComparer<GameEvent>
        {
            public bool Equals(GameEvent? x, GameEvent? y) => x switch
            {
                GameStarted a when y is GameStarted b => a.Seed == b.Seed && a.TurnOrder.SequenceEqual(b.TurnOrder),
                _ => object.Equals(x, y),
            };

            public int GetHashCode(GameEvent obj) => obj.GetType().GetHashCode();
        }
    }
}
