using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Monopoly.Core;

namespace Monopoly.App
{
    // Окно предложения обмена: с кем, что отдаёте, что просите. Проверяет условия хост.
    public class TradeWindow : Window
    {
        private readonly GameSnapshot snapshot;
        private readonly int myId;
        private readonly ComboBox targetBox = new() { FontSize = 18, Margin = new Thickness(0, 4, 0, 12) };
        private readonly Side mine;
        private readonly Side theirs;
        private readonly TextBlock errorText = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

        public ProposeTrade? Proposal { get; private set; }

        public TradeWindow(GameSnapshot snapshot, int myId, Func<int, Brush> colorOf)
        {
            this.snapshot = snapshot;
            this.myId = myId;
            Title = "Предложить обмен";
            Width = 820;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FontSize = 16;

            mine = new Side("Вы отдаёте");
            theirs = new Side("Вы просите");

            foreach (var player in snapshot.Players.Where(p => p.Id != myId && !p.IsBankrupt))
            {
                targetBox.Items.Add(new ComboBoxItem { Content = player.Name, Tag = player.Id, Foreground = colorOf(player.Id) });
            }
            targetBox.SelectionChanged += (_, _) => FillTheirs();

            var root = new DockPanel { Margin = new Thickness(16) };

            var top = new StackPanel();
            top.Children.Add(new TextBlock { Text = "С кем меняетесь" });
            top.Children.Add(targetBox);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "Предложить", Width = 140, Height = 36, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Отмена", Width = 100, Height = 36, IsCancel = true };
            ok.Click += (_, _) => Submit();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var bottom = new StackPanel();
            bottom.Children.Add(errorText);
            bottom.Children.Add(buttons);
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(mine.Panel, 0);
            Grid.SetColumn(theirs.Panel, 2);
            columns.Children.Add(mine.Panel);
            columns.Children.Add(theirs.Panel);
            root.Children.Add(columns);

            Content = root;

            mine.Fill(snapshot, myId);
            if (targetBox.Items.Count > 0)
            {
                targetBox.SelectedIndex = 0;
            }
        }

        private int? TargetId => (targetBox.SelectedItem as ComboBoxItem)?.Tag as int?;

        private void FillTheirs()
        {
            if (TargetId is int id)
            {
                theirs.Fill(snapshot, id);
            }
        }

        private void Submit()
        {
            if (TargetId is not int target)
            {
                errorText.Text = "Выберите, с кем меняться.";
                return;
            }
            var give = mine.Terms(out var error);
            var take = error is null ? theirs.Terms(out error) : null;
            if (error is not null || give is null || take is null)
            {
                errorText.Text = error ?? "";
                return;
            }
            Proposal = new ProposeTrade(myId, target, give, take);
            DialogResult = true;
        }

        // Одна сторона обмена: компании галочками, деньги и карточки числами.
        private sealed class Side
        {
            private readonly StackPanel cells = new();
            private readonly TextBox money = new() { Text = "0", Padding = new Thickness(4) };
            private readonly TextBox jailCards = new() { Text = "0", Padding = new Thickness(4) };
            private readonly TextBlock jailLabel = new() { Margin = new Thickness(0, 8, 0, 0) };

            public StackPanel Panel { get; } = new();

            public Side(string title)
            {
                Panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
                Panel.Children.Add(new ScrollViewer { Content = cells, Height = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
                Panel.Children.Add(new TextBlock { Text = "Деньги, грн", Margin = new Thickness(0, 8, 0, 0) });
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
                    {
                        continue;
                    }
                    string note = state.IsMortgaged ? " (заложена)" : state.Level > 0 ? " (есть филиалы)" : "";
                    cells.Children.Add(new CheckBox { Content = EventText.Cells[i].Name + note, Tag = i, Margin = new Thickness(0, 2, 0, 2) });
                }
                if (cells.Children.Count == 0)
                {
                    cells.Children.Add(new TextBlock { Text = "Компаний нет", Foreground = Brushes.Gray });
                }

                var player = snapshot.FindPlayer(playerId)!;
                money.Text = "0";
                jailCards.Text = "0";
                jailLabel.Text = $"Карточки «Выйти из тюрьмы» (есть: {player.JailCards})";
                bool hasCards = player.JailCards > 0;
                jailLabel.Visibility = hasCards ? Visibility.Visible : Visibility.Collapsed;
                jailCards.Visibility = hasCards ? Visibility.Visible : Visibility.Collapsed;
            }

            public TradeTerms? Terms(out string? error)
            {
                error = null;
                if (!int.TryParse(money.Text.Trim(), out int moneyValue) || moneyValue < 0)
                {
                    error = "Деньги — целое число от 0.";
                    return null;
                }
                if (!int.TryParse(jailCards.Text.Trim(), out int cardValue) || cardValue < 0)
                {
                    error = "Карточки — целое число от 0.";
                    return null;
                }
                var chosen = cells.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (int)c.Tag).ToList();
                return new TradeTerms(chosen, moneyValue, cardValue);
            }
        }
    }
}
