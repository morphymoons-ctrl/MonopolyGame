using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Monopoly.Net
{
    // Адрес этого компьютера и сеть, к которой он относится: его хост показывает друзьям.
    public sealed record LocalAddress(string Address, string Network);

    public static class LocalAddresses
    {
        public const string Radmin = "Radmin VPN";
        public const string ZeroTier = "ZeroTier";
        public const string Tailscale = "Tailscale";
        public const string Lan = "Локальна мережа";
        public const string Other = "Інша мережа";

        // Сначала VPN (через них обычно играют с друзьями), потом локальная сеть.
        public static IReadOnlyList<LocalAddress> Get()
        {
            var order = new[] { Radmin, ZeroTier, Tailscale, Lan, Other };
            return ActiveIPv4Interfaces()
                .Select(x => new LocalAddress(x.Address.Address.ToString(), Classify(x.Address.Address, x.Interface.Name, x.Interface.Description)))
                .DistinctBy(a => a.Address)
                .OrderBy(a => Array.IndexOf(order, a.Network))
                .ToList();
        }

        // Радмин раздаёт адреса 26.*, Tailscale — 100.64.0.0/10. ZeroTier выбирает диапазон сам — узнаём по имени адаптера.
        public static string Classify(IPAddress address, string interfaceName, string interfaceDescription)
        {
            string adapter = interfaceName + " " + interfaceDescription;
            var b = address.GetAddressBytes();

            if (adapter.Contains("Radmin", StringComparison.OrdinalIgnoreCase) || b[0] == 26)
                return Radmin;
            if (adapter.Contains("ZeroTier", StringComparison.OrdinalIgnoreCase))
                return ZeroTier;
            if (adapter.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) || (b[0] == 100 && (b[1] & 0xC0) == 64))
                return Tailscale;
            if (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168))
                return Lan;
            return Other;
        }

        internal static IEnumerable<UnicastIPAddressInformation> ActiveIPv4() =>
            ActiveIPv4Interfaces().Select(x => x.Address);

        private static IEnumerable<(NetworkInterface Interface, UnicastIPAddressInformation Address)> ActiveIPv4Interfaces()
        {
            NetworkInterface[] interfaces;
            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (NetworkInformationException)
            {
                yield break;
            }

            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
                {
                    var address = unicast.Address;
                    // 169.254.* — адрес без сети (DHCP не ответил), с него никто не подключится.
                    if (address.AddressFamily != AddressFamily.InterNetwork || address.GetAddressBytes() is [169, 254, ..])
                        continue;
                    yield return (ni, unicast);
                }
            }
        }
    }
}
