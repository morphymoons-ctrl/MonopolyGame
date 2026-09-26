using System.Globalization;

namespace Monopoly.Core
{
    // Числа из docs/RULES.md. Все суммы — в гривнах, в масштабе ×1000 (стартовый баланс 1 500 000).
    public static class GameRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 5;
        public const int StartingBalance = 1_500_000;

        public const int StartBonus = 200_000;
        public const int DoublesToJail = 3;

        public const int JailCell = 8;
        public const int BailAmount = 50_000;
        public const int MaxJailAttempts = 3;

        public const int HeadOfficeLevel = 5;

        public const int AuctionStep = 10_000;

        public static readonly IReadOnlyList<int> CasinoBets = new[] { 50_000, 100_000, 200_000, 300_000 };

        // Множитель базовой аренды по уровню: 0 — без филиалов, 1–4 — филиалы, 5 — головной офис.
        public static readonly IReadOnlyList<int> LevelMultipliers = new[] { 1, 5, 15, 40, 55, 70 };
        // Монополия без филиалов — ×2.
        public const int MonopolyMultiplier = 2;
        // Логистика: сумма кубиков ×4 000 за одну компанию, ×10 000 за обе.
        public const int LogisticsSingle = 4_000, LogisticsBoth = 10_000;
        // Аренда АЗС по числу станций у владельца (0–4).
        public static readonly IReadOnlyList<int> GasStationRent = new[] { 0, 25_000, 50_000, 100_000, 200_000 };

        private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

        // Сумма для текста игроку: «1 500 000 грн». Между тысячами — неразрывный пробел, число не разрывается переносом.
        public static string Money(int amount) => $"{amount.ToString("N0", Ukrainian)} грн";

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
