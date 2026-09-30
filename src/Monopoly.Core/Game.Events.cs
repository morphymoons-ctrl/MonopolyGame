namespace Monopoly.Core
{
    // События партии (RULES.md, §17): в начале круга заканчиваются старые и, когда подходит очередь, начинается новое.
    public partial class Game
    {
        // internal — для тестов: круг и события можно проверить по одному.
        internal void StartRound(List<GameEvent> events)
        {
            State.Round++;
            events.Add(new RoundStarted(State.Round));

            foreach (var ended in State.Events.Where(e => e.EndRound <= State.Round).ToList())
            {
                State.Events.Remove(ended);
                events.Add(new WorldEventEnded(ended.Kind, ended.Group));
            }

            if (options.Events == EventFrequency.Off || State.Round < State.NextEventRound)
                return;
            var (min, max) = WorldEvents.Gap(options.Events);
            State.NextEventRound = State.Round + random.Next(min, max + 1);
            var candidates = Enum.GetValues<WorldEventKind>().Where(CanStart).ToList();
            if (candidates.Count > 0)
                StartEvent(candidates[random.Next(0, candidates.Count)], events);
        }

        // Может ли событие начаться сейчас (§17).
        internal bool CanStart(WorldEventKind kind)
        {
            var active = State.Events;
            if (WorldEvents.IsLasting(kind))
            {
                if (active.Count >= WorldEvents.MaxLasting || WorldEvents.IsActive(active, kind))
                    return false;
                if (kind is WorldEventKind.Crisis && WorldEvents.IsActive(active, WorldEventKind.Boom)
                    || kind is WorldEventKind.Boom && WorldEvents.IsActive(active, WorldEventKind.Crisis))
                    return false;
            }
            return kind switch
            {
                WorldEventKind.DemandSeason or WorldEventKind.Sanctions => FreeGroups().Count > 0,
                WorldEventKind.Privatization => FreeCompanies().Count > 0,
                _ => true,
            };
        }

        // Цветные группы, которые ещё не под «Сезоном» или «Санкціями».
        private List<CellType> FreeGroups() =>
            WorldEvents.ColorGroups.Where(g => !State.Events.Any(e => e.Group == g)).ToList();

        private List<int> FreeCompanies() =>
            Enumerable.Range(0, State.Board.Count).Where(i => State.Board[i].IsPurchasable && State.Board[i].OwnerId is null).ToList();

        // group — только для тестов: группа «Сезона» или «Санкцій» без генератора.
        internal void StartEvent(WorldEventKind kind, List<GameEvent> events, CellType? group = null)
        {
            int rounds = WorldEvents.Duration(kind);
            if (group is null && kind is WorldEventKind.DemandSeason or WorldEventKind.Sanctions)
            {
                var groups = FreeGroups();
                group = groups[random.Next(0, groups.Count)];
            }
            if (rounds > 0)
                State.Events.Add(new ActiveEvent(kind, State.Round, rounds, group));
            events.Add(new WorldEventStarted(kind, rounds, group));

            var players = State.ActivePlayers.ToList();
            switch (kind)
            {
                case WorldEventKind.Inflation:
                    Inflate();
                    break;
                case WorldEventKind.LuxuryTax:
                    foreach (var p in players)
                    {
                        int tax = GameRules.RoundMoney(p.Balance * WorldEvents.LuxuryTaxPercent / 100);
                        if (tax <= 0)
                            continue;
                        p.Balance -= tax;
                        events.Add(new PaidToBank(p.Id, tax));
                    }
                    break;
                case WorldEventKind.StateAid:
                {
                    int poorest = Poorest(players).Id;
                    foreach (var p in players)
                    {
                        int aid = p.Id == poorest ? WorldEvents.StateAidPoorest : WorldEvents.StateAid;
                        p.Balance += aid;
                        events.Add(new ReceivedFromBank(p.Id, aid));
                    }
                    break;
                }
                case WorldEventKind.Solidarity:
                {
                    var rich = Richest(players);
                    var poor = Poorest(players);
                    int amount = GameRules.RoundMoney(rich.Balance * WorldEvents.SolidarityPercent / 100);
                    if (rich != poor && amount > 0)
                    {
                        rich.Balance -= amount;
                        poor.Balance += amount;
                        events.Add(new PaidToPlayer(rich.Id, poor.Id, amount));
                    }
                    break;
                }
                case WorldEventKind.Privatization:
                    foreach (int cell in FreeCompanies())
                        State.PrivatizationQueue.Enqueue(cell);
                    StartNextPrivatization(events);
                    break;
            }
        }

        // Инфляция: цены компаний и филиалов +10% (от них — аренда, залог, выкуп); бонус «Старта», АЗС и логистика — через индекс цен.
        private void Inflate()
        {
            foreach (var cell in State.Board.Where(c => c.IsPurchasable))
            {
                cell.Price = GameRules.RoundMoney(cell.Price * (100 + WorldEvents.InflationPercent) / 100);
                if (cell.IsBuildable)
                    cell.BranchCost = GameRules.RoundMoney(cell.BranchCost * (100 + WorldEvents.InflationPercent) / 100);
            }
            State.PriceIndex = State.PriceIndex * (100 + WorldEvents.InflationPercent) / 100;
        }

        // Богатство — по капиталу (§16); при равенстве — первый в порядке ходов.
        private Player Richest(List<Player> players) =>
            players.OrderByDescending(p => GameRules.NetWorth(State.Board, p.Id, p.Balance)).First();

        private Player Poorest(List<Player> players) =>
            players.OrderBy(p => GameRules.NetWorth(State.Board, p.Id, p.Balance)).First();

        // «Велика приватизація»: следующая свободная компания — на аукцион от 50% цены, без находчика (§17).
        private void StartNextPrivatization(List<GameEvent> events)
        {
            while (State.PrivatizationQueue.Count > 0)
            {
                int cellIndex = State.PrivatizationQueue.Dequeue();
                var cell = State.Board[cellIndex];
                if (cell.OwnerId is not null)
                    continue;
                int startBid = GameRules.RoundMoney(cell.Price * WorldEvents.PrivatizationStartPercent / 100);
                State.Auction = new AuctionState(cellIndex, startBid, null);
                events.Add(new AuctionStarted(cellIndex, startBid, null));
                return;
            }
        }
    }
}
