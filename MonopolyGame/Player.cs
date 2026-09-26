namespace MonopolyGame
{
    public class Player
    {
        public string Name { get; }
        public int Balance { get; set; } = 1500;
        public int Position { get; set; } = 0;
        public bool IsInJail { get; set; } = false;
        public int JailTurns { get; set; } = 0;

        public Player(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}