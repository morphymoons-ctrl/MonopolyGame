using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Monopoly.App;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Admin
{
    // Панель администратора (RULES.md, §14): партии в сети слева, выбранная партия справа.
    // Правил здесь нет: панель отправляет действия администратора, хост проверяет их движком.
    public partial class MainWindow : Window
    {
        // Доска партии (RULES.md, §15): названия клеток и слова («філія» / «підрозділ») — с неё.
        private BoardTheme theme = BoardTheme.Business;
        private IReadOnlyList<BoardCell> Cells { get; set; } = Board.CreateDefault();
        private GameTerms Terms => GameTerms.For(theme);
        private ThemeWords Words => ThemeWords.For(theme);

        private readonly ECDsa? key;
        private readonly DispatcherTimer searchTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        private bool searching;

        private AdminClient? client;
        private AdminView? view;
        // Для какой партии и какого состава построены строки игроков и компаний.
        private string builtFor = "";
        private readonly List<PlayerRow> playerRows = new();
        private readonly List<CompanyRow> companyRows = new();

        public MainWindow()
        {
            InitializeComponent();
            key = AdminKeyStore.Load();
            if (key is null)
            {
                ShowStatus($"На цьому ПК немає ключа адміністратора ({AdminKeyStore.FilePath}). Панель працює лише на ПК автора гри.", false);
                ConnectButton.IsEnabled = false;
            }
            else if (AdminAuth.ExportPublicKey(key) != AdminAuth.OwnerPublicKey)
            {
                ShowStatus("Цей ключ не збігається з ключем, вбудованим у гру: хости його не приймуть.", false);
            }

            searchTimer.Tick += async (_, _) => await SearchAsync();
            searchTimer.Start();
            Loaded += async (_, _) => await SearchAsync();
            Closed += async (_, _) =>
            {
                searchTimer.Stop();
                if (client is not null)
                    await client.DisposeAsync();
            };
        }

        // --- Поиск партий ---

        private async Task SearchAsync()
        {
            if (searching)
                return;
            searching = true;
            try
            {
                var games = await GameFinder.FindAsync(TimeSpan.FromSeconds(1.2));
                GamesPanel.Children.Clear();
                foreach (var game in games.OrderBy(g => g.HostName))
                    GamesPanel.Children.Add(GameItem(game));
                SearchText.Text = games.Count == 0
                    ? $"Партій не знайдено · {DateTime.Now:HH:mm:ss}"
                    : $"Знайдено: {games.Count} · {DateTime.Now:HH:mm:ss}";
            }
            finally
            {
                searching = false;
            }
        }

        private UIElement GameItem(DiscoveredGame game)
        {
            bool sameVersion = game.Version == NetDefaults.GameVersion;
            bool current = view?.GameId == game.GameId;
            // Шаблон кнопки ставит содержимое по центру — ширина на всю кнопку прижимает текст влево.
            var text = new StackPanel { Width = 232 };
            text.Children.Add(new TextBlock { Text = game.HostName, FontSize = 17, FontWeight = FontWeights.Bold });
            string state = game.InProgress ? "іде гра" : "лобі";
            text.Children.Add(new TextBlock
            {
                Text = sameVersion
                    ? $"{state} · гравців {game.Players}/{game.MaxPlayers} · {game.Address}"
                    : $"інша версія гри: {game.Version} · {game.Address}",
                FontSize = 13,
                Foreground = (Brush)FindResource("MutedTextBrush"),
            });
            text.Children.Add(new TextBlock
            {
                Text = ThemeWords.ThemeName(game.Theme),
                FontSize = 13,
                Foreground = (Brush)FindResource("MutedTextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            var button = new Button
            {
                Content = text,
                Style = (Style)FindResource("SecondaryButton"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                IsEnabled = sameVersion && key is not null,
            };
            if (current)
                button.BorderBrush = (Brush)FindResource("AccentBrush");
            AutomationProperties.SetName(button, game.HostName);
            AutomationProperties.SetAutomationId(button, $"Game{game.GameId:N}");
            button.Click += async (_, _) => await ConnectAsync($"{game.Address}:{game.Port}");
            return button;
        }

        private async void OnConnectByAddress(object sender, RoutedEventArgs e) => await ConnectAsync(AddressBox.Text);

        private async void OnAddressKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ConnectButton.IsEnabled)
                await ConnectAsync(AddressBox.Text);
        }

        // --- Подключение ---

        private async Task ConnectAsync(string address)
        {
            if (key is null || string.IsNullOrWhiteSpace(address))
                return;
            await DisconnectAsync();

            var newClient = new AdminClient(address, key);
            newClient.ViewChanged += v => Dispatcher.InvokeAsync(() =>
            {
                if (client == newClient)
                    Render(v);
            });
            newClient.ConnectionLost += text => Dispatcher.InvokeAsync(async () =>
            {
                if (client != newClient)
                    return;
                await DisconnectAsync();
                ShowStatus(text, false);
            });
            client = newClient;
            ShowStatus($"Підключення до {address}…", true);

            var error = await newClient.ConnectAsync();
            if (client != newClient)
                return;
            if (error is not null)
            {
                await DisconnectAsync();
                ShowStatus(error, false);
                return;
            }
            ShowStatus($"Підключено до {address}.", true);
            DisconnectButton.Visibility = Visibility.Visible;
        }

        private async void OnDisconnect(object sender, RoutedEventArgs e)
        {
            await DisconnectAsync();
            ShowStatus("Відключено.", true);
        }

        private async Task DisconnectAsync()
        {
            var old = client;
            client = null;
            view = null;
            builtFor = "";
            playerRows.Clear();
            companyRows.Clear();
            PlayersPanel.Children.Clear();
            CompaniesPanel.Children.Clear();
            PlayersTitle.Visibility = Visibility.Collapsed;
            CompaniesTitle.Visibility = Visibility.Collapsed;
            DisconnectButton.Visibility = Visibility.Collapsed;
            GameTitle.Text = "Панель адміністратора";
            GameSubtitle.Text = "Оберіть партію зліва. Зміни тихі: гравці бачать лише нові числа.";
            if (old is not null)
                await old.DisposeAsync();
        }

        // --- Показ партии ---

        private void Render(AdminView newView)
        {
            view = newView;
            var snapshot = newView.Snapshot;
            GameTitle.Text = $"Партія хоста {newView.HostName ?? "?"}";

            if (snapshot is null)
            {
                GameSubtitle.Text = $"Лобі · дошка «{ThemeWords.ThemeName(newView.Lobby.Theme)}»: гра ще не почалася. Змінювати можна після старту.";
                RenderLobby(newView.Lobby);
                return;
            }

            // Доска партии — до строк: названия клеток берутся с неё.
            if (theme != snapshot.Theme || builtFor == "")
            {
                theme = snapshot.Theme;
                Cells = Board.Create(theme);
            }

            var current = snapshot.FindPlayer(snapshot.CurrentPlayerId);
            string board = $"дошка «{ThemeWords.ThemeName(theme)}»";
            GameSubtitle.Text = snapshot.WinnerId is int winner
                ? $"Партію закінчено: переміг {snapshot.FindPlayer(winner)?.Name}. Змінювати вже нічого не можна."
                : $"Іде гра · {board} · ходить {current?.Name} · зміни тихі, таймер ходу від них не скидається.";

            string layout = $"{newView.GameId}|{theme}|{string.Join(",", snapshot.Players.Select(p => p.Id))}";
            if (builtFor != layout)
                Build(snapshot, layout);

            foreach (var row in playerRows)
                row.Update(snapshot.FindPlayer(row.Id)!, snapshot, newView);
            foreach (var row in companyRows)
                row.Update(snapshot.Cells[row.Index], snapshot);
        }

        private void RenderLobby(LobbyState lobby)
        {
            builtFor = "";
            playerRows.Clear();
            companyRows.Clear();
            PlayersPanel.Children.Clear();
            CompaniesPanel.Children.Clear();
            CompaniesTitle.Visibility = Visibility.Collapsed;
            PlayersTitle.Visibility = Visibility.Visible;
            foreach (var seat in lobby.Seats)
            {
                string role = seat.IsHost ? "хост" : seat.IsBot ? "бот" : seat.IsReady ? "готовий" : "не готовий";
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                line.Children.Add(Dot(PlayerPalette.Get(seat.ColorIndex)));
                line.Children.Add(new TextBlock { Text = seat.Name, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(10, 0, 10, 0) });
                line.Children.Add(new TextBlock { Text = role, FontSize = 14, Foreground = (Brush)FindResource("MutedTextBrush"), VerticalAlignment = VerticalAlignment.Center });
                PlayersPanel.Children.Add(line);
            }
        }

        private void Build(GameSnapshot snapshot, string layout)
        {
            builtFor = layout;
            playerRows.Clear();
            companyRows.Clear();
            PlayersPanel.Children.Clear();
            CompaniesPanel.Children.Clear();
            PlayersTitle.Visibility = Visibility.Visible;
            CompaniesTitle.Visibility = Visibility.Visible;

            foreach (var player in snapshot.Players)
            {
                var row = new PlayerRow(this, player.Id);
                playerRows.Add(row);
                PlayersPanel.Children.Add(row.Element);
            }

            // Владелец: банк или любой игрок (выбывшему движок откажет).
            var owners = new List<(string Name, int? Id)> { ("Банк", null) };
            owners.AddRange(snapshot.Players.Select(p => (p.Name, (int?)p.Id)));
            for (int i = 0; i < Cells.Count; i++)
            {
                if (!Cells[i].IsPurchasable)
                    continue;
                var row = new CompanyRow(this, i, owners);
                companyRows.Add(row);
                CompaniesPanel.Children.Add(row.Element);
            }
        }

        // --- Действия ---

        private async Task RunAsync(AdminAction action, string done)
        {
            if (client is null)
                return;
            var error = await client.ExecuteAsync(action);
            ShowStatus(error ?? done, error is null);
        }

        private void ShowStatus(string text, bool ok)
        {
            StatusText.Text = text;
            StatusText.Foreground = (Brush)FindResource(ok ? "SuccessBrush" : "DangerBrush");
        }

        private string PlayerName(int id) => view?.Snapshot?.FindPlayer(id)?.Name ?? "?";

        // Сумма из поля ввода: цифры, пробелы, «грн» и «$» допускаются. null — не число.
        private static int? ParseMoney(string text)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return digits.Length is > 0 and <= 10 && long.TryParse(digits, out long value) && value <= int.MaxValue ? (int)value : null;
        }

        // Число без знака валюты — для полей ввода.
        private static string Plain(int amount) => amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("uk-UA"));

        // Суммы — в валюте доски партии (§15).
        private string Money(int amount) => GameRules.Money(amount, theme);

        private static Ellipse Dot(Brush color) =>
            new() { Width = 16, Height = 16, Fill = color, VerticalAlignment = VerticalAlignment.Center };

        private Border RowBorder(UIElement child) => new()
        {
            Background = (Brush)FindResource("SurfaceRaisedBrush"),
            BorderBrush = (Brush)FindResource("SurfaceBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Child = child,
        };

        private Button SmallButton(string text, string style = "SecondaryButton") => new()
        {
            Content = text,
            Style = (Style)FindResource(style),
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };


        // Строка из колонок: первая и последние — по содержимому, вторая растягивается.
        private static Grid Row(params UIElement[] cells)
        {
            var grid = new Grid();
            for (int i = 0; i < cells.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
                Grid.SetColumn(cells[i], i);
                grid.Children.Add(cells[i]);
            }
            return grid;
        }

        // Строка игрока: баланс и пєтушатня.
        private sealed class PlayerRow
        {
            private readonly MainWindow window;
            private readonly Ellipse dot;
            private readonly TextBlock name = new() { FontSize = 18, FontWeight = FontWeights.Bold };
            private readonly TextBlock status = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap };
            private readonly TextBlock balanceNow = new() { FontSize = 13, Margin = new Thickness(0, 0, 0, 3) };
            private readonly TextBox balance = new() { Width = 150, FontSize = 16 };
            private readonly Button setBalance;
            private readonly Button jail;
            private bool inJail;

            public int Id { get; }
            public UIElement Element { get; }

            public PlayerRow(MainWindow window, int id)
            {
                this.window = window;
                Id = id;
                dot = Dot(Brushes.Gray);
                name.Foreground = (Brush)window.FindResource("TextBrush");
                status.Foreground = balanceNow.Foreground = (Brush)window.FindResource("MutedTextBrush");

                setBalance = window.SmallButton("Задати", "PrimaryButton");
                setBalance.Click += async (_, _) => await SetBalanceAsync();
                balance.KeyDown += async (_, e) =>
                {
                    if (e.Key == Key.Enter)
                        await SetBalanceAsync();
                };
                jail = window.SmallButton("");
                jail.Width = 200;
                jail.Margin = new Thickness(16, 0, 0, 0);
                jail.Click += async (_, _) =>
                    await window.RunAsync(new AdminSetJail(Id, !inJail),
                        inJail ? $"{window.PlayerName(Id)} виходить {window.Words.FromJail}." : $"{window.PlayerName(Id)} — {window.Words.InJail}.");
                AutomationProperties.SetAutomationId(balance, $"Balance{id}");
                AutomationProperties.SetAutomationId(setBalance, $"SetBalance{id}");
                AutomationProperties.SetAutomationId(jail, $"Jail{id}");

                var who = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                who.Children.Add(name);
                who.Children.Add(status);
                var money = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                money.Children.Add(balanceNow);
                money.Children.Add(balance);
                setBalance.VerticalAlignment = VerticalAlignment.Bottom;

                Element = window.RowBorder(Row(dot, who, money, setBalance, jail));
            }

            private async Task SetBalanceAsync()
            {
                if (ParseMoney(balance.Text) is not int amount)
                {
                    window.ShowStatus("Баланс — це число гривень, наприклад 1 500 000.", false);
                    return;
                }
                await window.RunAsync(new AdminSetBalance(Id, amount), $"Баланс {window.PlayerName(Id)}: {window.Money(amount)}.");
                Keyboard.ClearFocus();
            }

            public void Update(PlayerSnapshot player, GameSnapshot snapshot, AdminView view)
            {
                inJail = player.IsInJail;
                dot.Fill = PlayerPalette.Get(view.ColorByPlayerId.TryGetValue(player.Id, out int color) ? color : player.Id);
                name.Text = player.Name;

                var parts = new List<string>();
                if (player.IsBankrupt)
                    parts.Add("вибув");
                else
                {
                    if (snapshot.CurrentPlayerId == player.Id)
                        parts.Add("ходить зараз");
                    if (player.IsInJail)
                        parts.Add(window.Words.InJail);
                    if (player.IsResting)
                        parts.Add(window.Words.RestNote);
                    parts.Add($"на «{window.Cells[player.Position].Name}»");
                    parts.Add(view.Seats?.FirstOrDefault(s => s.PlayerId == player.Id)?.Connection switch
                    {
                        SeatConnection.Offline => "відключився",
                        SeatConnection.Bot => "грає бот",
                        _ => "у мережі",
                    });
                }
                status.Text = string.Join(" · ", parts);

                balanceNow.Text = $"зараз {window.Money(player.Balance)}";
                // Пока админ вводит сумму, живые обновления поле не трогают.
                if (!balance.IsKeyboardFocusWithin)
                    balance.Text = Plain(player.Balance);

                bool active = !player.IsBankrupt && snapshot.WinnerId is null;
                balance.IsEnabled = setBalance.IsEnabled = jail.IsEnabled = active;
                jail.Content = player.IsInJail ? $"Випустити {window.Words.FromJail}" : $"Посадити {window.Words.IntoJail}";
            }
        }

        // Строка компании: владелец (кнопка с меню) и цена.
        private sealed class CompanyRow
        {
            private readonly MainWindow window;
            private readonly IReadOnlyList<(string Name, int? Id)> owners;
            private readonly TextBlock info = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap };
            private readonly TextBlock ownerText = new() { TextTrimming = TextTrimming.CharacterEllipsis };
            private readonly Button owner;
            private readonly TextBox price = new() { Width = 120, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
            private readonly Button setPrice;
            private int? currentOwner;

            public int Index { get; }
            public UIElement Element { get; }

            public CompanyRow(MainWindow window, int index, IReadOnlyList<(string Name, int? Id)> owners)
            {
                this.window = window;
                this.owners = owners;
                Index = index;
                var cell = window.Cells[index];
                info.Foreground = (Brush)window.FindResource("MutedTextBrush");

                owner = window.SmallButton("");
                owner.Content = ownerText;
                owner.Width = 190;
                owner.HorizontalContentAlignment = HorizontalAlignment.Left;
                owner.Click += (_, _) => ShowOwnerMenu(cell);

                setPrice = window.SmallButton("Задати ціну", "PrimaryButton");
                setPrice.Click += async (_, _) => await SetPriceAsync();
                price.Margin = new Thickness(24, 0, 0, 0);
                price.KeyDown += async (_, e) =>
                {
                    if (e.Key == Key.Enter)
                        await SetPriceAsync();
                };
                AutomationProperties.SetAutomationId(owner, $"Owner{index}");
                AutomationProperties.SetAutomationId(price, $"Price{index}");
                AutomationProperties.SetAutomationId(setPrice, $"SetPrice{index}");

                var bar = new Border { Width = 8, CornerRadius = new CornerRadius(4), Background = GroupPalette.Get(cell.Type) };
                var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
                title.Children.Add(new TextBlock { Text = cell.Name, FontSize = 17, FontWeight = FontWeights.Bold, Foreground = (Brush)window.FindResource("TextBrush") });
                title.Children.Add(info);

                Element = window.RowBorder(Row(bar, title, owner, price, setPrice));
            }

            // Меню владельцев: выбор сразу передаёт компанию.
            private void ShowOwnerMenu(BoardCell cell)
            {
                var menu = new ContextMenu { PlacementTarget = owner, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
                foreach (var (ownerName, id) in owners)
                {
                    var item = new MenuItem { Header = ownerName, IsEnabled = id != currentOwner, FontSize = 15 };
                    item.Click += async (_, _) => await window.RunAsync(new AdminSetOwner(Index, id), $"«{cell.Name}» → {ownerName}.");
                    menu.Items.Add(item);
                }
                menu.IsOpen = true;
            }

            private async Task SetPriceAsync()
            {
                if (ParseMoney(price.Text) is not int amount)
                {
                    window.ShowStatus("Ціна — це число гривень, наприклад 200 000.", false);
                    return;
                }
                await window.RunAsync(new AdminSetPrice(Index, amount), $"Ціна «{window.Cells[Index].Name}»: {window.Money(amount)}.");
                Keyboard.ClearFocus();
            }

            public void Update(CellSnapshot cell, GameSnapshot snapshot)
            {
                currentOwner = cell.OwnerId;
                var parts = new List<string> { $"ціна {window.Money(cell.Price)}" };
                if (cell.Level == GameRules.HeadOfficeLevel)
                    parts.Add(window.Terms.Office);
                else if (cell.Level > 0)
                    parts.Add($"{window.Terms.BranchesGenitive}: {cell.Level}");
                if (cell.IsMortgaged)
                    parts.Add("закладена");
                info.Text = string.Join(" · ", parts);

                string name = cell.OwnerId is int id ? snapshot.FindPlayer(id)?.Name ?? "?" : "Банк";
                ownerText.Text = $"Власник: {name}  ▾";
                if (!price.IsKeyboardFocusWithin)
                    price.Text = Plain(cell.Price);

                bool running = snapshot.WinnerId is null;
                owner.IsEnabled = running;
                // Цена — только у свободной компании (§14).
                price.IsEnabled = setPrice.IsEnabled = running && cell.OwnerId is null;
            }
        }
    }
}
