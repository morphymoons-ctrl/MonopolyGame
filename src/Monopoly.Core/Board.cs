namespace Monopoly.Core
{
    // Тематика доски (RULES.md, §15). Раскладка, группы и цены у всех одинаковые — отличаются названия.
    public enum BoardTheme
    {
        // «Українські бізнеси» — основная доска.
        Business,
        // «Військова інфраструктура України».
        Military,
    }

    public static class Board
    {
        // 32 клетки по краю сетки 9x9, по часовой стрелке от «Старта». Углы: 0, 8, 16, 24.
        public const int CellCount = 32;
        public const int SideLength = 9;

        public static List<BoardCell> CreateDefault() => Create(BoardTheme.Business);

        public static List<BoardCell> Create(BoardTheme theme) => theme switch
        {
            BoardTheme.Military => Military(),
            _ => Business(),
        };

        private static List<BoardCell> Business() => new()
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

        // Те же типы и цены, что у основной доски, по тем же номерам клеток (§15).
        private static List<BoardCell> Military() => new()
        {
            new BoardCell("Пункт збору", CellType.Start),
            new BoardCell("Піхота", CellType.Supermarket, 100_000),
            new BoardCell("Легка бронетехніка", CellType.Supermarket, 120_000),
            new BoardCell("Танкові війська", CellType.Supermarket, 140_000),
            new BoardCell("ОК «Північ»", CellType.GasStation, 160_000),
            new BoardCell("Бронетанковий завод", CellType.Factory, 200_000),
            new BoardCell("Завод боєприпасів", CellType.Factory, 220_000),
            new BoardCell("Ракетний завод", CellType.Factory, 240_000),
            new BoardCell("Гауптвахта", CellType.Jail),
            new BoardCell("Військовий зв'язок", CellType.TV, 100_000),
            new BoardCell("РЕБ", CellType.TV, 120_000),
            new BoardCell("Розвідка", CellType.TV, 140_000),
            new BoardCell("ОК «Схід»", CellType.GasStation, 160_000),
            new BoardCell("Мінометна батарея", CellType.Food, 200_000),
            new BoardCell("Гаубична артилерія", CellType.Food, 220_000),
            new BoardCell("РСЗВ HIMARS", CellType.Food, 240_000),
            new BoardCell("Карти в бліндажі", CellType.Casino),
            new BoardCell("Відпустка", CellType.Rest),
            new BoardCell("FPV-дрони", CellType.Nightlife, 100_000),
            new BoardCell("Розвідувальні БПЛА", CellType.Nightlife, 120_000),
            new BoardCell("Ударні БПЛА", CellType.Nightlife, 140_000),
            new BoardCell("ОК «Південь»", CellType.GasStation, 160_000),
            new BoardCell("Військова залізниця", CellType.Logistics, 200_000),
            new BoardCell("Медична служба", CellType.Logistics, 220_000),
            new BoardCell("Наказ", CellType.Chance),
            new BoardCell("Морська піхота", CellType.Bank, 100_000),
            new BoardCell("Катери", CellType.Bank, 120_000),
            new BoardCell("Морські дрони", CellType.Bank, 140_000),
            new BoardCell("ОК «Захід»", CellType.GasStation, 160_000),
            new BoardCell("ЗРК «Бук»", CellType.NetworkShop, 200_000),
            new BoardCell("IRIS-T", CellType.NetworkShop, 220_000),
            new BoardCell("Patriot", CellType.NetworkShop, 240_000),
        };
    }
}
