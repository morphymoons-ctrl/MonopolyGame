using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Monopoly.Core;

namespace Monopoly.App
{
    // Итоги партии (RULES.md, §16) — карточка поверх поля, как обмен: игроки, награды, самые доходные клетки.
    // Во время игры — текущая статистика, после победы открывается сама.
    public class StatsPanel : Border
    {
        // Ширины колонок таблицы игроков: место, игрок, капитал, заплатил, получил, пєтушатня, казино, куплено.
        private static readonly double[] Columns = { 56, 240, 190, 170, 170, 130, 160, 110 };

        private readonly GameSnapshot snapshot;
        private readonly StatsSnapshot stats;
        private readonly Func<int, Brush> colorOf;

        public event Action? Closed;

        public StatsPanel(GameSnapshot snapshot, Func<int, Brush> colorOf, string duration)
        {
            this.snapshot = snapshot;
            this.colorOf = colorOf;
            stats = snapshot.Stats ?? new StatsSnapshot(0, Array.Empty<PlayerStatsSnapshot>(), new int[EventText.Cells.Count], Array.Empty<int>());
            Style = (Style)FindResource("Card");
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow");
            Width = Columns.Sum() + 64;
            Padding = new Thickness(32, 26, 32, 26);
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            TextElement.SetFontFamily(this, (FontFamily)FindResource("UiFont"));
            TextElement.SetFontSize(this, 19);
            TextElement.SetForeground(this, (Brush)FindResource("TextBrush"));
            Focusable = true;
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Closed?.Invoke();
                }
            };

