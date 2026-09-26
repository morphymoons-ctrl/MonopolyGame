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
                ConnectionTitle.Text = "Підключено";
                ConnectionHint.Text = $"Хост: {client.Address}. Гра почнеться, коли хост натисне «Почати».";
            }
        }

        public void Show(LobbyState lobby)
        {
            state = lobby;
            SeatsTitle.Text = $"Гравці: {lobby.Seats.Count} з {lobby.MaxPlayers}";

            SeatsPanel.Children.Clear();
            foreach (var seat in lobby.Seats)
            {
                string role = seat.IsHost ? "хост" : seat.IsBot ? "бот" : seat.IsReady ? "готовий" : "не готовий";
                string me = seat.SeatId == client.SeatId ? " (ви)" : "";
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                if (isHost && seat.IsBot)
                {
                    int seatId = seat.SeatId;
                    var remove = new Button { Content = "✕", FontSize = 12, MinHeight = 24, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 10, 0), ToolTip = "Прибрати бота" };
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
                    ToolTip = PlayerPalette.Names[color] + (taken ? " — зайнятий" : ""),
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
                ? lobby.StartBlockedReason ?? "Усі готові — можна починати."
                : "";
        }

        private void ShowHostAddresses()
        {
            ConnectionTitle.Text = "Адреси для друзів";
            var addresses = LocalAddresses.Get();
            AddressesPanel.Children.Clear();
            foreach (var address in addresses)
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                var copy = new Button { Content = "Копіювати", Padding = new Thickness(10, 2, 10, 2), MinHeight = 30, FontSize = 14 };
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
                ? "Не знайдено жодної мережі. Перевірте підключення до інтернету або VPN."
                : "";
        }

        private void CopyAddress(string address)
        {
            try
            {
                Clipboard.SetText(address);
                StatusText.Text = "";
                StartHint.Text = $"Адресу {address} скопійовано.";
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Буфер обмена занят другой программой.
                StatusText.Text = "Не вдалося скопіювати — спробуйте ще раз.";
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
                    FirewallText.Text = "Брандмауер Windows блокує гру — друзі не зможуть підключитися. " +
                        "Так буває, якщо у вікні брандмауера натиснули «Скасувати». " +
                        "Натисніть кнопку й підтвердьте запит Windows (потрібні права адміністратора).";
                    break;
                default:
                    FirewallPanel.Visibility = Visibility.Visible;
                    FirewallText.Text = $"Якщо друзі не можуть підключитися, відкрийте порт {NetDefaults.Port} у брандмауері Windows: " +
                        "натисніть кнопку й підтвердьте запит Windows (потрібні права адміністратора).";
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
                FirewallText.Text = "Windows не дозволила змінити правила. Вручну: Брандмауер Захисника Windows → Додаткові параметри → " +
                    $"Правила для вхідних підключень: видаліть заборонні правила «Monopoly» і відкрийте порт {NetDefaults.Port} (TCP і UDP).";
                return;
            }

            await CheckFirewallAsync();
            if (firewall?.Status == FirewallStatus.Allowed)
            {
                FirewallPanel.Visibility = Visibility.Visible;
                FirewallText.Foreground = (Brush)FindResource("SuccessBrush");
                FirewallText.Text = "Готово: брандмауер пропускає гру.";
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
