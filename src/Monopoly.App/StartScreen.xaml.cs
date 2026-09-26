using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Monopoly.Net;

namespace Monopoly.App
{
    // Первый экран: имя, «Создать игру», «Подключиться» и найденные в сети игры.
    public partial class StartScreen : UserControl
    {
        // Возвращают текст ошибки или null, если всё получилось.
        private readonly Func<string, Task<string?>> create;
        private readonly Func<string, string, Task<string?>> join;
        private readonly UserSettings settings;
        private bool busy;

        public StartScreen(UserSettings settings, Func<string, Task<string?>> create, Func<string, string, Task<string?>> join, string? message)
        {
            InitializeComponent();
            this.settings = settings;
            this.create = create;
            this.join = join;

            NameBox.Text = settings.Name.Length > 0 ? settings.Name : DefaultName();
            AddressBox.Text = settings.LastAddress;
            StatusText.Text = message ?? "";
            VersionText.Text = $"Версия {NetDefaults.GameVersion}";
            Loaded += async (_, _) => await SearchAsync();
        }

        private static string DefaultName()
        {
            string name = Environment.UserName;
            return name.Length > Lobby.MaxNameLength ? name[..Lobby.MaxNameLength] : name;
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync("Создаём игру…", name => create(name));
        }

        private async void Join_Click(object sender, RoutedEventArgs e)
        {
            await JoinAsync();
        }

        private async void AddressBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await JoinAsync();
            }
        }

        private async void GamesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GamesList.SelectedItem is ListBoxItem)
            {
                await JoinAsync();
            }
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GamesList.SelectedItem is ListBoxItem { Tag: DiscoveredGame game })
            {
                AddressBox.Text = game.Port == NetDefaults.Port ? game.Address : $"{game.Address}:{game.Port}";
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await SearchAsync();
        }

        private async Task JoinAsync()
        {
            string address = AddressBox.Text.Trim();
            if (address.Length == 0)
            {
                StatusText.Text = "Введите IP хоста или выберите игру в списке.";
                return;
            }
            await RunAsync($"Подключаемся к {address}…", name => join(name, address));
            if (StatusText.Text.Length == 0)
            {
                settings.LastAddress = address;
                settings.Save();
            }
        }

        private async Task RunAsync(string progress, Func<string, Task<string?>> action)
        {
            if (busy)
            {
                return;
            }
            string name = NameBox.Text.Trim();
            if (name.Length == 0)
            {
                StatusText.Text = "Введите имя.";
                return;
            }

            settings.Name = name;
            settings.Save();
            SetBusy(true);
            StatusText.Text = progress;
            var error = await action(name);
            StatusText.Text = error ?? "";
            SetBusy(false);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            CreateButton.IsEnabled = !value;
            JoinButton.IsEnabled = !value;
        }

        private async Task SearchAsync()
        {
            SearchButton.IsEnabled = false;
            SearchStatus.Text = "Ищем игры…";
            GamesList.Items.Clear();

            var games = await GameFinder.FindAsync(TimeSpan.FromSeconds(1.5));
            foreach (var game in games)
            {
                string text = $"{game.HostName} — {game.Address} · игроков {game.Players}/{game.MaxPlayers}";
                if (game.InProgress)
                {
                    text += " · идёт игра";
                }
                if (game.Version != NetDefaults.GameVersion)
                {
                    text += $" · другая версия ({game.Version})";
                }
                GamesList.Items.Add(new ListBoxItem { Content = text, Tag = game });
            }

            SearchStatus.Text = games.Count == 0
                ? "Игр не найдено. Через Tailscale поиск не работает — введите IP хоста вручную."
                : "";
            SearchButton.IsEnabled = true;
        }
    }
}
