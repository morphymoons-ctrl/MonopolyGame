namespace Monopoly.Core
{
    // Фаза хода текущего игрока.
    public enum TurnPhase
    {
        // Ждём бросок кубиков.
        AwaitingRoll,
        // Игрок стоит на свободной компании: купить или отказаться.
        BuyDecision,
        // Кубики брошены, клетка обработана: управление имуществом, затем конец хода.
        Manage,
    }

    public readonly record struct DiceRoll(int Die1, int Die2)
    {
        public int Total => Die1 + Die2;
        public bool IsDouble => Die1 == Die2;
    }

    // Всё состояние партии. Меняет его только Game.
    public class GameState
    {
        public IReadOnlyList<BoardCell> Board { get; }
        // Игроки в порядке хода.
        public IReadOnlyList<Player> Players { get; }
        public int CurrentPlayerIndex { get; internal set; }
        public TurnPhase Phase { get; internal set; } = TurnPhase.AwaitingRoll;
        // Последний бросок в текущем ходу; null — ещё не бросали.
        public DiceRoll? LastRoll { get; internal set; }

        public Player CurrentPlayer => Players[CurrentPlayerIndex];
        public BoardCell CurrentCell => Board[CurrentPlayer.Position];

        public GameState(IReadOnlyList<BoardCell> board, IReadOnlyList<Player> players)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Players = players ?? throw new ArgumentNullException(nameof(players));
        }

        public Player? FindPlayer(int id) => Players.FirstOrDefault(p => p.Id == id);
    }
}
