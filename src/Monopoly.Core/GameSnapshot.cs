namespace Monopoly.Core
{
    // Снимок состояния для показа: хост отправляет его клиентам после каждого действия.
    // Поле (названия, цены) не входит — оно одинаковое у всех, см. Board.CreateDefault.
    // Колода «Шанса» тоже не входит: её порядок — секрет.
    public sealed record GameSnapshot(
        IReadOnlyList<PlayerSnapshot> Players,
        IReadOnlyList<CellSnapshot> Cells,
        int CurrentPlayerId,
        TurnPhase Phase,
        DiceRoll? LastRoll,
        int? PendingPurchase,
        AuctionSnapshot? Auction,
        Debt? Debt,
        TradeOffer? Trade,
        bool CasinoAvailable,
        int? WinnerId)
    {
        public PlayerSnapshot? FindPlayer(int id) => Players.FirstOrDefault(p => p.Id == id);
    }

    // Игроки в снимке идут в порядке хода.
    public sealed record PlayerSnapshot(
        int Id, string Name, int Balance, int Position, bool IsInJail, bool IsResting, int JailCards, bool IsBankrupt);

    public sealed record CellSnapshot(int? OwnerId, int Level, bool IsMortgaged);

    public sealed record AuctionSnapshot(int CellIndex, int? LeaderId, int HighBid, int MinBid, IReadOnlyList<int> PassedIds);
}
