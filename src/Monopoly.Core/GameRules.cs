namespace Monopoly.Core
{
    // Числа из docs/RULES.md.
    public static class GameRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 5;
        public const int StartingBalance = 1500;

        public const int StartBonus = 200;
        public const int DoublesToJail = 3;

        public const int JailCell = 8;
        public const int BailAmount = 50;
        public const int MaxJailAttempts = 3;

        public const int HeadOfficeLevel = 5;

        public const int AuctionStep = 10;

        public static readonly IReadOnlyList<int> CasinoBets = new[] { 50, 100, 200, 300 };

        // Множитель базовой аренды по уровню: 0 — без филиалов, 1–4 — филиалы, 5 — головной офис.
        public static readonly IReadOnlyList<int> LevelMultipliers = new[] { 1, 5, 15, 40, 55, 70 };
        // Монополия без филиалов — ×2.
        public const int MonopolyMultiplier = 2;
        // Логистика: кубики ×4 за одну компанию, ×10 за обе.
        public const int LogisticsSingle = 4, LogisticsBoth = 10;
        // Аренда АЗС по числу станций у владельца (0–4).
        public static readonly IReadOnlyList<int> GasStationRent = new[] { 0, 25, 50, 100, 200 };

        // Базовая аренда — 10% цены.
        public static int BaseRent(BoardCell cell) => cell.Price / 10;

        // Аренда за клетку (§5). diceTotal нужен для логистики.
        public static int Rent(IReadOnlyList<BoardCell> board, int cellIndex, int diceTotal)
        {
            var cell = board[cellIndex];
            if (cell.OwnerId is not int owner || cell.IsMortgaged)
                return 0;

            var group = board.Where(c => c.Type == cell.Type).ToList();
            int owned = group.Count(c => c.OwnerId == owner);

            switch (cell.Type)
            {
                case CellType.GasStation:
                    return GasStationRent[owned];
                case CellType.Logistics:
                    return diceTotal * (owned == group.Count ? LogisticsBoth : LogisticsSingle);
            }

            int baseRent = BaseRent(cell);
            if (cell.Level > 0)
                return baseRent * LevelMultipliers[cell.Level];
            return owned == group.Count ? baseRent * MonopolyMultiplier : baseRent;
        }

        // Все компании группы у одного владельца (заложенные тоже считаются).
        public static bool IsMonopoly(IReadOnlyList<BoardCell> board, CellType type, int ownerId) =>
            board.Where(c => c.Type == type).All(c => c.OwnerId == ownerId);
    }
}
