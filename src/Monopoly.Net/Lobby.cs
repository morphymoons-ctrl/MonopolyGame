using Monopoly.Core;

namespace Monopoly.Net
{
    // Места за столом на хосте: имена, цвета, готовность, подключение, боты. Без сети — её делает GameHost.
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
        // Тематика доски (RULES.md, §15): выбирает хост до старта.
        public BoardTheme Theme { get; private set; } = BoardTheme.Business;

        public Lobby(string hostToken, string version)
        {
            this.hostToken = hostToken;
            this.version = version;
        }

        public int PlayerCount => seats.Count;

        public string? HostName => seats.FirstOrDefault(s => s.IsHost)?.Name;

        public JoinResult Join(string connectionId, JoinRequest request, DateTime now)
        {
            if (request.Version != version)
                return Fail($"Версії гри не збігаються: у хоста {version}, у вас {request.Version}. Потрібна однакова версія в усіх — у кого версія старіша, перезапустіть гру: вона оновиться сама.");

            string name = (request.Name ?? "").Trim();
            if (name.Length == 0)
                return Fail("Введіть ім'я.");
            if (IsStarted)
                return Rejoin(connectionId, name);

            if (seats.Count >= GameRules.MaxPlayers)
                return Fail($"Усі {GameRules.MaxPlayers} місць зайнято.");
            if (name.Length > MaxNameLength)
                return Fail($"Ім'я задовге: не більше {MaxNameLength} символів.");
            if (FindByName(name) is not null)
                return Fail($"Ім'я «{name}» уже зайняте.");

            bool isHost = request.HostToken == hostToken;
            if (isHost && seats.Any(s => s.IsHost))
                return Fail("Хост уже в лобі.");

            var seat = new Seat(nextSeatId++, name, FirstFreeColor(), isHost, isBot: false)
            {
                ConnectionId = connectionId,
                // Хосту готовность не нужна: он сам нажимает «Начать».
                IsReady = isHost,
            };
            seats.Add(seat);
            return new JoinResult(null, seat.SeatId);
        }

        // Возвращение в идущую партию: под именем игрока, который сейчас не подключён.
        private JoinResult Rejoin(string connectionId, string name)
        {
            var seat = FindByName(name);
            if (seat is null || seat.IsBot)
                return Fail("Гра вже почалася. Повернутися можна лише під своїм ім'ям із цієї партії.");
            if (seat.ConnectionId is not null)
                return Fail($"Гравець «{seat.Name}» уже в грі.");

            seat.ConnectionId = connectionId;
            seat.DisconnectedAt = null;
            seat.BotActive = false;
            return new JoinResult(null, seat.SeatId);
        }

        // До старта выход освобождает место; после старта место ждёт возвращения.
        public void Leave(string connectionId, DateTime now)
        {
            var seat = Find(connectionId);
            if (seat is null)
                return;
            if (IsStarted)
            {
                seat.ConnectionId = null;
                seat.DisconnectedAt = now;
            }
            else
            {
                seats.Remove(seat);
            }
        }

        public string? AddBot(string connectionId)
        {
            var error = RequireHostBeforeStart(connectionId);
            if (error is not null)
                return error;
            if (seats.Count >= GameRules.MaxPlayers)
                return $"Усі {GameRules.MaxPlayers} місць зайнято.";

            int number = 1;
            while (FindByName($"Бот {number}") is not null)
                number++;
            seats.Add(new Seat(nextSeatId++, $"Бот {number}", FirstFreeColor(), isHost: false, isBot: true) { IsReady = true });
            return null;
        }

        public string? RemoveBot(string connectionId, int seatId)
        {
            var error = RequireHostBeforeStart(connectionId);
            if (error is not null)
                return error;
            var seat = seats.FirstOrDefault(s => s.SeatId == seatId);
            if (seat is null || !seat.IsBot)
                return "Такого бота немає.";
            seats.Remove(seat);
            return null;
        }

        public string? SetTheme(string connectionId, BoardTheme theme)
        {
            var error = RequireHostBeforeStart(connectionId);
            if (error is not null)
                return error;
            if (!Enum.IsDefined(theme))
                return "Такої дошки немає.";
            Theme = theme;
            return null;
        }

        public string? SetReady(string connectionId, bool ready)
        {
            var seat = Find(connectionId);
            if (seat is null)
                return "Ви не в лобі.";
            if (IsStarted)
                return "Гра вже почалася.";
            if (!seat.IsHost)
                seat.IsReady = ready;
            return null;
        }

        public string? SetColor(string connectionId, int colorIndex)
        {
            var seat = Find(connectionId);
            if (seat is null)
                return "Ви не в лобі.";
            if (IsStarted)
                return "Гра вже почалася.";
            if (colorIndex < 0 || colorIndex >= ColorCount)
                return "Такого кольору немає.";
            if (seats.Any(s => s != seat && s.ColorIndex == colorIndex))
                return "Цей колір уже зайнятий.";
            seat.ColorIndex = colorIndex;
            return null;
        }

