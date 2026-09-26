namespace Monopoly.Core
{
    public static class Board
    {
        // 32 cells around a 9x9 grid, clockwise from Start. Corners: 0, 8, 16, 24.
        public const int CellCount = 32;
        public const int SideLength = 9;

        public static List<BoardCell> CreateDefault() => new()
        {
            new BoardCell("Старт", CellType.Start),
            new BoardCell("АТБ", CellType.Supermarket, 100),
            new BoardCell("Варус", CellType.Supermarket, 120),
            new BoardCell("Сильпо", CellType.Supermarket, 140),
            new BoardCell("WOG", CellType.GasStation, 160),
            new BoardCell("АрселорМитал", CellType.Factory, 200),
            new BoardCell("Завод Стасика", CellType.Factory, 220),
            new BoardCell("Азовсталь", CellType.Factory, 240),
            new BoardCell("Тюрьма", CellType.Jail),
            new BoardCell("ТЕТ", CellType.TV, 100),
            new BoardCell("Новый канал", CellType.TV, 120),
            new BoardCell("Интел", CellType.TV, 140),
            new BoardCell("ОККО", CellType.GasStation, 160),
            new BoardCell("Пузата хата", CellType.Food, 200),
            new BoardCell("Pizza Day", CellType.Food, 220),
            new BoardCell("Булочная №1", CellType.Food, 240),
            new BoardCell("Казино", CellType.Casino),
            new BoardCell("Отдых", CellType.Rest),
            new BoardCell("Розетка", CellType.OnlineShop, 100),
            new BoardCell("Пром", CellType.OnlineShop, 120),
            new BoardCell("ОЛХ", CellType.OnlineShop, 140),
            new BoardCell("UPG", CellType.GasStation, 160),
            new BoardCell("Нова пошта", CellType.Logistics, 200),
            new BoardCell("Укрпошта", CellType.Logistics, 220),
            new BoardCell("Шанс", CellType.Chance),
            new BoardCell("ПУМБ", CellType.Bank, 100),
            new BoardCell("Приватбанк", CellType.Bank, 120),
            new BoardCell("Монобанк", CellType.Bank, 140),
            new BoardCell("Укрнафта", CellType.GasStation, 160),
            new BoardCell("Алло", CellType.NetworkShop, 200),
            new BoardCell("Цитрус", CellType.NetworkShop, 220),
            new BoardCell("Фокстрот", CellType.NetworkShop, 240),
        };
    }
}
