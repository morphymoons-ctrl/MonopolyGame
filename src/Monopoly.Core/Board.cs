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
            new BoardCell("АКБ", CellType.Supermarket, 100_000),
            new BoardCell("Vanus", CellType.Supermarket, 120_000),
            new BoardCell("Сілько", CellType.Supermarket, 140_000),
            new BoardCell("MOG", CellType.GasStation, 160_000),
            new BoardCell("ArserlorMettal", CellType.Factory, 200_000),
            new BoardCell("Завод Стасика", CellType.Factory, 220_000),
            new BoardCell("Азовстіль", CellType.Factory, 240_000),
            new BoardCell("Пєтушатня", CellType.Jail),
            new BoardCell("ТОТ", CellType.TV, 100_000),
            new BoardCell("Старий Канал", CellType.TV, 120_000),
            new BoardCell("ГЮНТЕР", CellType.TV, 140_000),
            new BoardCell("ОЗЗО", CellType.GasStation, 160_000),
            new BoardCell("Пузата Халупа", CellType.Food, 200_000),
            new BoardCell("Pizza Night", CellType.Food, 220_000),
            new BoardCell("Булочна №2", CellType.Food, 240_000),
            new BoardCell("Казино", CellType.Casino),
            new BoardCell("Зачілься", CellType.Rest),
            new BoardCell("Масажка", CellType.Nightlife, 100_000),
            new BoardCell("Бордель", CellType.Nightlife, 120_000),
            new BoardCell("Стрипклуб", CellType.Nightlife, 140_000),
            new BoardCell("UPC", CellType.GasStation, 160_000),
            new BoardCell("Стара Пошта", CellType.Logistics, 200_000),
            new BoardCell("Укірпошта", CellType.Logistics, 220_000),
            new BoardCell("Шанс", CellType.Chance),
            new BoardCell("ПІМБ", CellType.Bank, 100_000),
            new BoardCell("ПривітБанк", CellType.Bank, 120_000),
            new BoardCell("Мінібанк", CellType.Bank, 140_000),
            new BoardCell("Укрнафла", CellType.GasStation, 160_000),
            new BoardCell("Алльо", CellType.NetworkShop, 200_000),
            new BoardCell("Цітрус", CellType.NetworkShop, 220_000),
            new BoardCell("Фокстріт", CellType.NetworkShop, 240_000),
        };
    }
}
