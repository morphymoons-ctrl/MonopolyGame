namespace Monopoly.Core
{
    // Частота событий (RULES.md, §17) — выбирает хост в лобби.
    public enum EventFrequency { Off, Rare, Normal, Often }

    // События партии (§17). Порядок не менять: по номеру события выбираются из генератора, от него зависят повторы партий.
    public enum WorldEventKind
    {
        Inflation,
        Crisis,
        Boom,
        LuxuryTax,
        StateAid,
        Solidarity,
        BuildingBan,
        Privatization,
        DemandSeason,
        Sanctions,
        Blackout,
        Raid,
        Jackpot,
        Quarantine,
    }

    // Идущее событие: началось в круге StartRound и действует Rounds кругов. Group — у «Сезона попиту» и «Санкцій».
    public sealed record ActiveEvent(WorldEventKind Kind, int StartRound, int Rounds, CellType? Group = null)
    {
        public int EndRound => StartRound + Rounds;
        public int RoundsLeft(int round) => EndRound - round;
    }

    // Что влияет на аренду, кроме самой клетки: индекс цен (инфляция) и идущие события.
    public sealed record RentContext(int PriceIndex, IReadOnlyList<ActiveEvent> Events)
    {
        public static RentContext None { get; } = new(100, Array.Empty<ActiveEvent>());
    }

    public static class WorldEvents
    {
        // Первое событие — в начале 3-го круга.
        public const int FirstRound = 3;
        // Одновременно — не больше двух событий на несколько кругов.
        public const int MaxLasting = 2;
        // Инфляция: +10% за раз.
        public const int InflationPercent = 10;
        public const int StateAid = 100_000, StateAidPoorest = 200_000;
        public const int LuxuryTaxPercent = 10, SolidarityPercent = 10;
        // «Велика приватизація»: первая ставка — от 50% цены.
        public const int PrivatizationStartPercent = 50;

        // Сколько кругов между событиями: от и до включительно.
        public static (int Min, int Max) Gap(EventFrequency frequency) => frequency switch
        {
            EventFrequency.Rare => (5, 7),
            EventFrequency.Often => (2, 3),
            _ => (3, 5),
        };

        // Сколько кругов действует событие; 0 — мгновенное или до конца игры (инфляция).
        public static int Duration(WorldEventKind kind) => kind switch
        {
            WorldEventKind.Crisis or WorldEventKind.Boom or WorldEventKind.BuildingBan or WorldEventKind.DemandSeason
                or WorldEventKind.Sanctions or WorldEventKind.Raid or WorldEventKind.Jackpot => 2,
            WorldEventKind.Blackout or WorldEventKind.Quarantine => 1,
            _ => 0,
        };

        public static bool IsLasting(WorldEventKind kind) => Duration(kind) > 0;

        // Семь цветных групп — для «Сезона попиту» и «Санкцій».
        public static readonly IReadOnlyList<CellType> ColorGroups = new[]
        {
            CellType.Supermarket, CellType.Factory, CellType.TV, CellType.Food, CellType.Nightlife, CellType.Bank, CellType.NetworkShop,
        };

        // Сумма с учётом инфляции: бонус «Старта», аренда АЗС и логистики.
        public static int Indexed(int amount, int priceIndex) =>
            priceIndex == 100 ? amount : GameRules.RoundMoney((int)((long)amount * priceIndex / 100));

        // Во сколько процентов обычной аренда группы сейчас — все события перемножаются.
        public static int RentPercent(IReadOnlyList<ActiveEvent> events, CellType type)
        {
            long percent = 100;
            foreach (var e in events)
            {
                percent = e.Kind switch
                {
                    WorldEventKind.Crisis => percent * 50 / 100,
                    WorldEventKind.Boom => percent * 150 / 100,
                    WorldEventKind.DemandSeason when e.Group == type => percent * 2,
                    WorldEventKind.Sanctions when e.Group == type => 0,
                    WorldEventKind.Blackout when type is CellType.GasStation or CellType.Logistics => 0,
                    WorldEventKind.Quarantine => 0,
                    _ => percent,
                };
            }
            return (int)percent;
        }

        // Какое событие отменило аренду группы; null — аренда берётся.
        public static WorldEventKind? RentBlockedBy(IReadOnlyList<ActiveEvent> events, CellType type) =>
            events.FirstOrDefault(e => e.Kind switch
            {
                WorldEventKind.Sanctions => e.Group == type,
                WorldEventKind.Blackout => type is CellType.GasStation or CellType.Logistics,
                WorldEventKind.Quarantine => true,
                _ => false,
            })?.Kind;

        // Аренда с учётом событий, округлённая (§5).
        public static int AdjustRent(int rent, CellType type, RentContext context)
        {
            int percent = RentPercent(context.Events, type);
            return percent == 100 ? rent : GameRules.RoundMoney((int)((long)rent * percent / 100));
        }

        public static bool IsActive(IReadOnlyList<ActiveEvent> events, WorldEventKind kind) => events.Any(e => e.Kind == kind);

        // «1 коло», «2 кола», «5 кіл».
        public static string Rounds(int n)
        {
            int last = n % 10, lastTwo = n % 100;
            string word = last == 1 && lastTwo != 11 ? "коло" : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "кола" : "кіл";
            return $"{n} {word}";
        }
    }
}
