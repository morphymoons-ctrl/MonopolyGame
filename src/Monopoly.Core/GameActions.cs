namespace Monopoly.Core
{
    // Действия игроков. PlayerId — кто действует; движок сам проверяет, можно ли ему это сейчас.
    // Простые записи, чтобы их можно было передавать по сети.
    public abstract record GameAction(int PlayerId);

    // --- Ход ---

    public sealed record RollDice(int PlayerId) : GameAction(PlayerId);

    public sealed record BuyProperty(int PlayerId) : GameAction(PlayerId);

    // Отказ от покупки — компания уходит на аукцион.
    public sealed record DeclinePurchase(int PlayerId) : GameAction(PlayerId);

    public sealed record EndTurn(int PlayerId) : GameAction(PlayerId);

    // --- Тюрьма и казино ---

    public sealed record PayBail(int PlayerId) : GameAction(PlayerId);

    public sealed record UseJailCard(int PlayerId) : GameAction(PlayerId);

    public sealed record PlayCasino(int PlayerId, int Bet) : GameAction(PlayerId);

    // --- Аукцион ---

    public sealed record PlaceBid(int PlayerId, int Amount) : GameAction(PlayerId);

    public sealed record PassAuction(int PlayerId) : GameAction(PlayerId);

    // --- Имущество ---

    // Построить филиал, а на пятом уровне — головной офис.
    public sealed record BuildBranch(int PlayerId, int CellIndex) : GameAction(PlayerId);

    public sealed record SellBranch(int PlayerId, int CellIndex) : GameAction(PlayerId);

    public sealed record MortgageCompany(int PlayerId, int CellIndex) : GameAction(PlayerId);

    public sealed record RedeemCompany(int PlayerId, int CellIndex) : GameAction(PlayerId);

    // --- Обмен ---

    // Игрок отдаёт Give и просит Take у TargetId.
    // В списке доступных действий ProposeTrade с пустыми условиями значит «сейчас можно предложить обмен».
    public sealed record ProposeTrade(int PlayerId, int TargetId, TradeTerms Give, TradeTerms Take) : GameAction(PlayerId);

    public sealed record AcceptTrade(int PlayerId) : GameAction(PlayerId);

    public sealed record RejectTrade(int PlayerId) : GameAction(PlayerId);

    public sealed record CancelTrade(int PlayerId) : GameAction(PlayerId);

    // --- Долги ---

    public sealed record DeclareBankruptcy(int PlayerId) : GameAction(PlayerId);
}