        // Старт партии. При успехе возвращает имена в порядке мест; номер в списке — Id игрока в движке.
        public string? Start(string connectionId, out IReadOnlyList<string> playerNames)
        {
            playerNames = Array.Empty<string>();
            var seat = Find(connectionId);
            if (seat is null || !seat.IsHost)
                return "Почати гру може лише хост.";
            var reason = StartBlockedReason();
            if (reason is not null)
                return reason;

            IsStarted = true;
            for (int i = 0; i < seats.Count; i++)
                seats[i].PlayerId = i;
            playerNames = seats.Select(s => s.Name).ToList();
            return null;
        }

        // Партия из сохранения: места и доска те же, все люди пока не подключены.
        public void Restore(IReadOnlyList<SavedSeat> saved, DateTime now, BoardTheme theme = BoardTheme.Business)
        {
            Theme = theme;
            seats.Clear();
            for (int i = 0; i < saved.Count; i++)
            {
                seats.Add(new Seat(nextSeatId++, saved[i].Name, saved[i].ColorIndex, saved[i].IsHost, saved[i].IsBot)
                {
                    IsReady = true,
                    PlayerId = i,
                    DisconnectedAt = saved[i].IsBot ? null : now,
                });
            }
            IsStarted = true;
        }

        // Отключившиеся дольше takeover передают место боту. Возвращает имена тех, за кого бот начал играть.
        public IReadOnlyList<string> Tick(DateTime now, TimeSpan takeover)
        {
            var switched = new List<string>();
            foreach (var seat in seats)
            {
                if (IsStarted && !seat.IsBot && !seat.BotActive && seat.ConnectionId is null
                    && seat.DisconnectedAt is DateTime since && now - since >= takeover)
                {
                    seat.BotActive = true;
                    switched.Add(seat.Name);
                }
            }
            return switched;
        }

        public bool IsBotControlled(int playerId) =>
            seats.FirstOrDefault(s => s.PlayerId == playerId) is { } seat && (seat.IsBot || seat.BotActive);

        public LobbyState GetState() => new(
            seats.Select(s => new LobbySeat(s.SeatId, s.Name, s.ColorIndex, s.IsReady, s.IsHost, s.IsBot)).ToList(),
            GameRules.MaxPlayers,
            StartBlockedReason(),
            Theme);

        public IReadOnlyList<SeatStatus> SeatStatuses(DateTime now, TimeSpan takeover) =>
            seats.Where(s => s.PlayerId is not null).Select(s =>
            {
                if (s.IsBot || s.BotActive)
                    return new SeatStatus(s.PlayerId!.Value, SeatConnection.Bot, null);
                if (s.ConnectionId is not null)
                    return new SeatStatus(s.PlayerId!.Value, SeatConnection.Online, null);
                var left = s.DisconnectedAt is DateTime since ? takeover - (now - since) : TimeSpan.Zero;
                return new SeatStatus(s.PlayerId!.Value, SeatConnection.Offline, Math.Max(0, (int)Math.Ceiling(left.TotalSeconds)));
            }).ToList();

        // Места для сохранения — в порядке Id игроков.
        public IReadOnlyList<SavedSeat> SavedSeats() =>
            seats.Where(s => s.PlayerId is not null)
                .OrderBy(s => s.PlayerId)
                .Select(s => new SavedSeat(s.Name, s.ColorIndex, s.IsHost, s.IsBot))
                .ToList();

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
                return "Гра вже триває.";
            if (seats.Count < GameRules.MinPlayers)
                return $"Потрібно хоча б {GameRules.MinPlayers} гравці.";
            var notReady = seats.Where(s => !s.IsReady).Select(s => s.Name).ToList();
            if (notReady.Count > 0)
                return $"Не готові: {string.Join(", ", notReady)}.";
            return null;
        }

        private string? RequireHostBeforeStart(string connectionId)
        {
            if (Find(connectionId) is not { IsHost: true })
                return "Це може лише хост.";
            return IsStarted ? "Гра вже почалася." : null;
        }

        private int FirstFreeColor() =>
            Enumerable.Range(0, ColorCount).First(c => seats.All(s => s.ColorIndex != c));

        private Seat? Find(string connectionId) => seats.FirstOrDefault(s => s.ConnectionId == connectionId);

        private Seat? FindByName(string name) =>
            seats.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        private static JoinResult Fail(string error) => new(error, -1);

        private sealed class Seat
        {
            public int SeatId { get; }
            public string Name { get; }
            public bool IsHost { get; }
            // Бот, добавленный хостом в лобби.
            public bool IsBot { get; }
            public int ColorIndex { get; set; }
            public bool IsReady { get; set; }
            public int? PlayerId { get; set; }
            // null — не подключён (бот или отключившийся игрок).
            public string? ConnectionId { get; set; }
            public DateTime? DisconnectedAt { get; set; }
            // За отключившегося играет бот.
            public bool BotActive { get; set; }

            public Seat(int seatId, string name, int colorIndex, bool isHost, bool isBot)
            {
                SeatId = seatId;
                Name = name;
                ColorIndex = colorIndex;
                IsHost = isHost;
                IsBot = isBot;
            }
        }
    }
}
