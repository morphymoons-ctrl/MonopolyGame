using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Monopoly.Net;

namespace Monopoly.App
{
    // Лобби: игроки, цвета, готовность. Хост видит свои адреса и запускает игру.
    public partial class LobbyScreen : UserControl
    {
        private readonly GameClient client;
        private readonly bool isHost;
        private readonly Func<Task> leave;
        private LobbyState? state;
        // Пока обновляем галочку из состояния хоста, не отправляем её обратно.
        private bool updatingReady;
        private FirewallCheck? firewall;

        public LobbyScreen(GameClient client, bool isHost, Func<Task> leave)
        {
            InitializeComponent();
            this.client = client;
            this.isHost = isHost;
            this.leave = leave;

            ReadyBox.Visibility = isHost ? Visibility.Collapsed : Visibility.Visible;
            StartButton.Visibility = isHost ? Visibility.Visible : Visibility.Collapsed;
            AddBotButton.Visibility = isHost ? Visibility.Visible : Visibility.Collapsed;
            if (isHost)
            {
                ShowHostAddresses();
                Loaded += async (_, _) => await CheckFirewallAsync();
            }
            else
            {
                ConnectionTitle.Text = "Подключено";
                ConnectionHint.Text = $"Хост: {client.Address}. Игра начнётся, когда хост нажмёт «Начать».";
            }
        }

        public void Show(LobbyState lobby)
        {
            state = lobby;
            SeatsTitle.Text = $"Игроки: {lobby.Seats.Count} из {lobby.MaxPlayers}";

            SeatsPanel.Children.Clear();
            foreach (var seat in lobby.Seats)
            {
                string role = seat.IsHost ? "хост" : seat.IsBot ? "бот" : seat.IsReady ? "готов" : "не готов";
                string me = seat.SeatId == client.SeatId ? " (вы)" : "";
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                if (isHost && seat.IsBot)
                {
                    int seatId = seat.SeatId;
                    var remove = new Button { Content = "✕", FontSize = 12, MinHeight = 24, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 10, 0), ToolTip = "Убрать бота" };
                    remove.Click += async (_, _) => StatusText.Text = await client.RemoveBotAsync(seatId) ?? "";
                    row.Children.Add(remove);
                }
                row.Children.Add(new Ellipse
                {
                    Width = 20,
                    Height = 20,
                    Fill = PlayerPalette.Get(seat.ColorIndex),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                row.Children.Add(new TextBlock { Text = $"{seat.Name}{me}", FontWeight = FontWeights.SemiBold });
                row.Children.Add(new TextBlock { Text = $" — {role}", Foreground = (Brush)FindResource("MutedTextBrush") });
                SeatsPanel.Children.Add(row);
            }

            var mine = lobby.Seats.FirstOrDefault(s => s.SeatId == client.SeatId);
            ColorsPanel.Children.Clear();
            for (int color = 0; color < Lobby.ColorCount; color++)
            {
                bool taken = lobby.Seats.Any(s => s.ColorIndex == color && s != mine);
                // Цвет — внутри кнопки: фон выключенной кнопки WPF не показывает.
                var button = new Button
                {
                    Width = 52,
                    Height = 52,
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(0),
                    Content = new Ellipse { Width = 32, Height = 32, Fill = PlayerPalette.Get(color), Opacity = taken ? 0.25 : 1 },
                    BorderBrush = mine?.ColorIndex == color ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("SurfaceBorderBrush"),
                    BorderThickness = new Thickness(mine?.ColorIndex == color ? 3 : 1),
                    IsEnabled = !taken,
                    ToolTip = PlayerPalette.Names[color] + (taken ? " — занят" : ""),
                    Tag = color
                };
                button.Click += Color_Click;
                ColorsPanel.Children.Add(button);
            }

            updatingReady = true;
            ReadyBox.IsChecked = mine?.IsReady == true;
            updatingReady = false;

            StartButton.IsEnabled = lobby.StartBlockedReason is null;
            StartHint.Text = isHost
                ? lobby.StartBlockedReason ?? "Все готовы — можно начинать."
                : "";
        }

        private void ShowHostAddresses()
        {
            ConnectionTitle.Text = "Адреса для друзей";
            var addresses = LocalAddresses.Get();
            AddressesPanel.Children.Clear();
            foreach (var address in addresses)
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                var copy = new Button { Content = "Скопировать", Padding = new Thickness(10, 2, 10, 2), MinHeight = 30, FontSize = 14 };
                copy.Click += (_, _) => CopyAddress(address.Address);
                DockPanel.SetDock(copy, Dock.Right);
                row.Children.Add(copy);
                row.Children.Add(new TextBlock
                {
                    Text = $"{address.Address}  ({address.Network})",
                    VerticalAlignment = VerticalAlignment.Center
                });
                AddressesPanel.Children.Add(row);
            }
            ConnectionHint.Text = addresses.Count == 0
                ? "Не найдено ни одной сети. Проверьте подключение к интернету или VPN."
                : $"Друзья вводят один из адресов — тот, что из той же сети, что у них (Radmin, ZeroTier, Tailscale или локальная). Порт {NetDefaults.Port}.";
        }

