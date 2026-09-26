using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.App
{
    // Экран партии: проигрывает обновления от хоста и отправляет ему действия игрока.
    // Какие кнопки показать, решает хост — через список доступных действий.
    public partial class GameScreen : UserControl
    {
        private readonly GameClient client;
        private readonly GameManager gameManager;
        private readonly Func<Task> leave;
        private readonly UserSettings settings;
        private readonly Sounds sounds = new();
        private readonly DiceView die1 = new(96);
        private readonly DiceView die2 = new(96);
        // Обновления проигрываются по очереди: пока идёт анимация, следующие ждут.
        private readonly Queue<GameUpdate> updates = new();
        private bool playing;
        private IReadOnlyList<GameAction> available = Array.Empty<GameAction>();
        // Действие отправлено, ответа ещё нет — кнопки скрыты.
        private bool sending;

        public GameScreen(GameClient client, GameStartInfo info, UserSettings settings, Func<Task> leave)
        {
            InitializeComponent();
            this.client = client;
            this.leave = leave;
            this.settings = settings;
            sounds.Enabled = settings.SoundOn;
            Die1Host.Content = die1;
            Die2Host.Content = die2;
            gameManager = new GameManager(ActionLog, GameBoardCanvas, TokenCanvas, PlayersPanel, info);
            gameManager.CellClicked += cell =>
            {
                gameManager.Select(cell);
                ShowCell();
            };
            gameManager.DrawBoard();
            UpdateSoundButton();
            Refresh();
        }

        private int MyId => gameManager.MyPlayerId;
        private GameSnapshot? Snapshot => gameManager.Snapshot;
        private bool Busy => sending || playing;

        public void Apply(GameUpdate update)
        {
            updates.Enqueue(update);
            if (!playing)
            {
                _ = PlayUpdatesAsync();
            }
        }

        public void AddLog(string text) => gameManager.AddLog(text);

        private async Task PlayUpdatesAsync()
        {
            playing = true;
            Refresh();
            while (updates.Count > 0)
            {
                var update = updates.Dequeue();
                await gameManager.PlayAsync(update, die1, die2, sounds);
                available = update.AvailableActions;
                sending = false;
            }
            playing = false;
            Refresh();
        }

        private void Refresh()
        {
            ShowSituation();
            ShowActions();
            ShowCell();
        }

        // --- Карточка в центре поля: что происходит ---

        private void ShowSituation()
        {
            if (Snapshot is not { } s)
            {
                ContextTitle.Text = "Раздаём фишки…";
                ContextText.Text = "";
                ContextBar.Visibility = Visibility.Collapsed;
                return;
            }

            var (title, text, bar) = Describe(s);
            ContextTitle.Text = title;
            ContextText.Text = text;
            ContextText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            ContextBar.Background = bar;
            ContextBar.Visibility = bar is null ? Visibility.Collapsed : Visibility.Visible;
        }

        private (string Title, string Text, Brush? Bar) Describe(GameSnapshot s)
        {
            string Name(int id) => s.FindPlayer(id)?.Name ?? "?";
            string Target(int? creditor) => creditor is int c ? $"игроку {Name(c)}" : "банку";
            var danger = (Brush)FindResource("DangerBrush");
            var blue = (Brush)FindResource("BlueBrush");
            var me = s.FindPlayer(MyId);
            bool myTurn = s.CurrentPlayerId == MyId;

            switch (s.Phase)
            {
                case TurnPhase.GameOver:
                    return ($"Победил {Name(s.WinnerId!.Value)}!", "Партия окончена. Выйдите в меню, чтобы собрать новую.", (Brush)FindResource("AccentBrush"));
                case TurnPhase.Debt when s.Debt!.DebtorId == MyId:
                    return ("Не хватает денег",
                        $"Долг {GameManager.Format(s.Debt.Amount)} {Target(s.Debt.CreditorId)}. Нажмите на свою компанию на поле, чтобы продать филиалы или заложить её. Долг спишется сам, как только хватит денег.",
                        danger);
                case TurnPhase.Debt:
                    return ($"{Name(s.Debt!.DebtorId)} ищет деньги", $"Долг {GameManager.Format(s.Debt.Amount)} {Target(s.Debt.CreditorId)}. Ждём.", danger);
                case TurnPhase.Auction:
                    var auction = s.Auction!;
                    var lot = EventText.Cells[auction.CellIndex];
                    string bid = auction.LeaderId is int leader
                        ? $"Ставка {GameManager.Format(auction.HighBid)} — {Name(leader)}."
                        : $"Ставок пока нет. Первая — от {GameManager.Format(auction.MinBid)}.";
                    var passed = auction.PassedIds.Select(Name).ToList();
                    string passedText = passed.Count > 0 ? $" Спасовали: {string.Join(", ", passed)}." : "";
                    return ($"Аукцион: «{lot.Name}»", $"Цена компании {GameManager.Format(lot.Price)}. {bid}{passedText}", GroupPalette.Get(lot.Type));
                case TurnPhase.TradeOffer when s.Trade!.ToId == MyId:
                    return ("Вам предлагают обмен", $"{Name(s.Trade.FromId)} {EventText.DescribeOffer(s.Trade, s)}", blue);
                case TurnPhase.TradeOffer when s.Trade!.FromId == MyId:
                    return ($"Ждём ответа: {Name(s.Trade.ToId)}", $"Вы {EventText.DescribeOffer(s.Trade, s)}", blue);
                case TurnPhase.TradeOffer:
                    return ("Идёт обмен", $"{Name(s.Trade!.FromId)} → {Name(s.Trade.ToId)}: {EventText.DescribeOffer(s.Trade, s)}", blue);
                case TurnPhase.BuyDecision:
                    var cell = EventText.Cells[s.PendingPurchase!.Value];
                    return myTurn
                        ? ($"«{cell.Name}» свободна", $"{EventText.GroupName(cell.Type)} · {GameManager.Format(cell.Price)}. Купите её или откажитесь — тогда начнётся аукцион.", GroupPalette.Get(cell.Type))
                        : ($"{Name(s.CurrentPlayerId)} решает", $"Покупать ли «{cell.Name}» за {GameManager.Format(cell.Price)}.", GroupPalette.Get(cell.Type));
            }

            if (!myTurn)
            {
                var current = s.FindPlayer(s.CurrentPlayerId)!;
                return ($"Ходит {current.Name}", current.IsInJail ? "Сидит в тюрьме." : "", null);
            }
            if (s.Phase == TurnPhase.AwaitingRoll && me?.IsInJail == true)
            {
                return ("Вы в тюрьме", $"Заплатите залог {GameManager.Format(GameRules.BailAmount)}, используйте карточку или бросьте кубики — нужен дубль.", null);
            }
            if (s.Phase == TurnPhase.AwaitingRoll)
            {
                return s.LastRoll is null
                    ? ("Ваш ход", "Бросьте кубики. До броска можно строить филиалы, закладывать компании и предлагать обмен.", null)
                    : ("Дубль! Бросайте ещё раз", "", null);
            }
            return s.CasinoAvailable
                ? ("Казино", "Ставка 50–300 грн: 50% — проигрыш, 10% — возврат, 35% — ×2, 5% — ×3. Можно не играть — просто завершите ход.", PlayerPalette.Make("#E0569B"))
                : ("Ваш ход", "Можно строить филиалы, закладывать компании и предлагать обмен. Потом завершите ход.", null);
        }

        // --- Кнопки действий ---

        private void ShowActions()
        {
            ActionsPanel.Children.Clear();
            TradeButton.IsEnabled = !Busy && available.OfType<ProposeTrade>().Any();
            ActionsPanel.Visibility = Busy ? Visibility.Collapsed : Visibility.Visible;
            if (Snapshot is not { } s || Busy)
            {
                return;
            }

            if (available.OfType<PlaceBid>().FirstOrDefault() is { } minBid && s.Auction is { } auction)
            {
                int balance = s.FindPlayer(MyId)?.Balance ?? 0;
                int baseBid = auction.LeaderId is null ? 0 : auction.HighBid;
                var amounts = new[] { minBid.Amount, baseBid + 50, baseBid + 100 }
                    .Where(a => a >= minBid.Amount && a <= balance)
                    .Distinct();
                bool first = true;
                foreach (int amount in amounts)
                {
                    AddButton(ActionsPanel, $"Ставка {GameManager.Format(amount)}", new PlaceBid(MyId, amount), first ? "PrimaryButton" : "BoardButton");
                    first = false;
                }
            }

            foreach (var action in available)
            {
                if (action is PlaceBid or ProposeTrade or BuildBranch or SellBranch or MortgageCompany or RedeemCompany)
                {
                    continue;
                }
                string style = action switch
                {
                    RollDice or BuyProperty or AcceptTrade => "PrimaryButton",
                    EndTurn when !available.Any(a => a is RollDice) => "PrimaryButton",
                    DeclareBankruptcy => "DangerButton",
                    _ => "BoardButton",
                };
                AddButton(ActionsPanel, EventText.ActionLabel(action, s), action, style);
            }
        }

        private void AddButton(Panel panel, string label, GameAction action, string style)
        {
            var button = new Button
            {
                Content = label,
                FontSize = 19,
                Margin = new Thickness(0, 0, 10, 10),
                Style = (Style)FindResource(style)
            };
            button.Click += async (_, _) => await SendAsync(action);
            panel.Children.Add(button);
        }

        private async Task SendAsync(GameAction action)
        {
            sending = true;
            Refresh();

            var error = await client.SendActionAsync(action);
            if (error is not null)
            {
                gameManager.AddLog(error);
                sending = false;
                Refresh();
            }
        }

        // --- Карточка компании ---

        private void ShowCell()
        {
            CellActions.Children.Clear();
            RentTable.Children.Clear();
            RentTable.RowDefinitions.Clear();
            RentTable.ColumnDefinitions.Clear();
            if (gameManager.SelectedCell is not int index)
            {
                return;
            }

            var cell = EventText.Cells[index];
            var state = Snapshot?.Cells[index];
            CellTitle.Text = cell.Name;
            DeedHeader.Background = GroupPalette.Get(cell.Type);
            CellTitle.Foreground = cell.Type == CellType.Bank ? (Brush)FindResource("OnAccentBrush") : Brushes.White;

            if (!cell.IsPurchasable)
            {
                CellInfo.Text = DescribeSpecial(cell.Type);
                return;
            }

            string owner = state?.OwnerId is int id ? Snapshot?.FindPlayer(id)?.Name ?? "?" : "банк";
            string status = state is { IsMortgaged: true } ? " · заложена, аренды нет" : "";
            CellInfo.Text = $"{EventText.GroupName(cell.Type)} · цена {GameManager.Format(cell.Price)} · владелец: {owner}{status}";
            FillRentTable(index, cell, state);

            if (!Busy)
            {
                foreach (var action in available)
                {
                    (string Label, string Style)? button = action switch
                    {
                        BuildBranch b when b.CellIndex == index => (state?.Level == GameRules.HeadOfficeLevel - 1
                            ? $"Головной офис · {GameManager.Format(cell.BranchCost)}"
                            : $"Филиал · {GameManager.Format(cell.BranchCost)}", "PrimaryButton"),
                        SellBranch s when s.CellIndex == index => ($"Продать филиал · +{GameManager.Format(cell.BranchSaleValue)}", "SecondaryButton"),
                        MortgageCompany m when m.CellIndex == index => ($"Заложить · +{GameManager.Format(cell.MortgageValue)}", "SecondaryButton"),
                        RedeemCompany r when r.CellIndex == index => ($"Выкупить · {GameManager.Format(cell.RedeemCost)}", "PrimaryButton"),
                        _ => null,
                    };
                    if (button is { } b2)
                    {
                        AddButton(CellActions, b2.Label, action, b2.Style);
                    }
                }
            }
        }

        // Таблица аренды; строка, которая действует сейчас, подсвечена.
        private void FillRentTable(int index, BoardCell cell, CellSnapshot? state)
        {
            RentTable.ColumnDefinitions.Add(new ColumnDefinition());
            RentTable.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rows = new List<(string Label, string Value)>();
            int active = -1;
            var owner = state?.OwnerId;
            var group = Enumerable.Range(0, EventText.Cells.Count).Where(i => EventText.Cells[i].Type == cell.Type).ToList();
            int ownedInGroup = owner is null ? 0 : group.Count(i => Snapshot?.Cells[i].OwnerId == owner);

            switch (cell.Type)
            {
                case CellType.GasStation:
                    for (int count = 1; count < GameRules.GasStationRent.Count; count++)
                    {
                        rows.Add(($"{count} АЗС у владельца", GameManager.Format(GameRules.GasStationRent[count])));
                    }
                    active = ownedInGroup - 1;
                    break;
                case CellType.Logistics:
                    rows.Add(("Одна компания", $"кубики × {GameRules.LogisticsSingle}"));
                    rows.Add(("Обе компании", $"кубики × {GameRules.LogisticsBoth}"));
                    active = ownedInGroup - 1;
                    break;
                default:
                    int rent = GameRules.BaseRent(cell);
                    rows.Add(("Аренда", GameManager.Format(rent)));
                    rows.Add(("Вся группа", GameManager.Format(rent * GameRules.MonopolyMultiplier)));
                    string[] names = { "", "1 филиал", "2 филиала", "3 филиала", "4 филиала", "Головной офис" };
                    for (int level = 1; level <= GameRules.HeadOfficeLevel; level++)
                    {
                        rows.Add((names[level], GameManager.Format(rent * GameRules.LevelMultipliers[level])));
                    }
                    if (owner is not null)
                    {
                        int level = state!.Level;
                        active = level > 0 ? level + 1 : ownedInGroup == group.Count ? 1 : 0;
                    }
                    break;
            }
            if (state is { IsMortgaged: true })
            {
                active = -1;
            }

            for (int r = 0; r < rows.Count; r++)
            {
                RentTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                bool on = r == active;
                var brush = on ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextBrush");
                var label = new TextBlock { Text = rows[r].Label, FontSize = 17, Foreground = brush, FontWeight = on ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(0, 1, 0, 1) };
                var value = new TextBlock { Text = rows[r].Value, FontSize = 17, Foreground = brush, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 1, 0, 1) };
                Grid.SetRow(label, r);
                Grid.SetRow(value, r);
                Grid.SetColumn(value, 1);
                RentTable.Children.Add(label);
                RentTable.Children.Add(value);
            }

            RentTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string costs = cell.IsBuildable
                ? $"Филиал {GameManager.Format(cell.BranchCost)} (продажа {GameManager.Format(cell.BranchSaleValue)}) · залог {GameManager.Format(cell.MortgageValue)} · выкуп {GameManager.Format(cell.RedeemCost)}"
                : $"Залог {GameManager.Format(cell.MortgageValue)} · выкуп {GameManager.Format(cell.RedeemCost)}";
            var footer = new TextBlock { Text = costs, FontSize = 15, Foreground = (Brush)FindResource("MutedTextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(footer, rows.Count);
            Grid.SetColumnSpan(footer, 2);
            RentTable.Children.Add(footer);
        }

        private static string DescribeSpecial(CellType type) => type switch
        {
            CellType.Start => $"Проход или попадание — +{GameManager.Format(GameRules.StartBonus)}.",
            CellType.Jail => $"Здесь просто в гостях. В тюрьму попадают по карточке «Шанса» или за три дубля подряд. Залог — {GameManager.Format(GameRules.BailAmount)}.",
            CellType.Casino => "Ставка 50–300 грн сразу после попадания: 50% — проигрыш, 10% — возврат, 35% — ×2, 5% — ×3.",
            CellType.Rest => "Пропуск следующего хода.",
            CellType.Chance => "Карточка из колоды «Шанса»: деньги, перемещение, тюрьма или выход из неё.",
            _ => "",
        };

        // --- Нижние кнопки ---

        private async void Trade_Click(object sender, RoutedEventArgs e)
        {
            if (Snapshot is null)
            {
                return;
            }
            var window = new TradeWindow(Snapshot, MyId, gameManager.PlayerColor) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true && window.Proposal is { } proposal)
            {
                await SendAsync(proposal);
            }
        }

        private void Sound_Click(object sender, RoutedEventArgs e)
        {
            sounds.Enabled = !sounds.Enabled;
            settings.SoundOn = sounds.Enabled;
            settings.Save();
            UpdateSoundButton();
        }

        private void UpdateSoundButton()
        {
            SoundButton.Content = sounds.Enabled ? "🔊  Звук" : "🔈  Без звука";
        }

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(Window.GetWindow(this)!, "Выйти из партии? Вернуться в неё пока нельзя.",
                "Монополия", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                await leave();
            }
        }
    }
}
