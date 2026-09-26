using Monopoly.Core;

namespace Monopoly.Net
{
    // Сообщения между хостом и клиентами.

    // HostToken знает только приложение хоста: так его собственный клиент становится хостом лобби.
    public sealed record JoinRequest(string Name, string Version, string? HostToken = null);

    public sealed record JoinResult(string? Error, int SeatId);

    // Обёртка нужна для JSON: аргумент метода SignalR пишется по своему настоящему типу, без поля "$type",
    // а у свойства типа GameAction тип действия сохраняется.
    public sealed record ActionRequest(GameAction Action);

    public sealed record LobbySeat(int SeatId, string Name, int ColorIndex, bool IsReady, bool IsHost);

    // StartBlockedReason — почему хост пока не может начать; null — можно начинать.
    public sealed record LobbyState(IReadOnlyList<LobbySeat> Seats, int MaxPlayers, string? StartBlockedReason);

    // Отправляется каждому игроку при старте: кто он в партии и какого цвета фишки у всех.
    public sealed record GameStartInfo(int MyPlayerId, IReadOnlyDictionary<int, int> ColorByPlayerId);

    // После каждого действия: новые события, состояние и что этот игрок может сделать сейчас.
    public sealed record GameUpdate(
        IReadOnlyList<GameEvent> Events,
        GameSnapshot Snapshot,
        IReadOnlyList<GameAction> AvailableActions);

    // Ответ хоста на поиск игр. Address заполняет клиент — по адресу, откуда пришёл ответ.
    public sealed record DiscoveredGame(
        Guid GameId,
        string HostName,
        string Version,
        int Port,
        int Players,
        int MaxPlayers,
        bool InProgress,
        string Address = "");

    // Имена методов клиента, которые вызывает хост.
    internal static class ClientMethods
    {
        public const string Lobby = "Lobby";
        public const string GameStart = "GameStart";
        public const string Update = "Update";
        public const string Notice = "Notice";
    }
}
