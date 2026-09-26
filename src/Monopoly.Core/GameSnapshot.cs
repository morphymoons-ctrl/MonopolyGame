namespace Monopoly.Core
{
    // Снимок состояния для показа: хост отправляет его клиентам после каждого действия.
    // Поле (названия, цены) не входит — оно одинаковое у всех, см. Board.CreateDefault.
    public sealed record GameSnapshot(
        IReadOnlyList<PlayerSnapshot> Players,
        IReadOnlyList<int?> Owners,
        int CurrentPlayerId,
        TurnPhase Phase,
        DiceRoll? LastRoll)
    {
        public PlayerSnapshot? FindPlayer(int id) => Players.FirstOrDefault(p => p.Id == id);
    }

    // Игроки в снимке идут в порядке хода.
    public sealed record PlayerSnapshot(int Id, string Name, int Balance, int Position, bool IsInJail, bool IsResting);
}
