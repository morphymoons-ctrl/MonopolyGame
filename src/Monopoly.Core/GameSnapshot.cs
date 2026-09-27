namespace Monopoly.Core
{
    // Снимок состояния для показа: хост отправляет его клиентам после каждого действия.
    // Названия клеток не входят — они одинаковые у всех, см. Board.CreateDefault. Цены входят: их может менять администратор.
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

        // Переносит цены из снимка в поле клиента: от цены считаются аренда, филиалы и залог в интерфейсе.
        public void ApplyPrices(IReadOnlyList<BoardCell> board)
        {
            for (int i = 0; i < Cells.Count && i < board.Count; i++)
                board[i].Price = Cells[i].Price;
        }
    }

    // Игроки в снимке идут в порядке хода.
    public sealed record PlayerSnapshot(
        int Id, string Name, int Balance, int Position, bool IsInJail, bool IsResting, int JailCards, bool IsBankrupt);

    public sealed record CellSnapshot(int? OwnerId, int Level, bool IsMortgaged, int Price);

    public sealed record AuctionSnapshot(int CellIndex, int? LeaderId, int HighBid, int MinBid, IReadOnlyList<int> PassedIds);
}
