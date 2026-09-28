namespace Monopoly.Core
{
    // Статистика партии (RULES.md, §16). Ведёт только движок; сохранение восстанавливает её само — повтором действий.
    public sealed class GameStats
    {
        private readonly Dictionary<int, PlayerStats> players = new();
        private readonly int[] cellIncome;
        private readonly List<int> eliminated = new();

        public GameStats(int cellCount)
        {
            cellIncome = new int[cellCount];
        }

        // Начатые ходы (пропущенные не считаются).
        public int Turns { get; private set; }

        public PlayerStats For(int playerId)
        {
            if (!players.TryGetValue(playerId, out var stats))
                players[playerId] = stats = new PlayerStats();
            return stats;
        }

        internal void TurnStarted() => Turns++;

        // Аренда начислена: платит payer, получает owner, приносит клетка cellIndex — даже если платить пришлось через долг.
        internal void Rent(int payerId, int ownerId, int cellIndex, int amount)
        {
            For(payerId).RentPaid += amount;
            For(ownerId).RentReceived += amount;
            cellIncome[cellIndex] += amount;
        }

        internal void Jailed(int playerId) => For(playerId).TimesJailed++;

        internal void Casino(int playerId, int net) => For(playerId).CasinoNet += net;

        internal void Bought(int playerId) => For(playerId).CompaniesBought++;

        internal void Bankrupt(int playerId) => eliminated.Add(playerId);

        public StatsSnapshot ToSnapshot(IEnumerable<int> playerIds) => new(
            Turns,
            playerIds.Select(id =>
            {
                var s = For(id);
                return new PlayerStatsSnapshot(id, s.RentPaid, s.RentReceived, s.TimesJailed, s.CasinoNet, s.CompaniesBought);
            }).ToList(),
            cellIncome.ToList(),
            eliminated.ToList());
    }

    public sealed class PlayerStats
    {
        public int RentPaid { get; internal set; }
        public int RentReceived { get; internal set; }
        public int TimesJailed { get; internal set; }
        public int CasinoNet { get; internal set; }
        public int CompaniesBought { get; internal set; }
    }

    // Для клиентов. CellIncome — аренда, начисленная на каждой клетке за партию; Eliminated — выбывшие в порядке выбывания.
    public sealed record StatsSnapshot(
        int Turns,
        IReadOnlyList<PlayerStatsSnapshot> Players,
        IReadOnlyList<int> CellIncome,
        IReadOnlyList<int> Eliminated)
    {
        public PlayerStatsSnapshot? For(int playerId) => Players.FirstOrDefault(p => p.PlayerId == playerId);
    }

    public sealed record PlayerStatsSnapshot(int PlayerId, int RentPaid, int RentReceived, int TimesJailed, int CasinoNet, int CompaniesBought);
}
