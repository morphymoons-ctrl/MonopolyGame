using Monopoly.Core;

namespace Monopoly.Net
{
    // Лобби на хосте: места, имена, цвета, готовность. Без сети — её делает GameHost.
    // Методы возвращают текст ошибки для игрока или null, если всё получилось.
    public sealed class Lobby
    {
        // Сколько цветов фишек в палитре интерфейса — по одному на место.
        public const int ColorCount = GameRules.MaxPlayers;
        public const int MaxNameLength = 20;

        private readonly List<Seat> seats = new();
        private readonly string hostToken;
        private readonly string version;
        private int nextSeatId;

        public bool IsStarted { get; private set; }

        public Lobby(string hostToken, string version)
        {
            this.hostToken = hostToken;
            this.version = version;
        }

        public int PlayerCount => seats.Count;

        public string? HostName => seats.FirstOrDefault(s => s.IsHost)?.Name;

        public JoinResult Join(string connectionId, JoinRequest request)
        {
            if (request.Version != version)
                return Fail($"Версии игры не совпадают: у хоста {version}, у вас {request.Version}. Нужна одна версия у всех.");
            if (IsStarted)
                return Fail("Игра уже началась.");
            if (seats.Count >= GameRules.MaxPlayers)
                return Fail($"Все {GameRules.MaxPlayers} мест заняты.");

            string name = (request.Name ?? "").Trim();
            if (name.Length == 0)
                return Fail("Введите имя.");
            if (name.Length > MaxNameLength)
                return Fail($"Имя слишком длинное: не больше {MaxNameLength} символов.");
            if (seats.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                return Fail($"Имя «{name}» уже занято.");

            bool isHost = request.HostToken == hostToken;
            if (isHost && seats.Any(s => s.IsHost))
                return Fail("Хост уже в лобби.");

            var seat = new Seat(nextSeatId++, connectionId, name, FirstFreeColor(), isHost)
            {
                // Хосту готовность не нужна: он сам нажимает «Начать».
                IsReady = isHost,
            };
            seats.Add(seat);
            return new JoinResult(null, seat.SeatId);
        }

        // Выход до старта освобождает место. После старта место остаётся (переподключение — этап 5).
        public void Leave(string connectionId)
        {
            var seat = Find(connectionId);
            if (seat is null)
                return;
            if (IsStarted)
                seat.ConnectionId = null;
            else
                seats.Remove(seat);
        }

        public string? SetReady(string connectionId, bool ready)
        {
            var seat = Find(connectionId);
            if (seat is null)
                return "Вы не в лобби.";
            if (IsStarted)
                return "Игра уже началась.";
            if (!seat.IsHost)
                seat.IsReady = ready;
            return null;
        }

        public string? SetColor(string connectionId, int colorIndex)
        {
            var seat = Find(connectionId);
            if (seat is null)
                return "Вы не в лобби.";
            if (IsStarted)
                return "Игра уже началась.";
            if (colorIndex < 0 || colorIndex >= ColorCount)
                return "Такого цвета нет.";
            if (seats.Any(s => s != seat && s.ColorIndex == colorIndex))
                return "Этот цвет уже занят.";
            seat.ColorIndex = colorIndex;
            return null;
        }

        // Старт партии. При успехе возвращает имена в порядке мест; номер в списке — Id игрока в движке.
        public string? Start(string connectionId, out IReadOnlyList<string> playerNames)
        {
            playerNames = Array.Empty<string>();
            var seat = Find(connectionId);
            if (seat is null || !seat.IsHost)
                return "Начать игру может только хост.";
            var reason = StartBlockedReason();
            if (reason is not null)
                return reason;

            IsStarted = true;
            for (int i = 0; i < seats.Count; i++)
                seats[i].PlayerId = i;
            playerNames = seats.Select(s => s.Name).ToList();
            return null;
        }

        public LobbyState GetState() => new(
            seats.Select(s => new LobbySeat(s.SeatId, s.Name, s.ColorIndex, s.IsReady, s.IsHost)).ToList(),
            GameRules.MaxPlayers,
            StartBlockedReason());

        // Подключённые игроки: адресаты рассылки.
        public IEnumerable<(string ConnectionId, int? PlayerId)> Connections() =>
            seats.Where(s => s.ConnectionId is not null).Select(s => (s.ConnectionId!, s.PlayerId));

        public int? FindPlayerId(string connectionId) => Find(connectionId)?.PlayerId;

        public string? FindName(string connectionId) => Find(connectionId)?.Name;

        public IReadOnlyDictionary<int, int> ColorByPlayerId() =>
            seats.Where(s => s.PlayerId is not null).ToDictionary(s => s.PlayerId!.Value, s => s.ColorIndex);

        private string? StartBlockedReason()
        {
            if (IsStarted)
                return "Игра уже идёт.";
            if (seats.Count < GameRules.MinPlayers)
                return $"Нужно хотя бы {GameRules.MinPlayers} игрока.";
            var notReady = seats.Where(s => !s.IsReady).Select(s => s.Name).ToList();
            if (notReady.Count > 0)
                return $"Не готовы: {string.Join(", ", notReady)}.";
            return null;
        }

        private int FirstFreeColor() =>
            Enumerable.Range(0, ColorCount).First(c => seats.All(s => s.ColorIndex != c));

        private Seat? Find(string connectionId) => seats.FirstOrDefault(s => s.ConnectionId == connectionId);

        private static JoinResult Fail(string error) => new(error, -1);

        private sealed class Seat
        {
            public int SeatId { get; }
            public string? ConnectionId { get; set; }
            public string Name { get; }
            public int ColorIndex { get; set; }
            public bool IsHost { get; }
            public bool IsReady { get; set; }
            public int? PlayerId { get; set; }

            public Seat(int seatId, string connectionId, string name, int colorIndex, bool isHost)
            {
                SeatId = seatId;
                ConnectionId = connectionId;
                Name = name;
                ColorIndex = colorIndex;
                IsHost = isHost;
            }
        }
    }
}
