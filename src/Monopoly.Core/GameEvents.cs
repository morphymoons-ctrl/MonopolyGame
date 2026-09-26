namespace Monopoly.Core
{
    // События движка: что произошло в игре. Интерфейс их только показывает.
    // Игроки — по Id, клетки — по номеру на поле.
    public abstract record GameEvent;

    // Seed — зерно генератора, по нему партию можно повторить; null, если генератор без зерна (тесты).
    public sealed record GameStarted(int? Seed, IReadOnlyList<int> TurnOrder) : GameEvent;

    public sealed record TurnStarted(int PlayerId) : GameEvent;

    public sealed record DiceRolled(int PlayerId, int Die1, int Die2) : GameEvent
    {
        public int Total => Die1 + Die2;
    }

    public sealed record PlayerMoved(int PlayerId, int From, int To) : GameEvent;

    public sealed record PurchaseOffered(int PlayerId, int CellIndex, int Price) : GameEvent;

    public sealed record PropertyBought(int PlayerId, int CellIndex, int Price) : GameEvent;

    public sealed record PurchaseDeclined(int PlayerId, int CellIndex) : GameEvent;

    public sealed record RentPaid(int PayerId, int OwnerId, int CellIndex, int Amount) : GameEvent;

    // Игрок попал на «Отдых» и пропустит следующий ход.
    public sealed record RestStarted(int PlayerId) : GameEvent;

    public sealed record TurnSkipped(int PlayerId) : GameEvent;
}
