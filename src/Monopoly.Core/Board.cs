namespace Monopoly.Core
{
    // Тематика доски (RULES.md, §15). Раскладка, группы и цены у всех одинаковые — отличаются названия.
    public enum BoardTheme
    {
        // «Українські бізнеси» — основная доска.
        Business,
        // «Військова інфраструктура України».
        Military,
        // «Уряд України».
        Government,
        // «Криптовалюти».
        Crypto,
        // «Відеоігри».
        Games,
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
            BoardTheme.Government => Government(),
            BoardTheme.Crypto => Crypto(),
            BoardTheme.Games => Games(),
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
            new BoardCell("Піхота", CellType.Supermarket, 90_000),
            new BoardCell("Легка бронетехніка", CellType.Supermarket, 120_000),
            new BoardCell("Танкові війська", CellType.Supermarket, 150_000),
            new BoardCell("ОК „Північ“", CellType.GasStation, 150_000),
            new BoardCell("Бронетанковий завод", CellType.Factory, 200_000),
            new BoardCell("Завод боєприпасів", CellType.Factory, 190_000),
            new BoardCell("Ракетний завод", CellType.Factory, 270_000),
            new BoardCell("Гауптвахта", CellType.Jail),
            new BoardCell("Військовий зв'язок", CellType.TV, 90_000),
            new BoardCell("РЕБ", CellType.TV, 130_000),
            new BoardCell("Розвідка", CellType.TV, 140_000),
            new BoardCell("ОК „Схід“", CellType.GasStation, 180_000),
            new BoardCell("Мінометна батарея", CellType.Food, 180_000),
            new BoardCell("Гаубична артилерія", CellType.Food, 210_000),
            new BoardCell("РСЗВ HIMARS", CellType.Food, 270_000),
            new BoardCell("Карти в бліндажі", CellType.Casino),
            new BoardCell("Відпустка", CellType.Rest),
            new BoardCell("FPV-дрони", CellType.Nightlife, 80_000),
            new BoardCell("Розвідувальні БПЛА", CellType.Nightlife, 110_000),
            new BoardCell("Ударні БПЛА", CellType.Nightlife, 170_000),
            new BoardCell("ОК „Південь“", CellType.GasStation, 170_000),
            new BoardCell("Військова залізниця", CellType.Logistics, 230_000),
            new BoardCell("Медична служба", CellType.Logistics, 190_000),
            new BoardCell("Наказ", CellType.Chance),
            new BoardCell("Морська піхота", CellType.Bank, 110_000),
            new BoardCell("Катери", CellType.Bank, 100_000),
            new BoardCell("Морські дрони", CellType.Bank, 150_000),
            new BoardCell("ОК „Захід“", CellType.GasStation, 140_000),
            new BoardCell("ЗРК „Бук“", CellType.NetworkShop, 180_000),
            new BoardCell("IRIS-T", CellType.NetworkShop, 200_000),
            new BoardCell("Patriot", CellType.NetworkShop, 280_000),
        };

        // «Уряд України» — те же типы и цены по тем же номерам (§15).
        private static List<BoardCell> Government() => new()
        {
            new BoardCell("Банкова", CellType.Start),
            new BoardCell("Патрульна поліція", CellType.Supermarket, 90_000),
            new BoardCell("Національна поліція", CellType.Supermarket, 120_000),
            new BoardCell("Національна гвардія", CellType.Supermarket, 150_000),
            new BoardCell("КПП „Ягодин“", CellType.GasStation, 160_000),
            new BoardCell("СБУ", CellType.Factory, 230_000),
            new BoardCell("ГУР", CellType.Factory, 250_000),
            new BoardCell("СЗРУ", CellType.Factory, 180_000),
            new BoardCell("Лук'янівське СІЗО", CellType.Jail),
            new BoardCell("НАЗК", CellType.TV, 90_000),
            new BoardCell("САП", CellType.TV, 110_000),
            new BoardCell("НАБУ", CellType.TV, 160_000),
            new BoardCell("КПП „Чоп“", CellType.GasStation, 150_000),
            new BoardCell("Печерський суд", CellType.Food, 180_000),
            new BoardCell("Верховний Суд", CellType.Food, 220_000),
            new BoardCell("Конституційний Суд", CellType.Food, 260_000),
            new BoardCell("Кнопкодавство", CellType.Casino),
            new BoardCell("Закордонне відрядження", CellType.Rest),
            new BoardCell("Податкова", CellType.Nightlife, 130_000),
            new BoardCell("Митниця", CellType.Nightlife, 140_000),
            new BoardCell("БЕБ", CellType.Nightlife, 90_000),
            new BoardCell("КПП „Краковець“", CellType.GasStation, 170_000),
            new BoardCell("Нафтогаз", CellType.Logistics, 240_000),
            new BoardCell("Укренерго", CellType.Logistics, 180_000),
            new BoardCell("Указ", CellType.Chance),
            new BoardCell("Дія", CellType.Bank, 150_000),
            new BoardCell("Резерв+", CellType.Bank, 110_000),
            new BoardCell("Армія+", CellType.Bank, 100_000),
            new BoardCell("КПП „Шегині“", CellType.GasStation, 160_000),
            new BoardCell("Кабмін", CellType.NetworkShop, 190_000),
            new BoardCell("Верховна Рада", CellType.NetworkShop, 200_000),
            new BoardCell("Офіс Президента", CellType.NetworkShop, 270_000),
        };

        // «Криптовалюти» — те же типы и цены по тем же номерам (§15).
        private static List<BoardCell> Crypto() => new()
        {
            new BoardCell("Генезис-блок", CellType.Start),
            new BoardCell("Dogecoin", CellType.Supermarket, 150_000),
            new BoardCell("Shiba Inu", CellType.Supermarket, 110_000),
            new BoardCell("Pepe", CellType.Supermarket, 100_000),
            new BoardCell("Ферма в гаражі", CellType.GasStation, 100_000),
            new BoardCell("Binance", CellType.Factory, 270_000),
            new BoardCell("Coinbase", CellType.Factory, 240_000),
            new BoardCell("WhiteBIT", CellType.Factory, 150_000),
            new BoardCell("Блокування акаунта", CellType.Jail),
            new BoardCell("MetaMask", CellType.TV, 120_000),
            new BoardCell("Trust Wallet", CellType.TV, 100_000),
            new BoardCell("Ledger", CellType.TV, 140_000),
            new BoardCell("Ферма в Техасі", CellType.GasStation, 200_000),
            new BoardCell("Solana", CellType.Food, 250_000),
            new BoardCell("TON", CellType.Food, 210_000),
            new BoardCell("Cardano", CellType.Food, 200_000),
            new BoardCell("Трейдинг з плечем", CellType.Casino),
            new BoardCell("HODL", CellType.Rest),
            new BoardCell("Bored Ape", CellType.Nightlife, 130_000),
            new BoardCell("CryptoPunks", CellType.Nightlife, 150_000),
            new BoardCell("Pudgy Penguins", CellType.Nightlife, 80_000),
            new BoardCell("Ферма в Ісландії", CellType.GasStation, 170_000),
            new BoardCell("USDT", CellType.Logistics, 240_000),
            new BoardCell("USDC", CellType.Logistics, 180_000),
            new BoardCell("Твіт Ілона", CellType.Chance),
            new BoardCell("Uniswap", CellType.Bank, 150_000),
            new BoardCell("Aave", CellType.Bank, 120_000),
            new BoardCell("PancakeSwap", CellType.Bank, 90_000),
            new BoardCell("Ферма в Казахстані", CellType.GasStation, 170_000),
            new BoardCell("BNB", CellType.NetworkShop, 170_000),
            new BoardCell("Ethereum", CellType.NetworkShop, 210_000),
            new BoardCell("Bitcoin", CellType.NetworkShop, 280_000),
        };

        // «Відеоігри» — те же типы и цены по тем же номерам (§15).
        private static List<BoardCell> Games() => new()
        {
            new BoardCell("Головне меню", CellType.Start),
            new BoardCell("Brawl Stars", CellType.Supermarket, 130_000),
            new BoardCell("Clash Royale", CellType.Supermarket, 120_000),
            new BoardCell("Subway Surfers", CellType.Supermarket, 110_000),
            new BoardCell("Steam", CellType.GasStation, 200_000),
            new BoardCell("Valorant", CellType.Factory, 200_000),
            new BoardCell("Call of Duty", CellType.Factory, 200_000),
            new BoardCell("Counter-Strike 2", CellType.Factory, 260_000),
            new BoardCell("Бан за читерство", CellType.Jail),
            new BoardCell("Stardew Valley", CellType.TV, 120_000),
            new BoardCell("Terraria", CellType.TV, 110_000),
            new BoardCell("Hollow Knight", CellType.TV, 130_000),
            new BoardCell("Epic Games", CellType.GasStation, 130_000),
            new BoardCell("Козаки 3", CellType.Food, 180_000),
            new BoardCell("Metro Exodus", CellType.Food, 220_000),
            new BoardCell("S.T.A.L.K.E.R. 2", CellType.Food, 260_000),
            new BoardCell("Кейси в CS", CellType.Casino),
            new BoardCell("AFK", CellType.Rest),
            new BoardCell("Roblox", CellType.Nightlife, 130_000),
            new BoardCell("Garry's Mod", CellType.Nightlife, 80_000),
            new BoardCell("Minecraft", CellType.Nightlife, 150_000),
            new BoardCell("PlayStation", CellType.GasStation, 170_000),
            new BoardCell("Twitch", CellType.Logistics, 190_000),
            new BoardCell("YouTube", CellType.Logistics, 230_000),
            new BoardCell("Лутбокс", CellType.Chance),
            new BoardCell("PUBG", CellType.Bank, 110_000),
            new BoardCell("Apex Legends", CellType.Bank, 100_000),
            new BoardCell("Fortnite", CellType.Bank, 150_000),
            new BoardCell("Xbox", CellType.GasStation, 140_000),
            new BoardCell("The Witcher 3", CellType.NetworkShop, 200_000),
            new BoardCell("GTA V", CellType.NetworkShop, 230_000),
            new BoardCell("Dota 2", CellType.NetworkShop, 230_000),
        };
    }
}
