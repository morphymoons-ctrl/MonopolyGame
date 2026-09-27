namespace Monopoly.Core
{
    public class Player
    {
        // Номер места за столом (порядок подключения). Не меняется, даже если порядок ходов перемешан.
        public int Id { get; }
        public string Name { get; }
        public int Balance { get; internal set; } = GameRules.StartingBalance;
        public int Position { get; internal set; } = 0;
        // В пєтушатні — пропускает следующий ход (§6).
        public bool IsInJail { get; internal set; } = false;
        // Попал на «Зачілься» — пропускает следующий ход (§7).
        public bool IsResting { get; internal set; } = false;
        // Карточки «Выйти из тюрьмы бесплатно».
        public int JailCards { get; internal set; } = 0;
        public bool IsBankrupt { get; internal set; } = false;

        public Player(int id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
