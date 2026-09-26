namespace Monopoly.Core
{
    // Числа из docs/RULES.md.
    public static class GameRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;
        public const int StartingBalance = 1500;

        // Базовая аренда — 10% цены (§5). Монополии, филиалы, АЗС и логистика — этап 3.
        public static int Rent(BoardCell cell) => cell.Price / 10;
    }
}
