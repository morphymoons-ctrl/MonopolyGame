using System;
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
    // Предложение обмена — карточка внутри игры, поверх поля: с кем, что отдаёте, что просите. Проверяет условия хост.
    public class TradePanel : Border
    {
        private readonly GameSnapshot snapshot;
        private readonly int myId;
        private readonly WrapPanel targets = new() { Margin = new Thickness(0, 8, 0, 14) };
        private readonly Side mine;
        private readonly Side theirs;
        private readonly TextBlock errorText = new() { Foreground = PlayerPalette.Make("#FF8A8D"), FontSize = 18, TextWrapping = TextWrapping.Wrap };
        private int? targetId;

        // Встречное предложение (§11): на какое предложение отвечаем; null — обычное предложение обмена.
        private readonly TradeOffer? counterTo;

        // Закрыта: предложение (ProposeTrade или CounterTrade) или null — отмена.
        public event Action<GameAction?>? Finished;

        public TradePanel(GameSnapshot snapshot, int myId, Func<int, Brush> colorOf, TradeOffer? counterTo = null)
        {
            this.snapshot = snapshot;
            this.myId = myId;
            this.counterTo = counterTo;
            Style = (Style)FindResource("Card");
            Effect = (System.Windows.Media.Effects.Effect)FindResource("SoftShadow");
            Width = 1040;
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
                    Finished?.Invoke(null);
                }
            };

            mine = new Side("Ви віддаєте");
            theirs = new Side("Ви просите");

            var root = new StackPanel();

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var close = new Button { Content = "✕", FontSize = 18, Padding = new Thickness(14, 4, 14, 4), MinHeight = 0, VerticalAlignment = VerticalAlignment.Top };
            close.Click += (_, _) => Finished?.Invoke(null);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            header.Children.Add(new TextBlock { Text = counterTo is null ? "Запропонувати обмін" : "Змінити умови", FontSize = 34, FontWeight = FontWeights.Bold });
            root.Children.Add(header);

            root.Children.Add(new TextBlock { Text = "З ким мінятися", Foreground = (Brush)FindResource("MutedTextBrush") });
            // Встречное предложение — только тому, кто предлагал.
            foreach (var player in snapshot.Players.Where(p => p.Id != myId && !p.IsBankrupt && (counterTo is null || p.Id == counterTo.FromId)))
            {
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(new Ellipse { Width = 16, Height = 16, Fill = colorOf(player.Id), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                content.Children.Add(new TextBlock { Text = player.Name, FontSize = 20 });
                var chip = new RadioButton { Content = content, GroupName = "TradeTarget", Style = (Style)FindResource("Chip"), Tag = player.Id };
                int id = player.Id;
                chip.Checked += (_, _) => SelectTarget(id);
                targets.Children.Add(chip);
            }
            root.Children.Add(targets);

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(mine.Panel, 0);
            Grid.SetColumn(theirs.Panel, 2);
            columns.Children.Add(mine.Panel);
            columns.Children.Add(theirs.Panel);
            root.Children.Add(columns);

            var bottom = new DockPanel { Margin = new Thickness(0, 18, 0, 0) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var ok = new Button { Content = "Запропонувати", FontSize = 19, MinWidth = 190, Margin = new Thickness(0, 0, 12, 0), IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
            var cancel = new Button { Content = "Скасувати", FontSize = 19, MinWidth = 150 };
            ok.Click += (_, _) => Submit();
            cancel.Click += (_, _) => Finished?.Invoke(null);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Right);
            bottom.Children.Add(buttons);
            errorText.VerticalAlignment = VerticalAlignment.Center;
            bottom.Children.Add(errorText);
            root.Children.Add(bottom);

            Child = root;

            mine.Fill(snapshot, myId);
            if (targets.Children.Count > 0)
                ((RadioButton)targets.Children[0]).IsChecked = true;
            // Встречное — с условиями пришедшего предложения, с нашей стороны: что у нас просили, теперь мы отдаём.
            if (counterTo is not null)
            {
                mine.Set(counterTo.Take);
                theirs.Set(counterTo.Give);
            }
            Loaded += (_, _) => Focus();
        }

        private void SelectTarget(int id)
        {
            targetId = id;
            theirs.Title = $"Ви просите в гравця {snapshot.FindPlayer(id)?.Name}";
            theirs.Fill(snapshot, id);
        }

        private void Submit()
        {
            if (targetId is not int target)
            {
                errorText.Text = "Оберіть, з ким мінятися.";
                return;
            }
            var give = mine.Terms(out var error);
            var take = error is null ? theirs.Terms(out error) : null;
            if (error is not null || give is null || take is null)
            {
                errorText.Text = error ?? "";
                return;
            }
            Finished?.Invoke(counterTo is null ? new ProposeTrade(myId, target, give, take) : new CounterTrade(myId, give, take));
        }

        // Одна сторона обмена: компании галочками, деньги и карточки числами.
        private sealed class Side
        {
            private readonly TextBlock title = new() { FontWeight = FontWeights.Bold, FontSize = 21, Margin = new Thickness(0, 0, 0, 8) };
            private readonly StackPanel cells = new();
            private readonly TextBox money = new() { Text = "0", FontSize = 20 };
            private readonly TextBox jailCards = new() { Text = "0", FontSize = 20 };
            private readonly TextBlock jailLabel = new() { Margin = new Thickness(0, 12, 0, 6), Foreground = (Brush)Application.Current.Resources["MutedTextBrush"] };

            public StackPanel Panel { get; } = new();

            public string Title
            {
                set => title.Text = value;
            }

            public Side(string text)
            {
                title.Text = text;
                Panel.Children.Add(title);
                Panel.Children.Add(new Border
                {
                    Background = PlayerPalette.Make("#0F1C30"),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(6),
                    Child = new ScrollViewer { Content = cells, Height = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
                });
                Panel.Children.Add(new TextBlock { Text = $"Гроші, {GameRules.Currency(EventText.Theme)}", Margin = new Thickness(0, 12, 0, 6), Foreground = (Brush)Application.Current.Resources["MutedTextBrush"] });
                Panel.Children.Add(money);
                Panel.Children.Add(jailLabel);
                Panel.Children.Add(jailCards);
            }

            public void Fill(GameSnapshot snapshot, int playerId)
            {
                cells.Children.Clear();
                for (int i = 0; i < snapshot.Cells.Count; i++)
                {
                    var state = snapshot.Cells[i];
                    if (state.OwnerId != playerId)
                        continue;

                    var cell = EventText.Cells[i];
                    var content = new StackPanel { Orientation = Orientation.Horizontal };
                    content.Children.Add(new Border { Width = 6, Height = 22, CornerRadius = new CornerRadius(3), Background = GroupPalette.Get(cell.Type), Margin = new Thickness(0, 0, 10, 0) });
                    content.Children.Add(new TextBlock { Text = cell.Name, FontSize = 19 });
                    string note = state.IsMortgaged ? "закладена" : state.Level > 0 ? $"є {EventText.Terms.Branches}" : "";
                    if (note.Length > 0)
                        content.Children.Add(new TextBlock { Text = "  · " + note, FontSize = 16, Foreground = (Brush)Application.Current.Resources["MutedTextBrush"], VerticalAlignment = VerticalAlignment.Center });
                    cells.Children.Add(new CheckBox { Content = content, Tag = i });
                }
                if (cells.Children.Count == 0)
                    cells.Children.Add(new TextBlock { Text = "Компаній немає", Margin = new Thickness(10, 8, 0, 0), Foreground = (Brush)Application.Current.Resources["MutedTextBrush"] });

                var player = snapshot.FindPlayer(playerId)!;
                money.Text = "0";
                jailCards.Text = "0";
                jailLabel.Text = $"Картки «{EventText.Words.JailCard}» (є: {player.JailCards})";
                bool hasCards = player.JailCards > 0;
                jailLabel.Visibility = hasCards ? Visibility.Visible : Visibility.Collapsed;
                jailCards.Visibility = hasCards ? Visibility.Visible : Visibility.Collapsed;
            }

            // Заполнить условиями: отметить компании, вписать деньги и карточки.
            public void Set(TradeTerms terms)
            {
                foreach (var box in cells.Children.OfType<CheckBox>())
                    box.IsChecked = terms.Cells.Contains((int)box.Tag);
                money.Text = GameRules.MoneyInput(terms.Money, EventText.Theme);
                jailCards.Text = terms.JailCards.ToString();
            }

            public TradeTerms? Terms(out string? error)
            {
                error = null;
                // Пробелы между тысячами допускаются: «50 000»; на досках в миллионах — дробь «1,5» (§15). Сумма округляется (§11).
                if (money.Text.Contains('-') || GameRules.ParseMoney(money.Text, EventText.Theme) is not int moneyValue)
                {
                    error = GameRules.FractionalInput(EventText.Theme) ? "Гроші — число мільйонів від 0, наприклад 1,5." : "Гроші — ціле число від 0.";
                    return null;
                }
                if (!int.TryParse(Digits(jailCards.Text), out int cardValue) || cardValue < 0)
                {
                    error = "Картки — ціле число від 0.";
                    return null;
                }
                var chosen = cells.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (int)c.Tag).ToList();
                return new TradeTerms(chosen, moneyValue, cardValue);
            }

            private static string Digits(string text) => string.Concat(text.Where(ch => !char.IsWhiteSpace(ch)));
        }
    }
}
