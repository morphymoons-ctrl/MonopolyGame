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
        int? WinnerId,
        BoardTheme Theme = BoardTheme.Business,
        StatsSnapshot? Stats = null)
    {
        public PlayerSnapshot? FindPlayer(int id) => Players.FirstOrDefault(p => p.Id == id);

        // Переносит в поле клиента всё, что меняется по ходу партии: цены, владельцев, филиалы, залог.
        // Тогда интерфейс считает аренду той же GameRules.Rent, что и движок, — число на клетке совпадает с оплатой.
        public void ApplyTo(IReadOnlyList<BoardCell> board)
        {
            for (int i = 0; i < Cells.Count && i < board.Count; i++)
            {
                board[i].Price = Cells[i].Price;
                board[i].OwnerId = Cells[i].OwnerId;
                board[i].Level = Cells[i].Level;
                board[i].IsMortgaged = Cells[i].IsMortgaged;
                board[i].MortgageTurnsLeft = Cells[i].MortgageTurnsLeft;
            }
        }
    }

    // Игроки в снимке идут в порядке хода.
    public sealed record PlayerSnapshot(
        int Id, string Name, int Balance, int Position, bool IsInJail, bool IsResting, int JailCards, bool IsBankrupt);

    // MortgageTurnsLeft — сколько ходов владельца осталось на выкуп заложенной компании (§10).
    public sealed record CellSnapshot(int? OwnerId, int Level, bool IsMortgaged, int Price, int MortgageTurnsLeft = 0);

    public sealed record AuctionSnapshot(int CellIndex, int? LeaderId, int HighBid, int MinBid, IReadOnlyList<int> PassedIds);
}
