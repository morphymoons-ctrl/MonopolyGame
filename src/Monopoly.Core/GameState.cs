namespace Monopoly.Core
{
    // Фаза партии: кто и что может сделать сейчас.
    public enum TurnPhase
    {
        // Текущий игрок бросает кубики (до броска можно управлять имуществом).
        AwaitingRoll,
        // Текущий игрок стоит на свободной компании: купить или отказаться (тогда аукцион).
        BuyDecision,
        // Аукцион: ставки делают все игроки.
        Auction,
        // Кто-то должен денег: ждём, пока он расплатится или обанкротится.
        Debt,
        // Предложен обмен: ждём ответа второго игрока.
        TradeOffer,
        // Бросок сделан: управление имуществом, затем конец хода.
        Manage,
        GameOver,
    }

    public readonly record struct DiceRoll(int Die1, int Die2)
    {
        public int Total => Die1 + Die2;
        public bool IsDouble => Die1 == Die2;
    }

    // Долг: платёж, на который не хватило наличных. CreditorId null — долг банку.
    public sealed record Debt(int DebtorId, int? CreditorId, int Amount);

    // Что отдаёт одна сторона обмена.
    public sealed record TradeTerms(IReadOnlyList<int> Cells, int Money, int JailCards)
    {
        public static TradeTerms Empty { get; } = new(Array.Empty<int>(), 0, 0);

        public bool IsEmpty => Cells.Count == 0 && Money == 0 && JailCards == 0;
    }

    // Предложение обмена: FromId отдаёт Give и получает Take от ToId.
    public sealed record TradeOffer(int FromId, int ToId, TradeTerms Give, TradeTerms Take);

    public sealed class AuctionState
    {
        public int CellIndex { get; }
        public int? LeaderId { get; internal set; }
        public int HighBid { get; internal set; }
        internal HashSet<int> Passed { get; } = new();

        public AuctionState(int cellIndex)
        {
            CellIndex = cellIndex;
        }

        public int MinBid => LeaderId is null ? GameRules.AuctionStep : HighBid + GameRules.AuctionStep;
    }

    // Всё состояние партии. Меняет его только Game.
    public class GameState
    {
        public IReadOnlyList<BoardCell> Board { get; }
        // Тематика доски (§15) — только названия, правила те же.
        public BoardTheme Theme { get; init; } = BoardTheme.Business;
        // Слова для построек в текстах движка: «філія» или «підрозділ».
        public GameTerms Terms => GameTerms.For(Theme);
        // Игроки в порядке хода, включая выбывших.
        public IReadOnlyList<Player> Players { get; }
        public int CurrentPlayerIndex { get; internal set; }
        // Этап хода без учёта покупки, аукциона, долгов и обмена: AwaitingRoll или Manage.
        internal TurnPhase Stage { get; set; } = TurnPhase.AwaitingRoll;
        // Последний бросок в текущем ходу; null — ещё не бросали.
        public DiceRoll? LastRoll { get; internal set; }
        public int DoublesInRow { get; internal set; }
        // Клетка, которую текущий игрок может купить.
        public int? PendingPurchase { get; internal set; }
        public AuctionState? Auction { get; internal set; }
        internal List<Debt> Debts { get; } = new();
        public TradeOffer? Trade { get; internal set; }
        // Игрок только что попал на «Казино» и может сделать ставку.
        public bool CasinoAvailable { get; internal set; }
        public int? WinnerId { get; internal set; }
        // Колода «Шанса»: сверху берут, использованные — в сброс.
        internal List<ChanceCard> ChanceDeck { get; } = new();
        internal List<ChanceCard> ChanceDiscard { get; } = new();
        // Статистика партии для итогов (§16).
        public GameStats Stats { get; }

        public GameState(IReadOnlyList<BoardCell> board, IReadOnlyList<Player> players)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Players = players ?? throw new ArgumentNullException(nameof(players));
            Stats = new GameStats(board.Count);
        }

        public Player CurrentPlayer => Players[CurrentPlayerIndex];
        public BoardCell CurrentCell => Board[CurrentPlayer.Position];
        public IReadOnlyList<Debt> PendingDebts => Debts;
        public IEnumerable<Player> ActivePlayers => Players.Where(p => !p.IsBankrupt);

        // Фаза с учётом всего, что ждёт ответа. Порядок важен.
        // Обмен — выше долга: должник может предложить обмен (§11), и пока второй отвечает, ждём ответа.
        // Обмен и аукцион или покупка одновременно не бывают: предложить обмен в них нельзя.
        public TurnPhase Phase =>
            WinnerId is not null ? TurnPhase.GameOver
            : Trade is not null ? TurnPhase.TradeOffer
            : Debts.Count > 0 ? TurnPhase.Debt
            : Auction is not null ? TurnPhase.Auction
            : PendingPurchase is not null ? TurnPhase.BuyDecision
            : Stage;

        // Группы, где в этом ходу уже строили: за ход — одна постройка на группу (§5). Сбрасывается при передаче хода.
        internal HashSet<CellType> BuiltThisTurn { get; } = new();

        public Player? FindPlayer(int id) => Players.FirstOrDefault(p => p.Id == id);

        public IEnumerable<BoardCell> CellsOf(int playerId) => Board.Where(c => c.OwnerId == playerId);

        public IEnumerable<BoardCell> GroupOf(BoardCell cell) => Board.Where(c => c.Type == cell.Type);

        public GameSnapshot ToSnapshot() => new(
            Players.Select(p => new PlayerSnapshot(p.Id, p.Name, p.Balance, p.Position, p.IsInJail, p.IsResting,
                p.JailCards, p.IsBankrupt)).ToList(),
            Board.Select(c => new CellSnapshot(c.OwnerId, c.Level, c.IsMortgaged, c.Price, c.MortgageTurnsLeft)).ToList(),
            CurrentPlayer.Id,
            Phase,
            LastRoll,
            PendingPurchase,
            Auction is null ? null : new AuctionSnapshot(Auction.CellIndex, Auction.LeaderId, Auction.HighBid,
                Auction.MinBid, Auction.Passed.Order().ToList()),
            Debts.FirstOrDefault(),
            Trade,
            CasinoAvailable,
            WinnerId,
            Theme,
            Stats.ToSnapshot(Players.Select(p => p.Id)));
    }
}
