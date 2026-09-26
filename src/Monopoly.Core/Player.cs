namespace Monopoly.Core
{
    public class Player
    {
        // Номер места за столом (порядок подключения). Не меняется, даже если порядок ходов перемешан.
        public int Id { get; }
        public string Name { get; }
        public int Balance { get; internal set; } = GameRules.StartingBalance;
        public int Position { get; internal set; } = 0;
        // Тюрьма — этап 3.
        public bool IsInJail { get; internal set; } = false;
        public int JailTurns { get; internal set; } = 0;
        // Попал на «Отдых» — пропускает следующий ход.
        public bool IsResting { get; internal set; } = false;

        public Player(int id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
