using System.Reflection;

namespace Monopoly.Net
{
    public static class NetDefaults
    {
        // TCP — игра (SignalR), UDP — поиск игр в сети. Номер один и тот же.
        public const int Port = 7777;
        public const string HubPath = "/game";

        // Время на решение и сколько ждать отключившегося до передачи места боту (RULES.md, §13).
        public static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(80);
        public static readonly TimeSpan BotTakeover = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan BotDelay = TimeSpan.FromSeconds(1);

        // Версия игры из Directory.Build.props. Клиенты разных версий вместе не играют.
        public static string GameVersion { get; } =
            typeof(NetDefaults).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
