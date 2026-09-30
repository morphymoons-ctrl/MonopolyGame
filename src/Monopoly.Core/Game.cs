namespace Monopoly.Core
{
    public sealed record GameOptions
    {
        // Порядок ходов — случайный при старте (RULES.md, §1).
        public bool ShuffleTurnOrder { get; init; } = true;
        // Колода «Шанса» перемешивается в начале и при каждом новом круге колоды (§9).
        public bool ShuffleChanceDeck { get; init; } = true;
        // Дубли реже: выпавший дубль иногда перебрасывается (§3). Тесты с заданными кубиками это выключают.
        public bool ReduceDoubles { get; init; } = true;
        // Тематика доски (§15): названия клеток; правила от неё не зависят.
        public BoardTheme Theme { get; init; } = BoardTheme.Business;
        // События партии (§17). В тестах по умолчанию выключены: они берут числа из генератора.
        public EventFrequency Events { get; init; } = EventFrequency.Off;
    }

    // Движок: принимает действия игроков, проверяет их по правилам и меняет состояние.
    // Разбит на части: здесь ход и проверки, остальное — в Game.*.cs.
    public partial class Game
    {
        private readonly IRandomSource random;
        private readonly GameOptions options;
        private readonly List<GameEvent> history = new();
        private readonly List<GameAction> actions = new();

        public GameState State { get; }
        // Все события с начала партии.
        public IReadOnlyList<GameEvent> History => history;
        // Все выполненные действия по порядку. Вместе с зерном по ним партию можно восстановить (Replay).
        public IReadOnlyList<GameAction> Actions => actions;
        // Зерно генератора; null — генератор без зерна (тесты).
        public int? Seed => (random as SeededRandom)?.Seed;

        // Новая партия. Без seed зерно выбирается случайно и попадает в событие GameStarted.
        public static Game Start(IReadOnlyList<string> playerNames, int? seed = null, BoardTheme theme = BoardTheme.Business,
            EventFrequency events = EventFrequency.Off)
        {
            return new Game(playerNames, new SeededRandom(seed ?? Random.Shared.Next()), new GameOptions { Theme = theme, Events = events });
        }

        public Game(IReadOnlyList<string> playerNames, IRandomSource random, GameOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(playerNames);
            if (playerNames.Count < GameRules.MinPlayers || playerNames.Count > GameRules.MaxPlayers)
                throw new ArgumentException($"Потрібно від {GameRules.MinPlayers} до {GameRules.MaxPlayers} гравців.", nameof(playerNames));
            if (playerNames.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Кожен гравець повинен мати ім'я.", nameof(playerNames));

            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.options = options ?? new GameOptions();

            var players = playerNames.Select((name, i) => new Player(i, name)).ToList();
            if (this.options.ShuffleTurnOrder)
                Shuffle(players);

            State = new GameState(Board.Create(this.options.Theme), players) { Theme = this.options.Theme };
            State.ChanceDeck.AddRange(Enum.GetValues<ChanceCard>());
            if (this.options.ShuffleChanceDeck)
                Shuffle(State.ChanceDeck);

            history.Add(new GameStarted((random as SeededRandom)?.Seed, players.Select(p => p.Id).ToList()));
            history.Add(new TurnStarted(State.CurrentPlayer.Id));
            State.Stats.TurnStarted();
        }

        public bool CanExecute(GameAction action) => Validate(action) is null;

        // Сумма в сообщениях движка — в валюте доски партии (§15).
        private string Money(int amount) => GameRules.Money(amount, State.Theme);

        // Что игрок может сделать прямо сейчас. По этому списку интерфейс включает кнопки.
        public IReadOnlyList<GameAction> GetAvailableActions(int playerId)
        {
            var candidates = new List<GameAction>
            {
                new RollDice(playerId),
                new BuyProperty(playerId),
                new DeclinePurchase(playerId),
                new EndTurn(playerId),
                new PassAuction(playerId),
                new AcceptTrade(playerId),
                new RejectTrade(playerId),
                new CancelTrade(playerId),
                new DeclareBankruptcy(playerId),
            };
            candidates.AddRange(GameRules.CasinoBets.Select(bet => new PlayCasino(playerId, bet)));
            if (State.Auction is not null)
                candidates.Add(new PlaceBid(playerId, State.Auction.MinBid));
            for (int i = 0; i < State.Board.Count; i++)
            {
                if (State.Board[i].OwnerId != playerId)
                    continue;
                candidates.Add(new BuildBranch(playerId, i));
                candidates.Add(new SellBranch(playerId, i));
                candidates.Add(new MortgageCompany(playerId, i));
                candidates.Add(new RedeemCompany(playerId, i));
            }

            var available = candidates.Where(CanExecute).ToList();
            // Условия обмена игрок выбирает сам, поэтому в списке — «пустой» обмен как знак, что предлагать можно.
            var player = State.FindPlayer(playerId);
            if (player is not null && ValidateCommon(player) is null && ValidateTradeTiming(player) is null)
                available.Add(new ProposeTrade(playerId, -1, TradeTerms.Empty, TradeTerms.Empty));
            // Так же «пустое» встречное предложение — знак, что можно изменить условия (§11).
            if (player is not null && CanCounter(player))
                available.Add(new CounterTrade(playerId, TradeTerms.Empty, TradeTerms.Empty));
            return available;
        }

        public ActionResult Execute(GameAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            // Игрок не может выдать своё действие за действие администратора.
            if (action is AdminAction)
                return ActionResult.Fail("Це дія адміністратора.");

            var error = Validate(action);
            if (error is not null)
                return ActionResult.Fail(error);

            var player = State.FindPlayer(action.PlayerId)!;
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
                    Decline(player, events);
                    break;
                case EndTurn:
                    PassTurn(events);
                    break;
                case PlayCasino casino:
                    PlayCasinoBet(player, casino.Bet, events);
                    break;
                case PlaceBid bid:
                    Bid(player, bid.Amount, events);
                    break;
                case PassAuction:
                    Pass(player, events);
                    break;
                case BuildBranch build:
                    Build(player, build.CellIndex, events);
                    break;
                case SellBranch sell:
                    Sell(player, sell.CellIndex, events);
                    break;
                case MortgageCompany mortgage:
                    Mortgage(player, mortgage.CellIndex, events);
                    break;
                case RedeemCompany redeem:
                    Redeem(player, redeem.CellIndex, events);
                    break;
                case ProposeTrade proposal:
                    Propose(player, proposal, events);
                    break;
                case AcceptTrade:
                    AcceptOffer(events);
                    break;
                case RejectTrade:
                    events.Add(new TradeRejected(State.Trade!.FromId, State.Trade.ToId));
                    State.Trade = null;
                    break;
                case CounterTrade counter:
                    Counter(player, counter, events);
                    break;
                case CancelTrade:
                    events.Add(new TradeCancelled(State.Trade!.FromId, State.Trade.ToId));
                    State.Trade = null;
                    break;
                case DeclareBankruptcy:
                    GoBankrupt(player, State.Debts[0].CreditorId, events);
                    break;
            }

            Continue(events);
            history.AddRange(events);
            actions.Add(action);
            return ActionResult.Ok(events);
        }

        // После каждого действия: закрыть долги, на которые уже хватает денег, проверить победителя
        // и передать ход, если текущий игрок обанкротился.
        private void Continue(List<GameEvent> events)
        {
            SettleDebts(events);
            if (State.Debts.Count > 0 || State.WinnerId is not null)
                return;

            var active = State.ActivePlayers.ToList();
            if (active.Count == 1)
            {
                State.WinnerId = active[0].Id;
                State.Auction = null;
                State.PendingPurchase = null;
                State.Trade = null;
                events.Add(new GameOver(active[0].Id));
                return;
            }

            if (State.CurrentPlayer.IsBankrupt)
                PassTurn(events);
        }

        // --- Проверка действий ---

        private string? Validate(GameAction action)
        {
            var player = State.FindPlayer(action.PlayerId);
            if (player is null)
                return "Немає такого гравця.";
            var common = ValidateCommon(player);
            if (common is not null)
                return common;

            return action switch
            {
                RollDice => RequireTurn(player, TurnPhase.AwaitingRoll),
                BuyProperty => ValidateBuy(player),
                DeclinePurchase => NothingToBuy(player) ?? RequireTurn(player, TurnPhase.BuyDecision),
                EndTurn => RequireTurn(player, TurnPhase.Manage),
                PlayCasino casino => ValidateCasino(player, casino.Bet),
                PlaceBid bid => ValidateBid(player, bid.Amount),
                PassAuction => ValidatePass(player),
                BuildBranch build => ValidateBuild(player, build.CellIndex),
                SellBranch sell => ValidateSell(player, sell.CellIndex),
                MortgageCompany mortgage => ValidateMortgage(player, mortgage.CellIndex),
                RedeemCompany redeem => ValidateRedeem(player, redeem.CellIndex),
                ProposeTrade proposal => ValidateProposal(player, proposal),
                AcceptTrade => ValidateAnswer(player) ?? ValidateOffer(State.Trade!),
                RejectTrade => ValidateAnswer(player),
                CancelTrade => ValidateCancel(player),
                CounterTrade counter => ValidateCounter(player, counter),
                DeclareBankruptcy => State.Phase == TurnPhase.Debt && State.Debts[0].DebtorId == player.Id
                    ? null
                    : "Оголосити банкрутство можна, лише коли нема чим сплатити борг.",
                _ => "Невідома дія.",
            };
        }

        private string? ValidateCommon(Player player)
        {
            if (State.Phase == TurnPhase.GameOver)
                return "Гру закінчено.";
            if (player.IsBankrupt)
                return "Ви вибули з гри.";
            return null;
        }

        // Действие текущего игрока в одной из фаз; иначе — объяснение, чего ждёт игра.
        private string? RequireTurn(Player player, params TurnPhase[] allowed)
        {
            var current = State.CurrentPlayer;
            if (allowed.Contains(State.Phase))
                return player == current ? null : $"Зараз ходить {current.Name}.";

            switch (State.Phase)
            {
                case TurnPhase.Debt:
                    var debtor = State.FindPlayer(State.Debts[0].DebtorId)!;
                    return debtor == player
                        ? $"Спершу закрийте борг: продайте {State.Terms.Branches} або закладіть компанії."
                        : $"Чекаємо, поки {debtor.Name} закриє борг.";
                case TurnPhase.Auction:
                    return "Зараз триває аукціон.";
                case TurnPhase.TradeOffer:
                    return $"Чекаємо відповіді від гравця {State.FindPlayer(State.Trade!.ToId)!.Name} на пропозицію обміну.";
                case TurnPhase.GameOver:
                    return "Гру закінчено.";
            }

            if (player != current)
                return $"Зараз ходить {current.Name}.";
            return State.Phase switch
            {
                TurnPhase.BuyDecision => $"Спершу вирішіть, чи купувати «{State.CurrentCell.Name}».",
                TurnPhase.AwaitingRoll => State.LastRoll is null ? "Спершу киньте кубики." : "Випав дубль — киньте кубики ще раз.",
                _ => "Кубики в цьому ході вже кинуто.",
            };
        }

        private string? NothingToBuy(Player player) =>
            player == State.CurrentPlayer && State.Phase is TurnPhase.AwaitingRoll or TurnPhase.Manage
                ? "Зараз нічого купувати."
                : null;

        private string? ValidateBuy(Player player)
        {
            var error = NothingToBuy(player) ?? RequireTurn(player, TurnPhase.BuyDecision);
            if (error is not null)
                return error;
            var cell = State.CurrentCell;
            return player.Balance < cell.Price
                ? $"Не вистачає грошей: «{cell.Name}» коштує {Money(cell.Price)}, у вас {Money(player.Balance)}."
                : null;
        }

        private string? ValidateCasino(Player player, int bet)
        {
            var error = RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage);
            if (error is not null)
                return error;
            if (!State.CasinoAvailable)
                return "Грати в казино можна лише одразу після потрапляння на клітинку.";
            if (!GameRules.CasinoBets.Contains(bet))
                return $"Ставка може бути {string.Join(", ", GameRules.CasinoBets.Select(Money))}.";
            return bet > player.Balance ? $"Не вистачає грошей на ставку: у вас {Money(player.Balance)}." : null;
        }

        // --- Ход ---

        private void Roll(Player player, List<GameEvent> events)
        {
            var roll = ThrowDice();
            State.LastRoll = roll;
            State.CasinoAvailable = false;
            events.Add(new DiceRolled(player.Id, roll.Die1, roll.Die2));

            if (roll.IsDouble && ++State.DoublesInRow == GameRules.DoublesToJail)
            {
                SendToJail(player, JailReason.ThreeDoubles, events);
                return;
            }

            MoveForward(player, roll.Total, events);
            ResolveCell(player, events);

            // Дубль — ещё один бросок, если игрок не попал в пєтушатню и не выбыл.
            if (roll.IsDouble && !player.IsInJail && !player.IsBankrupt)
            {
                State.Stage = TurnPhase.AwaitingRoll;
                events.Add(new RollAgain(player.Id));
            }
            else
            {
                State.Stage = TurnPhase.Manage;
            }
        }

        // Два кубика. Выпавший дубль с вероятностью DoubleRerollPercent перебрасывается один раз — дубли реже (§3).
        private DiceRoll ThrowDice()
        {
            var roll = new DiceRoll(random.Next(1, 7), random.Next(1, 7));
            if (options.ReduceDoubles && roll.IsDouble && random.Next(0, 100) < GameRules.DoubleRerollPercent)
                roll = new DiceRoll(random.Next(1, 7), random.Next(1, 7));
            return roll;
        }

        private void MoveForward(Player player, int steps, List<GameEvent> events)
        {
            int from = player.Position;
            int count = State.Board.Count;
            player.Position = (from + steps) % count;
            // Проход или попадание на «Старт» (§3) — в журнале раньше, чем прибытие на клетку.
            if (from + steps >= count)
            {
                player.Balance += State.StartBonus;
                events.Add(new PassedStart(player.Id, State.StartBonus));
            }
            events.Add(new PlayerMoved(player.Id, from, player.Position));
        }

        private void MoveForwardTo(Player player, int cellIndex, List<GameEvent> events)
        {
            int count = State.Board.Count;
            MoveForward(player, (cellIndex - player.Position + count) % count, events);
        }

        private void MoveBack(Player player, int steps, List<GameEvent> events)
        {
            int from = player.Position;
            player.Position = ((from - steps) % State.Board.Count + State.Board.Count) % State.Board.Count;
            events.Add(new PlayerMoved(player.Id, from, player.Position));
        }

        // Действие клетки, на которую встал игрок. rentMultiplier — для карточки «Такси».
        private void ResolveCell(Player player, List<GameEvent> events, int rentMultiplier = 1)
        {
            var cell = State.Board[player.Position];

            if (cell.IsPurchasable)
            {
                if (cell.OwnerId is null)
                {
                    State.PendingPurchase = player.Position;
                    events.Add(new PurchaseOffered(player.Id, player.Position, cell.Price));
                }
                else if (cell.OwnerId != player.Id)
                {
                    if (cell.IsMortgaged)
                    {
                        events.Add(new RentSkipped(player.Id, player.Position));
                        return;
                    }
                    // Санкции, блекаут или карантин (§17) — аренды нет, в журнале — почему.
                    if (WorldEvents.RentBlockedBy(State.Events, cell.Type) is WorldEventKind blockedBy)
                    {
                        events.Add(new RentWaived(player.Id, player.Position, blockedBy));
                        return;
                    }
                    var owner = State.FindPlayer(cell.OwnerId.Value)!;
                    int rent = GameRules.Rent(State.Board, player.Position, State.LastRoll?.Total ?? 0, State.RentContext) * rentMultiplier;
                    State.Stats.Rent(player.Id, owner.Id, player.Position, rent);
                    Charge(player, owner, rent, events, new RentPaid(player.Id, owner.Id, player.Position, rent));
                }
                return;
            }

            switch (cell.Type)
            {
                case CellType.Rest:
                    player.IsResting = true;
                    events.Add(new RestStarted(player.Id));
                    break;
                case CellType.Casino:
                    State.CasinoAvailable = true;
                    events.Add(new CasinoOffered(player.Id));
                    break;
                case CellType.Chance:
                    DrawChance(player, events);
                    break;
                case CellType.Jail:
                    events.Add(new SentToJail(player.Id, JailReason.Landed));
                    Imprison(player, events);
                    break;
                // «Старт»: за проход бонус уже начислен при движении; встал ровно на него — ещё столько же (§3).
                case CellType.Start:
                    player.Balance += State.StartBonus;
                    events.Add(new LandedOnStart(player.Id, State.StartBonus));
                    break;
            }
        }

        private void Buy(Player player, List<GameEvent> events)
        {
            var cell = State.CurrentCell;
            player.Balance -= cell.Price;
            cell.OwnerId = player.Id;
            State.PendingPurchase = null;
            State.Stats.Bought(player.Id);
            events.Add(new PropertyBought(player.Id, player.Position, cell.Price));
        }

        private void PassTurn(List<GameEvent> events)
        {
            State.PendingPurchase = null;
            State.CasinoAvailable = false;
            State.LastRoll = null;
            State.DoublesInRow = 0;
            State.BuiltThisTurn.Clear();

            // Выбывших пропускаем молча, отдыхающих и сидящих в пєтушатні — с событием (§6, §7).
            // Цикл конечен: каждый пропуск снимает отметку, а активный игрок есть всегда.
            while (true)
            {
                State.CurrentPlayerIndex = (State.CurrentPlayerIndex + 1) % State.Players.Count;
                // Ход снова у первого в порядке ходов — новый круг: события (§17).
                if (State.CurrentPlayerIndex == 0)
                    StartRound(events);
                var next = State.CurrentPlayer;
                if (next.IsBankrupt)
                    continue;
                if (!next.IsResting && !next.IsInJail)
                    break;
                var reason = next.IsInJail ? SkipReason.Jail : SkipReason.Rest;
                next.IsResting = false;
                // Во время «Облави» пропускается два хода (§17).
                if (next.IsInJail && --next.JailSkips <= 0)
                    next.IsInJail = false;
                events.Add(new TurnSkipped(next.Id, reason));
            }

            State.Stage = TurnPhase.AwaitingRoll;
            events.Add(new TurnStarted(State.CurrentPlayer.Id));
            State.Stats.TurnStarted();
            // Ход действительно начался (пропущенные не считаются) — срок выкупа заложенных компаний (§10).
            TickMortgages(State.CurrentPlayer, events);
        }

        // --- Пєтушатня (§6) ---

        // Три дубля или карточка «Шанса»: фишка на клетку 8, бросков в этом ходу больше нет.
        private void SendToJail(Player player, JailReason reason, List<GameEvent> events)
        {
            player.Position = GameRules.JailCell;
            State.DoublesInRow = 0;
            State.Stage = TurnPhase.Manage;
            events.Add(new SentToJail(player.Id, reason));
            Imprison(player, events);
        }

        // Следующий ход будет пропущен — если нет карточки «Вийти з пєтушатні»: тогда она срабатывает сама.
        private void Imprison(Player player, List<GameEvent> events)
        {
            if (player.JailCards > 0)
            {
                player.JailCards--;
                State.ChanceDiscard.Add(ChanceCard.GetOutOfJail);
                events.Add(new JailCardUsed(player.Id));
                return;
            }
            player.IsInJail = true;
            player.JailSkips = WorldEvents.IsActive(State.Events, WorldEventKind.Raid) ? 2 : 1;
            State.Stats.Jailed(player.Id);
        }

        // --- Казино ---

        // 50% — проигрыш, 10% — ставка возвращается, 35% — ×2, 5% — ×3 (§8).
        // «Джекпот-тиждень» (§17): 40% — проигрыш, 10% — возврат, 35% — ×2, 15% — ×3.
        private void PlayCasinoBet(Player player, int bet, List<GameEvent> events)
        {
            int roll = random.Next(0, 100);
            int lose = WorldEvents.IsActive(State.Events, WorldEventKind.Jackpot) ? 40 : 50;
            int multiplier = roll < lose ? 0 : roll < lose + 10 ? 1 : roll < lose + 45 ? 2 : 3;
            player.Balance += bet * multiplier - bet;
            State.CasinoAvailable = false;
            State.Stats.Casino(player.Id, bet * multiplier - bet);
            events.Add(new CasinoPlayed(player.Id, bet, multiplier));
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