            var root = new StackPanel();
            root.Children.Add(Header(duration));
            root.Children.Add(PlayersTable());
            if (Awards() is { } awards)
            {
                root.Children.Add(SectionTitle("Нагороди"));
                root.Children.Add(awards);
            }
            root.Children.Add(SectionTitle("Клітинки, що принесли найбільше оренди"));
            root.Children.Add(CellsBlock());
            Child = root;
            Loaded += (_, _) => Focus();
        }

        private Brush Muted => (Brush)FindResource("MutedTextBrush");
        private Brush Accent => (Brush)FindResource("AccentBrush");
        private Brush Raised => (Brush)FindResource("SurfaceRaisedBrush");

        private string Name(int id) => snapshot.FindPlayer(id)?.Name ?? "?";

        // --- Заголовок ---

        private UIElement Header(string duration)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            var top = new DockPanel();
            var close = new Button { Content = "✕", FontSize = 18, Padding = new Thickness(14, 4, 14, 4), MinHeight = 0, VerticalAlignment = VerticalAlignment.Top };
            close.Click += (_, _) => Closed?.Invoke();
            DockPanel.SetDock(close, Dock.Right);
            top.Children.Add(close);
            bool over = snapshot.WinnerId is not null;
            top.Children.Add(new TextBlock { Text = over ? "Підсумки партії" : "Статистика партії", FontSize = 34, FontWeight = FontWeights.Bold });
            panel.Children.Add(top);

            var parts = new List<string>();
            if (snapshot.WinnerId is int winner)
                parts.Add($"Перемога: {Name(winner)}");
            parts.Add($"ходів: {stats.Turns}");
            if (!string.IsNullOrEmpty(duration))
                parts.Add(duration.ToLowerInvariant());
            parts.Add($"дошка «{ThemeWords.ThemeName(EventText.Theme)}»");
            panel.Children.Add(new TextBlock { Text = string.Join(" · ", parts), Foreground = over ? Accent : Muted, FontSize = 19, Margin = new Thickness(0, 4, 0, 0) });
            return panel;
        }

        private TextBlock SectionTitle(string text) =>
            new() { Text = text, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 22, 0, 10) };

        // --- Таблица игроков ---

        private UIElement PlayersTable()
        {
            var table = new StackPanel();
            string jail = EventText.Cells[GameRules.JailCell].Name;
            string casino = EventText.Cells[16].Name;
            table.Children.Add(Row(new UIElement[]
            {
                HeaderCell("№"), HeaderCell("Гравець"), HeaderCell("Капітал"), HeaderCell("Заплатив оренди"),
                HeaderCell("Отримав оренди"), HeaderCell(jail), HeaderCell(casino), HeaderCell("Куплено"),
            }, header: true));

            var board = EventText.Cells;
            int place = 1;
            foreach (int id in GameRules.Standings(board, snapshot, stats))
            {
                var player = snapshot.FindPlayer(id);
                var s = stats.For(id);
                if (player is null || s is null)
                    continue;
                bool first = place == 1;
                var name = new StackPanel { Orientation = Orientation.Horizontal };
                name.Children.Add(new Ellipse { Width = 16, Height = 16, Fill = colorOf(id), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                name.Children.Add(new TextBlock { Text = player.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = Columns[1] - 40 });
                var capital = player.IsBankrupt
                    ? Cell("вибув", Muted)
                    : Cell(EventText.Money(GameRules.NetWorth(board, id, player.Balance)), first ? Accent : null, FontWeights.SemiBold);
                string casinoText = s.CasinoNet switch
                {
                    > 0 => "+" + EventText.Money(s.CasinoNet),
                    < 0 => "−" + EventText.Money(-s.CasinoNet),
                    _ => "—",
                };
                table.Children.Add(Row(new UIElement[]
                {
                    Cell(place.ToString(), first ? Accent : Muted, FontWeights.Bold),
                    name,
                    capital,
                    Cell(s.RentPaid > 0 ? EventText.Money(s.RentPaid) : "—"),
                    Cell(s.RentReceived > 0 ? EventText.Money(s.RentReceived) : "—"),
                    Cell(s.TimesJailed > 0 ? Times(s.TimesJailed) : "—"),
                    Cell(casinoText, s.CasinoNet > 0 ? (Brush)FindResource("SuccessBrush") : s.CasinoNet < 0 ? (Brush)FindResource("DangerBrush") : null),
                    Cell(s.CompaniesBought.ToString()),
                }, outline: first && snapshot.WinnerId is not null ? Accent : null, faded: player.IsBankrupt));
                place++;
            }
            return table;
        }

        private Border Row(UIElement[] cells, Brush? outline = null, bool faded = false, bool header = false)
        {
            var grid = new Grid();
            for (int i = 0; i < Columns.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Columns[i]) });
                Grid.SetColumn(cells[i], i);
                grid.Children.Add(cells[i]);
            }
            return new Border
            {
                Child = grid,
                Background = header ? Brushes.Transparent : Raised,
                BorderBrush = outline ?? Brushes.Transparent,
                BorderThickness = new Thickness(outline is null ? 0 : 2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, header ? 0 : 10, 12, header ? 6 : 10),
                Margin = new Thickness(0, 0, 0, header ? 0 : 6),
                Opacity = faded ? 0.55 : 1,
            };
        }

        private TextBlock HeaderCell(string text) =>
            new() { Text = text, Foreground = Muted, FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Bottom };

        private static TextBlock Cell(string text, Brush? color = null, FontWeight? weight = null)
        {
            var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), FontWeight = weight ?? FontWeights.Normal };
            if (color is not null)
                block.Foreground = color;
            return block;
        }

        // «1 раз», «3 рази», «5 разів».
        private static string Times(int n)
        {
            int last = n % 10, lastTwo = n % 100;
            string word = last == 1 && lastTwo != 11 ? "раз" : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "рази" : "разів";
            return $"{n} {word}";
        }

        // --- Награды: кто больше всех получил и заплатил аренды, чаще сидел, лучше сыграл в казино ---

        private UIElement? Awards()
        {
            var alive = stats.Players.Where(p => snapshot.FindPlayer(p.PlayerId) is not null).ToList();
            var awards = new WrapPanel();
            void Add(string title, Func<PlayerStatsSnapshot, int> value, Func<int, string> format)
            {
                var best = alive.OrderByDescending(value).FirstOrDefault();
                if (best is null || value(best) <= 0)
                    return;
                var body = new StackPanel();
                body.Children.Add(new TextBlock { Text = title, Foreground = Muted, FontSize = 16 });
                var who = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                who.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = colorOf(best.PlayerId), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                who.Children.Add(new TextBlock { Text = Name(best.PlayerId), FontWeight = FontWeights.Bold });
                body.Children.Add(who);
                body.Children.Add(new TextBlock { Text = format(value(best)), Foreground = Accent, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0) });
                awards.Children.Add(new Border { Child = body, Background = Raised, CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 10, 10), MinWidth = 270 });
            }

            Add("Найбільше отримав оренди", p => p.RentReceived, EventText.Money);
            Add("Найбільше заплатив оренди", p => p.RentPaid, EventText.Money);
            Add($"Найчастіше — «{EventText.Cells[GameRules.JailCell].Name}»", p => p.TimesJailed, Times);
            Add($"Найкраще — «{EventText.Cells[16].Name}»", p => p.CasinoNet, amount => "+" + EventText.Money(amount));
            return awards.Children.Count > 0 ? awards : null;
        }

        // --- Клетки: лидер крупно, рядом первая пятёрка ---

        private UIElement CellsBlock()
        {
            var ranked = stats.CellIncome
                .Select((income, index) => (income, index))
                .Where(c => c.income > 0)
                .OrderByDescending(c => c.income)
                .Take(5)
                .ToList();
            if (ranked.Count == 0)
            {
                return new TextBlock { Text = "Оренду за цю партію ще ніхто не платив.", Foreground = Muted };
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var leader = LeaderCard(ranked[0].index, ranked[0].income);
            Grid.SetColumn(leader, 0);
            grid.Children.Add(leader);

            var list = new StackPanel();
            for (int i = 0; i < ranked.Count; i++)
            {
                var (income, index) = ranked[i];
                var cell = EventText.Cells[index];
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                var money = new TextBlock { Text = EventText.Money(income), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(money, Dock.Right);
                row.Children.Add(money);
                row.Children.Add(new TextBlock { Text = $"{i + 1}.", Foreground = i == 0 ? Accent : Muted, FontWeight = FontWeights.Bold, Width = 34, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new Border { Width = 14, Height = 26, CornerRadius = new CornerRadius(4), Background = GroupPalette.Get(cell.Type), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = cell.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                list.Children.Add(new Border { Child = row, Background = Raised, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 0, 6) });
            }
            Grid.SetColumn(list, 2);
            grid.Children.Add(list);
            return grid;
        }

        private Border LeaderCard(int index, int income)
        {
            var cell = EventText.Cells[index];
            var owner = snapshot.Cells.Count > index ? snapshot.Cells[index].OwnerId : null;
            var body = new StackPanel();
            body.Children.Add(new Border { Height = 12, CornerRadius = new CornerRadius(6), Background = GroupPalette.Get(cell.Type), Margin = new Thickness(0, 0, 0, 14) });

            var head = new DockPanel();
            if (Logos.For(index, EventText.Theme) is { } logo)
            {
                var image = new Image { Source = logo, Width = 84, Height = 84, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 16, 0) };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                DockPanel.SetDock(image, Dock.Left);
                head.Children.Add(image);
            }
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(new TextBlock { Text = "Найприбутковіша клітинка", Foreground = Muted, FontSize = 16 });
            names.Children.Add(new TextBlock { Text = cell.Name, FontSize = 26, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            names.Children.Add(new TextBlock { Text = EventText.GroupName(cell.Type), Foreground = Muted, FontSize = 16 });
            head.Children.Add(names);
            body.Children.Add(head);

            body.Children.Add(new TextBlock { Text = $"Принесла {EventText.Money(income)} оренди", FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Accent, Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap });
            body.Children.Add(new TextBlock { Text = owner is int id ? $"Зараз у гравця {Name(id)}" : "Зараз у банку", Foreground = Muted, FontSize = 17, Margin = new Thickness(0, 4, 0, 0) });
            return new Border { Child = body, Background = Raised, CornerRadius = new CornerRadius(14), Padding = new Thickness(18, 16, 18, 18) };
        }
    }
}
