namespace Monopoly.Core
{
    // Движок: принимает действия игроков, проверяет их по правилам и меняет состояние.
    public class Game
    {
        private readonly IRandomSource random;
        private readonly List<GameEvent> history = new();

        public GameState State { get; }
        // Все события с начала партии.
        public IReadOnlyList<GameEvent> History => history;

        // Новая партия. Без seed зерно выбирается случайно и попадает в событие GameStarted.
        public static Game Start(IReadOnlyList<string> playerNames, int? seed = null)
        {
            return new Game(playerNames, new SeededRandom(seed ?? Random.Shared.Next()));
        }

        public Game(IReadOnlyList<string> playerNames, IRandomSource random, bool shuffleTurnOrder = true)
        {
            ArgumentNullException.ThrowIfNull(playerNames);
            if (playerNames.Count < GameRules.MinPlayers || playerNames.Count > GameRules.MaxPlayers)
                throw new ArgumentException($"Нужно от {GameRules.MinPlayers} до {GameRules.MaxPlayers} игроков.", nameof(playerNames));
            if (playerNames.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("У каждого игрока должно быть имя.", nameof(playerNames));

            this.random = random ?? throw new ArgumentNullException(nameof(random));

            var players = playerNames.Select((name, i) => new Player(i, name)).ToList();
            // Порядок ходов — случайный при старте партии (RULES.md, §1).
            if (shuffleTurnOrder)
                Shuffle(players);

            State = new GameState(Board.CreateDefault(), players);
            history.Add(new GameStarted((random as SeededRandom)?.Seed, players.Select(p => p.Id).ToList()));
            history.Add(new TurnStarted(State.CurrentPlayer.Id));
        }

        public bool CanExecute(GameAction action) => Validate(action) is null;

        public ActionResult Execute(GameAction action)
        {
            ArgumentNullException.ThrowIfNull(action);

            var error = Validate(action);
            if (error is not null)
                return ActionResult.Fail(error);

            var player = State.CurrentPlayer;
            var events = new List<GameEvent>();
            switch (action)
            {
                case RollDice:
                    Roll(player, events);
                    break;
                case BuyProperty:
                    Buy(player, events);
                    break;
                case DeclinePurchase:
                    events.Add(new PurchaseDeclined(player.Id, player.Position));
                    // Аукцион после отказа — этап 3.
                    State.Phase = TurnPhase.Manage;
                    break;
                case EndTurn:
                    PassTurn(events);
                    break;
            }

            history.AddRange(events);
            return ActionResult.Ok(events);
        }

        // Проверка «чей ход? можно ли это сейчас?». null — действие допустимо.
        private string? Validate(GameAction action)
        {
            var player = State.FindPlayer(action.PlayerId);
            if (player is null)
                return "Нет такого игрока.";
            if (player != State.CurrentPlayer)
                return $"Сейчас ходит {State.CurrentPlayer.Name}.";

            var cell = State.CurrentCell;
            string pendingPurchase = $"Сначала решите, покупать ли «{cell.Name}».";

            return action switch
            {
                RollDice => State.Phase switch
                {
                    TurnPhase.AwaitingRoll => null,
                    TurnPhase.BuyDecision => pendingPurchase,
                    _ => "Кубики в этом ходу уже брошены.",
                },
                BuyProperty when State.Phase != TurnPhase.BuyDecision => "Сейчас нечего покупать.",
                BuyProperty when player.Balance < cell.Price =>
                    $"Не хватает денег: «{cell.Name}» стоит {cell.Price} грн, у вас {player.Balance} грн.",
                BuyProperty => null,
                DeclinePurchase => State.Phase == TurnPhase.BuyDecision ? null : "Сейчас нечего покупать.",
                EndTurn => State.Phase switch
                {
                    TurnPhase.AwaitingRoll => "Сначала бросьте кубики.",
                    TurnPhase.BuyDecision => pendingPurchase,
                    _ => null,
                },
                _ => "Неизвестное действие.",
            };
        }

        private void Roll(Player player, List<GameEvent> events)
        {
            // Дубли — этап 3; пока кубики бросаются ровно один раз за ход.
            var roll = new DiceRoll(random.Next(1, 7), random.Next(1, 7));
            State.LastRoll = roll;
            events.Add(new DiceRolled(player.Id, roll.Die1, roll.Die2));

            int from = player.Position;
            // +200 за «Старт» — этап 3.
            player.Position = (from + roll.Total) % State.Board.Count;
            events.Add(new PlayerMoved(player.Id, from, player.Position));

            ResolveCell(player, events);
        }

        // Действие клетки, на которую встал игрок.
        private void ResolveCell(Player player, List<GameEvent> events)
        {
            var cell = State.Board[player.Position];
            State.Phase = TurnPhase.Manage;

            if (cell.IsPurchasable)
            {
                if (cell.OwnerId is null)
                {
                    State.Phase = TurnPhase.BuyDecision;
                    events.Add(new PurchaseOffered(player.Id, player.Position, cell.Price));
                }
                else if (cell.OwnerId != player.Id)
                {
                    PayRent(player, cell, events);
                }
                return;
            }

            switch (cell.Type)
            {
                case CellType.Rest:
                    player.IsResting = true;
                    events.Add(new RestStarted(player.Id));
                    break;
                // Тюрьма, казино и «Шанс» — этап 3. На клетке «Тюрьма» игрок пока просто в гостях (§6).
            }
        }

        private void PayRent(Player player, BoardCell cell, List<GameEvent> events)
        {
            var owner = State.FindPlayer(cell.OwnerId!.Value)!;
            // Долги и банкротство — этап 3. Пока игрок отдаёт сколько есть: баланс не уходит в минус (§12).
            int amount = Math.Min(GameRules.Rent(cell), player.Balance);
            player.Balance -= amount;
            owner.Balance += amount;
            events.Add(new RentPaid(player.Id, owner.Id, player.Position, amount));
        }

        private void Buy(Player player, List<GameEvent> events)
        {
            var cell = State.CurrentCell;
            player.Balance -= cell.Price;
            cell.OwnerId = player.Id;
            events.Add(new PropertyBought(player.Id, player.Position, cell.Price));
            State.Phase = TurnPhase.Manage;
        }

        private void PassTurn(List<GameEvent> events)
        {
            // Отдыхающие пропускают ход. Цикл конечен: каждый пропуск снимает отметку отдыха.
            while (true)
            {
                State.CurrentPlayerIndex = (State.CurrentPlayerIndex + 1) % State.Players.Count;
                var next = State.CurrentPlayer;
                if (!next.IsResting)
                    break;
                next.IsResting = false;
                events.Add(new TurnSkipped(next.Id));
            }

            State.Phase = TurnPhase.AwaitingRoll;
            State.LastRoll = null;
            events.Add(new TurnStarted(State.CurrentPlayer.Id));
        }

        // Тасование Фишера — Йетса через генератор движка.
        private void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
