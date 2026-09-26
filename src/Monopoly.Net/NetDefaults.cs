using System.Reflection;

namespace Monopoly.Net
{
    public static class NetDefaults
    {
        // TCP — игра (SignalR), UDP — поиск игр в сети. Номер один и тот же.
        public const int Port = 7777;
        public const string HubPath = "/game";

        // Версия игры из Directory.Build.props. Клиенты разных версий вместе не играют.
        public static string GameVersion { get; } =
            typeof(NetDefaults).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
