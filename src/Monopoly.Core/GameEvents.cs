namespace Monopoly.Core
{
    // События движка: что произошло в игре. Интерфейс их только показывает.
    // Игроки — по Id, клетки — по номеру на поле.
    public abstract record GameEvent;

    // Seed — зерно генератора, по нему партию можно повторить; null, если генератор без зерна (тесты).
    public sealed record GameStarted(int? Seed, IReadOnlyList<int> TurnOrder) : GameEvent;

    public sealed record TurnStarted(int PlayerId) : GameEvent;

    // Пропуск хода: после «Зачілься» или пєтушатні (§6, §7).
    public enum SkipReason { Rest, Jail }

    public sealed record TurnSkipped(int PlayerId, SkipReason Reason = SkipReason.Rest) : GameEvent;

    public sealed record GameOver(int WinnerId) : GameEvent;

    // --- События партии (§17) ---

    // Новый круг: ход снова дошёл до первого в порядке ходов.
    public sealed record RoundStarted(int Round) : GameEvent;

    // Началось событие. Rounds — сколько кругов действует (0 — мгновенное или до конца игры); Group — у «Сезона» и «Санкцій».
    public sealed record WorldEventStarted(WorldEventKind Kind, int Rounds, CellType? Group = null) : GameEvent;

    // Событие на несколько кругов закончилось.
    public sealed record WorldEventEnded(WorldEventKind Kind, CellType? Group = null) : GameEvent;

    // Аренду не взяли из-за события (санкции, блекаут, карантин).
    public sealed record RentWaived(int PlayerId, int CellIndex, WorldEventKind Reason) : GameEvent;

    // --- Движение ---

    public sealed record DiceRolled(int PlayerId, int Die1, int Die2) : GameEvent
    {
        public int Total => Die1 + Die2;
        public bool IsDouble => Die1 == Die2;
    }

    // Выпал дубль — игрок бросает ещё раз.
    public sealed record RollAgain(int PlayerId) : GameEvent;

    public sealed record PlayerMoved(int PlayerId, int From, int To) : GameEvent;

    public sealed record PassedStart(int PlayerId, int Amount) : GameEvent;

    // Встал ровно на «Старт» — ещё столько же сверху (§3).
    public sealed record LandedOnStart(int PlayerId, int Amount) : GameEvent;

    // Игрок попал на «Отдых» и пропустит следующий ход.
    public sealed record RestStarted(int PlayerId) : GameEvent;

    // --- Пєтушатня (§6): попал — пропускаешь следующий ход ---

    // Landed — встал на клетку 8 после броска.
    public enum JailReason { ThreeDoubles, Card, Landed }

    public sealed record SentToJail(int PlayerId, JailReason Reason) : GameEvent;

    // Сработала карточка «Вийти з пєтушатні»: ход не пропускается.
    public sealed record JailCardUsed(int PlayerId) : GameEvent;

    // Заложенную компанию не выкупили за 15 ходов — она вернулась банку (§10).
    public sealed record MortgageExpired(int PlayerId, int CellIndex) : GameEvent;

    // --- Покупка и аукцион ---

    public sealed record PurchaseOffered(int PlayerId, int CellIndex, int Price) : GameEvent;

    public sealed record PropertyBought(int PlayerId, int CellIndex, int Price) : GameEvent;

    public sealed record PurchaseDeclined(int PlayerId, int CellIndex) : GameEvent;

    // StartBid — первая ставка (90% цены); FinderId — кто получит 30% от продажи, null — никто (§4).
    public sealed record AuctionStarted(int CellIndex, int StartBid, int? FinderId) : GameEvent;

    public sealed record BidPlaced(int PlayerId, int Amount) : GameEvent;

    public sealed record AuctionPassed(int PlayerId) : GameEvent;

    public sealed record AuctionWon(int PlayerId, int CellIndex, int Amount) : GameEvent;

    // Находчику, которому не хватило денег на покупку, — 30% итоговой ставки (§4).
    public sealed record FinderPaid(int PlayerId, int CellIndex, int Amount) : GameEvent;

    public sealed record AuctionUnsold(int CellIndex) : GameEvent;

    // --- Деньги ---

    public sealed record RentPaid(int PayerId, int OwnerId, int CellIndex, int Amount) : GameEvent;

    // На заложенной компании аренды нет.
    public sealed record RentSkipped(int PlayerId, int CellIndex) : GameEvent;

    public sealed record PaidToBank(int PlayerId, int Amount) : GameEvent;

    public sealed record ReceivedFromBank(int PlayerId, int Amount) : GameEvent;

    public sealed record PaidToPlayer(int FromId, int ToId, int Amount) : GameEvent;

    // Наличных не хватило — появился долг. CreditorId null — долг банку.
    public sealed record DebtIncurred(int DebtorId, int? CreditorId, int Amount) : GameEvent;

    public sealed record DebtPaid(int DebtorId, int? CreditorId, int Amount) : GameEvent;

    // CreditorId null — имущество вернулось банку.
    public sealed record PlayerBankrupt(int PlayerId, int? CreditorId) : GameEvent;

    // --- Казино и «Шанс» ---

    public sealed record CasinoOffered(int PlayerId) : GameEvent;

    // Multiplier: 0 — проигрыш, 1 — ставка вернулась, 2 и 3 — выигрыш.
    public sealed record CasinoPlayed(int PlayerId, int Bet, int Multiplier) : GameEvent
    {
        public int Payout => Bet * Multiplier;
    }

    public sealed record ChanceCardDrawn(int PlayerId, ChanceCard Card) : GameEvent;

    // --- Имущество ---

    // Level — новый уровень: 1–4 филиала, 5 — головной офис.
    public sealed record BranchBuilt(int PlayerId, int CellIndex, int Level, int Cost) : GameEvent;

    public sealed record BranchSold(int PlayerId, int CellIndex, int Level, int Amount) : GameEvent;

    public sealed record CompanyMortgaged(int PlayerId, int CellIndex, int Amount) : GameEvent;

    public sealed record CompanyRedeemed(int PlayerId, int CellIndex, int Amount) : GameEvent;

    // --- Обмен ---

    public sealed record TradeProposed(TradeOffer Offer) : GameEvent;

    public sealed record TradeAccepted(TradeOffer Offer) : GameEvent;

    public sealed record TradeRejected(int FromId, int ToId) : GameEvent;

    public sealed record TradeCancelled(int FromId, int ToId) : GameEvent;
}
