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
        // Филиал продаётся банку за этот процент своей цены — и при обычной продаже, и при банкротстве (§5, §12).
        public const int BranchSalePercent = 75;
        // Сколько своих ходов даётся на выкуп заложенной компании; потом она возвращается банку (§10).
        public const int MortgageTurns = 15;

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
        // Аренда АЗС по числу незаложенных станций у владельца (0–4).
        public static readonly IReadOnlyList<int> GasStationRent = new[] { 0, 50_000, 75_000, 150_000, 250_000 };

        private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

        // Суммы в игре — не мельче сотен, а от миллиона — не мельче сотен тысяч (§5):
        // коротко любая сумма пишется максимум с одним знаком после запятой («15,2к», «1,4м»), без «15,25к».
        public static int RoundMoney(int amount)
        {
            int step = Math.Abs(amount) >= 1_000_000 ? 100_000 : 100;
            return (int)Math.Round(amount / (double)step, MidpointRounding.AwayFromZero) * step;
        }

        // Военная доска и «Битва олігархів» (§15): суммы показываются в 1000 раз больше, в миллионах — «90 млн грн».
        // Движок считает в тех же числах, что и на остальных досках, поэтому экономика одна.
        public static int MoneyScale(BoardTheme theme) => theme is BoardTheme.Military or BoardTheme.Oligarchs ? 1000 : 1;

        // Сумма для текста игроку: «1 500 000 грн», на доске «Криптовалюти» — «$1 500 000», на досках в миллионах — «1 500 млн грн» (§15).
        // Между тысячами — неразрывный пробел, число не разрывается переносом.
        public static string Money(int amount, BoardTheme theme = BoardTheme.Business)
        {
            if (MoneyScale(theme) > 1)
                return $"{Millions(amount, theme).ToString("#,0.#", Ukrainian)} млн грн";
            string number = amount.ToString("N0", Ukrainian);
            return theme == BoardTheme.Crypto ? $"${number}" : $"{number} грн";
        }

        private static double Millions(int amount, BoardTheme theme) => amount * (long)MoneyScale(theme) / 1_000_000.0;

        // Единица для подписей и полей ввода: «грн», «$» или «млн грн».
        public static string Currency(BoardTheme theme) =>
            MoneyScale(theme) > 1 ? "млн грн" : theme == BoardTheme.Crypto ? "$" : "грн";

        // Коротко, для аренды на клетке: «16,8к», «252к», «1,4м», на досках в миллионах — «16,8м», «1,4млрд»
        // (на крипто-доске — «$252к») — чтобы не путать с ценой покупки. Знак после запятой — не больше одного.
        public static string ShortMoney(int amount, BoardTheme theme = BoardTheme.Business)
        {
            long value = amount * (long)MoneyScale(theme);
            string text = value >= 1_000_000_000 ? $"{(value / 1_000_000_000.0).ToString("0.#", Ukrainian)}млрд"
                : value >= 1_000_000 ? $"{(value / 1_000_000.0).ToString("0.#", Ukrainian)}м"
                : $"{(value / 1_000.0).ToString("0.#", Ukrainian)}к";
            return theme == BoardTheme.Crypto ? $"${text}" : text;
        }

        // Сумма для поля ввода — в единицах Currency: «150000» или, на досках в миллионах, «150» и «1,5».
        public static string MoneyInput(int amount, BoardTheme theme) =>
            MoneyScale(theme) > 1 ? Millions(amount, theme).ToString("0.#", Ukrainian) : amount.ToString(CultureInfo.InvariantCulture);

        // Сумма из поля ввода (в единицах Currency): пробелы, «грн» и «$» не мешают, у миллионов можно дробь — «1,5» или «1.5».
        // Результат округлён до сотен (RoundMoney). null — не число или слишком много.
        public static int? ParseMoney(string text, BoardTheme theme)
        {
            var number = new string(text.Where(ch => char.IsDigit(ch) || ch is ',' or '.').ToArray()).Replace(',', '.');
            if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
                return null;
            decimal amount = MoneyScale(theme) > 1 ? value * 1_000_000 / MoneyScale(theme) : value;
            return amount <= int.MaxValue ? RoundMoney((int)Math.Round(amount)) : null;
        }

        // Базовая аренда — BaseRentPercent от цены.
        public static int BaseRent(BoardCell cell) => RoundMoney(cell.Price * BaseRentPercent / 100);

        // Аренда за клетку (§5). diceTotal нужен для логистики.
        public static int Rent(IReadOnlyList<BoardCell> board, int cellIndex, int diceTotal)
        {
            var cell = board[cellIndex];
            if (cell.OwnerId is not int owner || cell.IsMortgaged)
                return 0;

            // Заложенные компании не усиливают остальные: считаются только работающие (§5).
            var group = board.Where(c => c.Type == cell.Type).ToList();
            int owned = ActiveInGroup(board, cell.Type, owner);

            switch (cell.Type)
            {
                case CellType.GasStation:
                    return GasStationRent[owned];
                case CellType.Logistics:
                    return diceTotal * (owned == group.Count ? LogisticsBoth : LogisticsSingle);
            }

            if (cell.Level > 0)
                return LevelRent(cell, cell.Level);
            int baseRent = BaseRent(cell);
            return owned == group.Count ? baseRent * MonopolyMultiplier : baseRent;
        }

        // Аренда с филиалами или головным офисом (level 1–5), округлённая: от миллиона — до сотен тысяч.
        public static int LevelRent(BoardCell cell, int level) => RoundMoney(BaseRent(cell) * LevelMultipliers[level]);

        // Сколько компаний группы у владельца приносят доход — незаложенных. От этого числа — аренда АЗС, логистики и ×2 группы.
        public static int ActiveInGroup(IReadOnlyList<BoardCell> board, CellType type, int ownerId) =>
            board.Count(c => c.Type == type && c.OwnerId == ownerId && !c.IsMortgaged);

        // Капитал для итогов партии (§16): деньги + цены компаний (заложенных — половина) + цена построенных филиалов.
        public static int NetWorth(IReadOnlyList<BoardCell> board, int playerId, int balance) =>
            balance + board.Where(c => c.OwnerId == playerId)
                .Sum(c => (c.IsMortgaged ? c.MortgageValue : c.Price) + c.Level * c.BranchCost);

        // Места (§16): победитель, затем оставшиеся по капиталу, затем выбывшие — кто выбыл позже, тот выше.
        public static IReadOnlyList<int> Standings(IReadOnlyList<BoardCell> board, GameSnapshot snapshot, StatsSnapshot stats) =>
            snapshot.Players.Where(p => !p.IsBankrupt)
                .OrderByDescending(p => p.Id == snapshot.WinnerId)
                .ThenByDescending(p => NetWorth(board, p.Id, p.Balance))
                .Select(p => p.Id)
                .Concat(stats.Eliminated.Reverse())
                .ToList();

        // Все компании группы у одного владельца (заложенные тоже считаются) — нужно для постройки филиалов.
        public static bool IsMonopoly(IReadOnlyList<BoardCell> board, CellType type, int ownerId) =>
            board.Where(c => c.Type == type).All(c => c.OwnerId == ownerId);
    }
}
