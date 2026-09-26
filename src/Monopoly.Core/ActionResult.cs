namespace Monopoly.Core
{
    // Результат действия: либо список событий, либо понятное игроку сообщение об ошибке.
    public sealed class ActionResult
    {
        public string? Error { get; }
        public IReadOnlyList<GameEvent> Events { get; }

        public bool Success => Error is null;

        private ActionResult(string? error, IReadOnlyList<GameEvent> events)
        {
            Error = error;
            Events = events;
        }

        public static ActionResult Ok(IReadOnlyList<GameEvent> events) => new(null, events);

        public static ActionResult Fail(string error) => new(error, Array.Empty<GameEvent>());
    }
}
