namespace Monopoly.Core
{
    public static class Board
    {
        // 32 клетки по краю сетки 9x9, по часовой стрелке от «Старта». Углы: 0, 8, 16, 24.
        public const int CellCount = 32;
        public const int SideLength = 9;

        public static List<BoardCell> CreateDefault() => new()
        {
            new BoardCell("Старт", CellType.Start),
            new BoardCell("АТБ", CellType.Supermarket, 100),
            new BoardCell("Варус", CellType.Supermarket, 120),
            new BoardCell("Сільпо", CellType.Supermarket, 140),
            new BoardCell("WOG", CellType.GasStation, 160),
            new BoardCell("АрселорМіттал", CellType.Factory, 200),
            new BoardCell("Завод Стасика", CellType.Factory, 220),
            new BoardCell("Азовсталь", CellType.Factory, 240),
            new BoardCell("Пєтушатня", CellType.Jail),
            new BoardCell("ТЕТ", CellType.TV, 100),
            new BoardCell("Новий канал", CellType.TV, 120),
            new BoardCell("Інтел", CellType.TV, 140),
            new BoardCell("ОККО", CellType.GasStation, 160),
            new BoardCell("Пузата хата", CellType.Food, 200),
            new BoardCell("Pizza Day", CellType.Food, 220),
            new BoardCell("Булочна №1", CellType.Food, 240),
            new BoardCell("Казино", CellType.Casino),
            new BoardCell("Зачілься", CellType.Rest),
            new BoardCell("Масажка", CellType.Nightlife, 100),
            new BoardCell("Бордель", CellType.Nightlife, 120),
            new BoardCell("Стрипклуб", CellType.Nightlife, 140),
            new BoardCell("UPG", CellType.GasStation, 160),
            new BoardCell("Нова пошта", CellType.Logistics, 200),
            new BoardCell("Укрпошта", CellType.Logistics, 220),
            new BoardCell("Шанс", CellType.Chance),
            new BoardCell("ПУМБ", CellType.Bank, 100),
            new BoardCell("ПриватБанк", CellType.Bank, 120),
            new BoardCell("Монобанк", CellType.Bank, 140),
            new BoardCell("Укрнафта", CellType.GasStation, 160),
            new BoardCell("Алло", CellType.NetworkShop, 200),
            new BoardCell("Цитрус", CellType.NetworkShop, 220),
            new BoardCell("Фокстрот", CellType.NetworkShop, 240),
        };
    }
}
