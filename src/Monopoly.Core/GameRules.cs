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
        // Дубль перебрасывается с такой вероятностью (%): итого дубль ≈ 1/6 × (1/2 + 1/2 × 1/6) ≈ 10% бросков вместо 17% (§3).
        public const int DoubleRerollPercent = 50;

        public const int JailCell = 8;

        public const int HeadOfficeLevel = 5;

        public const int AuctionStep = 10_000;

        public static readonly IReadOnlyList<int> CasinoBets = new[] { 50_000, 100_000, 200_000, 300_000 };

        // Множитель базовой аренды по уровню: 0 — без филиалов, 1–4 — филиалы, 5 — головной офис (§5).
        // От цены компании: 12% / 60% / 180% / 420% / 540% / 660% — как в классической «Монополии» (и Monopoly One).
        public static readonly IReadOnlyList<int> LevelMultipliers = new[] { 1, 5, 15, 35, 45, 55 };
        // Базовая аренда — процент от цены компании.
        public const int BaseRentPercent = 12;
        // Цена филиала и головного офиса — своя у каждой группы, как в Monopoly One (§5):
        // дешёвые группы (компании за 100–140 тыс.) и дорогие (за 200–240 тыс.).
        public const int CheapBranchCost = 100_000, ExpensiveBranchCost = 150_000;

        public static int BranchCost(CellType type) => type switch
        {
            CellType.Supermarket or CellType.TV or CellType.Nightlife or CellType.Bank => CheapBranchCost,
            CellType.Factory or CellType.Food or CellType.NetworkShop => ExpensiveBranchCost,
            _ => 0,
        };
        // Монополия без филиалов — ×2.
        public const int MonopolyMultiplier = 2;
        // Логистика: сумма кубиков ×4 000 за одну компанию, ×10 000 за обе.
        public const int LogisticsSingle = 4_000, LogisticsBoth = 10_000;
        // Аренда АЗС по числу станций у владельца (0–4).
        public static readonly IReadOnlyList<int> GasStationRent = new[] { 0, 25_000, 50_000, 100_000, 200_000 };

        private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

        // Сумма для текста игроку: «1 500 000 грн». Между тысячами — неразрывный пробел, число не разрывается переносом.
        public static string Money(int amount) => $"{amount.ToString("N0", Ukrainian)} грн";

        // Коротко, для аренды на клетке: «16,8к», «252к», «1,05м» — чтобы не путать с ценой покупки («140 000 грн»).
        public static string ShortMoney(int amount) => amount >= 1_000_000
            ? $"{(amount / 1_000_000.0).ToString("0.##", Ukrainian)}м"
            : $"{(amount / 1_000.0).ToString("0.#", Ukrainian)}к";

        // Базовая аренда — BaseRentPercent от цены.
        public static int BaseRent(BoardCell cell) => cell.Price * BaseRentPercent / 100;

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