        private void CopyAddress(string address)
        {
            try
            {
                Clipboard.SetText(address);
                StatusText.Text = "";
                StartHint.Text = $"Адрес {address} скопирован.";
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Буфер обмена занят другой программой.
                StatusText.Text = "Не удалось скопировать — попробуйте ещё раз.";
            }
        }

        private async Task CheckFirewallAsync()
        {
            firewall = await Task.Run(Firewall.Check);
            switch (firewall.Status)
            {
                case FirewallStatus.Allowed:
                    FirewallPanel.Visibility = Visibility.Collapsed;
                    break;
                case FirewallStatus.Blocked:
                    FirewallPanel.Visibility = Visibility.Visible;
                    FirewallText.Foreground = PlayerPalette.Make("#FF8A8D");
                    FirewallText.Text = "Брандмауэр Windows блокирует игру — друзья не смогут подключиться. " +
                        "Так бывает, если в окне брандмауэра нажали «Отмена». " +
                        "Нажмите кнопку и подтвердите запрос Windows (нужны права администратора).";
                    break;
                default:
                    FirewallPanel.Visibility = Visibility.Visible;
                    FirewallText.Text = $"Если друзья не могут подключиться, откройте порт {NetDefaults.Port} в брандмауэре Windows: " +
                        "нажмите кнопку и подтвердите запрос Windows (нужны права администратора).";
                    break;
            }
        }

        private async void Firewall_Click(object sender, RoutedEventArgs e)
        {
            FirewallButton.IsEnabled = false;
            var blockRules = firewall?.BlockRuleNames ?? Array.Empty<string>();
            bool confirmed = await Task.Run(() => Firewall.TryAllow(blockRules));
            FirewallButton.IsEnabled = true;
            if (!confirmed)
            {
                FirewallText.Text = "Windows не дала изменить правила. Вручную: Брандмауэр Защитника Windows → Дополнительные параметры → " +
                    $"Правила для входящих подключений: удалите запрещающие правила «Monopoly» и откройте порт {NetDefaults.Port} (TCP и UDP).";
                return;
            }

            await CheckFirewallAsync();
            if (firewall?.Status == FirewallStatus.Allowed)
            {
                FirewallPanel.Visibility = Visibility.Visible;
                FirewallText.Foreground = (Brush)FindResource("SuccessBrush");
                FirewallText.Text = "Готово: брандмауэр пропускает игру.";
                FirewallButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void Color_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int color })
            {
                StatusText.Text = await client.SetColorAsync(color) ?? "";
            }
        }

        private async void Ready_Changed(object sender, RoutedEventArgs e)
        {
            if (updatingReady)
            {
                return;
            }
            StatusText.Text = await client.SetReadyAsync(ReadyBox.IsChecked == true) ?? "";
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            var error = await client.StartGameAsync();
            if (error is not null)
            {
                StatusText.Text = error;
                StartButton.IsEnabled = state?.StartBlockedReason is null;
            }
        }

        private async void AddBot_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = await client.AddBotAsync() ?? "";
        }

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            await leave();
        }
    }
}
