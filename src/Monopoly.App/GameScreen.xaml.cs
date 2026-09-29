using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        // Итоги партии уже открывались сами после победы (§16).
        private bool summaryShown;
        // Таймер хода: до какого момента ждут решения и от кого.
        private DateTime? deadline;
        private IReadOnlyList<int> awaitedIds = Array.Empty<int>();
        // Длительность партии от хоста и когда она пришла: дальше часы идут сами.
        private GameDuration? duration;
        private DateTime durationReceived;
        private readonly System.Windows.Threading.DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };

        public GameScreen(GameClient client, GameStartInfo info, UserSettings settings, Func<Task> leave)
        {
            // Доска партии (§15) — до всего остального: поле, журнал и тексты берут названия с неё.
            EventText.UseTheme(info.Theme);
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

            clock.Tick += (_, _) =>
            {
                ShowTimer();
                ShowDuration();
                gameManager.RefreshPlayers();
            };
            Loaded += (_, _) => clock.Start();
            Unloaded += (_, _) => clock.Stop();
        }

        public void ShowReconnecting(bool reconnecting)
        {
            ReconnectOverlay.Visibility = reconnecting ? Visibility.Visible : Visibility.Collapsed;
        }

        // Сколько идёт партия; после победы — сколько она шла.
        private void ShowDuration()
        {
            if (duration is null)
            {
                DurationText.Text = "";
                return;
            }
            int seconds = duration.Seconds + (duration.Running ? (int)(DateTime.UtcNow - durationReceived).TotalSeconds : 0);
            string time = $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}";
            DurationText.Text = duration.Running ? $"Партія триває {time}" : $"Партія тривала {time}";
        }

        // Отсчёт считается от момента, когда пришло обновление: часы у компьютеров могут не совпадать.
        private void ShowTimer()
        {
            if (deadline is not DateTime until || Snapshot?.WinnerId is not null)
            {
                TimerText.Text = "";
                return;
            }
            int left = Math.Max(0, (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds));
            TimerText.Text = $"⏱ {left / 60}:{left % 60:00}";
            bool mine = awaitedIds.Contains(MyId);
            TimerText.Foreground = mine && left <= 15
                ? (Brush)FindResource("DangerBrush")
                : (Brush)FindResource("BoardMutedBrush");
            TimerText.ToolTip = mine ? "Коли час вийде, гра зробить хід за вас" : null;
        }

        private int MyId => gameManager.MyPlayerId;
        private GameSnapshot? Snapshot => gameManager.Snapshot;
        private bool Busy => sending || playing;

        public void Apply(GameUpdate update)
        {
            deadline = update.Timer is { } timer ? DateTime.UtcNow.AddSeconds(timer.SecondsLeft) : null;
            awaitedIds = update.Timer?.AwaitedIds ?? Array.Empty<int>();
            gameManager.SetStatus(update.Seats, awaitedIds);
            if (update.Duration is not null)
            {
                duration = update.Duration;
                durationReceived = DateTime.UtcNow;
            }
            ShowTimer();
            ShowDuration();
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
                if (update.IsResync)
                {
                    gameManager.LoadHistory(update);
                }
                else
                {
                    await gameManager.PlayAsync(update, die1, die2, sounds);
                }
                available = update.AvailableActions;
                sending = false;
            }
            playing = false;
            Refresh();
            // Партия закончилась — итоги открываются сами, один раз (§16).
            if (Snapshot?.WinnerId is not null && !summaryShown)
            {
                summaryShown = true;
                ShowStats();
            }
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
                ContextTitle.Text = "Роздаємо фішки…";
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
            string Target(int? creditor) => creditor is int c ? $"гравцю {Name(c)}" : "банку";
            var danger = (Brush)FindResource("DangerBrush");
            var blue = (Brush)FindResource("BlueBrush");
            var me = s.FindPlayer(MyId);
            bool myTurn = s.CurrentPlayerId == MyId;

            switch (s.Phase)
            {
                case TurnPhase.GameOver:
                    return ($"Перемога: {Name(s.WinnerId!.Value)}!", "Партію закінчено. Вийдіть у меню, щоб зібрати нову.", (Brush)FindResource("AccentBrush"));
                case TurnPhase.Debt when s.Debt!.DebtorId == MyId:
                    return ("Бракує грошей",
                        $"Борг {GameManager.Format(s.Debt.Amount)} {Target(s.Debt.CreditorId)}. Натисніть на свою компанію на полі, щоб продати {EventText.Terms.Branches} або закласти її, чи запропонуйте обмін іншому гравцю. Борг спишеться сам, щойно вистачить грошей.",
                        danger);
                case TurnPhase.Debt:
                    return ($"{Name(s.Debt!.DebtorId)} шукає гроші", $"Борг {GameManager.Format(s.Debt.Amount)} {Target(s.Debt.CreditorId)}. Чекаємо.", danger);
                case TurnPhase.Auction:
                    var auction = s.Auction!;
                    var lot = EventText.Cells[auction.CellIndex];
                    string bid = auction.LeaderId is int leader
                        ? $"Ставка {GameManager.Format(auction.HighBid)} — {Name(leader)}."
                        : $"Ставок поки немає. Перша — від {GameManager.Format(auction.MinBid)}.";
                    var passed = auction.PassedIds.Select(Name).ToList();
                    string passedText = passed.Count > 0 ? $" Спасували: {string.Join(", ", passed)}." : "";
                    string finderText = auction.FinderId is int finder ? $" {Name(finder)} отримає 30% від продажу, якщо виграє інший." : "";
                    return ($"Аукціон: «{lot.Name}»", $"Ціна компанії {GameManager.Format(lot.Price)}. {bid}{passedText}{finderText}", GroupPalette.Get(lot.Type));
                case TurnPhase.TradeOffer when s.Trade!.ToId == MyId:
                    return ("Вам пропонують обмін", $"{Name(s.Trade.FromId)} {EventText.DescribeOffer(s.Trade, s)}", blue);
                case TurnPhase.TradeOffer when s.Trade!.FromId == MyId:
                    return ($"Чекаємо відповіді: {Name(s.Trade.ToId)}", $"Ви {EventText.DescribeOffer(s.Trade, s)}", blue);
                case TurnPhase.TradeOffer:
                    return ("Триває обмін", $"{Name(s.Trade!.FromId)} → {Name(s.Trade.ToId)}: {EventText.DescribeOffer(s.Trade, s)}", blue);
                case TurnPhase.BuyDecision:
                    var cell = EventText.Cells[s.PendingPurchase!.Value];
                    return myTurn
                        ? ($"«{cell.Name}» вільна", $"{EventText.GroupName(cell.Type)} · {GameManager.Format(cell.Price)}. Купіть її або відмовтеся — тоді почнеться аукціон.", GroupPalette.Get(cell.Type))
                        : ($"{Name(s.CurrentPlayerId)} вирішує", $"Чи купувати «{cell.Name}» за {GameManager.Format(cell.Price)}.", GroupPalette.Get(cell.Type));
            }

            if (!myTurn)
            {
                var current = s.FindPlayer(s.CurrentPlayerId)!;
                return ($"Ходить {current.Name}", current.IsInJail ? $"Сидить {EventText.Words.InJail} — наступний хід пропустить." : "", null);
            }
            if (me?.IsInJail == true && s.Phase == TurnPhase.Manage)
            {
                return ($"Ви {EventText.Words.InJail}", $"Наступний хід ви пропустите. Можна будувати {EventText.Terms.Branches}, закладати компанії й пропонувати обмін, потім — завершити хід.", null);
            }
            if (s.Phase == TurnPhase.AwaitingRoll)
            {
                return s.LastRoll is null
                    ? ("Ваш хід", $"Киньте кубики. До кидка можна будувати {EventText.Terms.Branches}, закладати компанії та пропонувати обмін.", null)
                    : ("Дубль! Кидайте ще раз", "", null);
            }
            return s.CasinoAvailable
                ? (EventText.Cells[16].Name, $"Ставка {EventText.CasinoRange}: 50% — програш, 10% — повернення, 35% — ×2, 5% — ×3. Можна не грати — просто завершіть хід.", PlayerPalette.Make("#E0569B"))
                : ("Ваш хід", $"Можна будувати {EventText.Terms.Branches}, закладати компанії та пропонувати обмін. Потім завершіть хід.", null);
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
                // Минимальная ставка и две побольше: +40 000 и +90 000 к ней.
                var amounts = new[] { minBid.Amount, minBid.Amount + GameRules.AuctionStep * 4, minBid.Amount + GameRules.AuctionStep * 9 }
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

            // После победы — итоги партии (§16).
            if (s.Phase == TurnPhase.GameOver)
            {
                var summary = new Button { Content = "Підсумки партії", FontSize = 19, Margin = new Thickness(0, 0, 10, 10), Style = (Style)FindResource("PrimaryButton") };
                summary.Click += (_, _) => ShowStats();
                ActionsPanel.Children.Add(summary);
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
            string status = state is { IsMortgaged: true } ? $" · закладена, оренди немає · {EventText.MortgageLeft(state.MortgageTurnsLeft)}" : "";
            CellInfo.Text = $"{EventText.GroupName(cell.Type)} · ціна {GameManager.Format(cell.Price)} · власник: {owner}{status}";
            FillRentTable(index, cell, state);

            if (!Busy)
            {
                foreach (var action in available)
                {
                    (string Label, string Style)? button = action switch
                    {
                        BuildBranch b when b.CellIndex == index => (state?.Level == GameRules.HeadOfficeLevel - 1
                            ? $"{GameTerms.Capital(EventText.Terms.Office)} · {GameManager.Format(cell.BranchCost)}"
                            : $"{GameTerms.Capital(EventText.Terms.Branch)} · {GameManager.Format(cell.BranchCost)}", "PrimaryButton"),
                        SellBranch s when s.CellIndex == index => ($"Продати {EventText.Terms.BranchAccusative} · +{GameManager.Format(cell.BranchSaleValue)}", "SecondaryButton"),
                        MortgageCompany m when m.CellIndex == index => ($"Закласти · +{GameManager.Format(cell.MortgageValue)}", "SecondaryButton"),
                        RedeemCompany r when r.CellIndex == index => ($"Викупити · {GameManager.Format(cell.RedeemCost)}", "PrimaryButton"),
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
            // Работающие (незаложенные) компании владельца в группе — от них аренда (§5).
            int ownedInGroup = owner is int ownerId ? GameRules.ActiveInGroup(EventText.Cells, cell.Type, ownerId) : 0;

            switch (cell.Type)
            {
                case CellType.GasStation:
                    for (int count = 1; count < GameRules.GasStationRent.Count; count++)
                    {
                        rows.Add(($"{EventText.GroupName(cell.Type)}: {count}", GameManager.Format(GameRules.GasStationRent[count])));
                    }
                    active = ownedInGroup - 1;
                    break;
                case CellType.Logistics:
                    rows.Add(("Одна компанія", $"кубики × {GameManager.Format(GameRules.LogisticsSingle)}"));
                    rows.Add(("Обидві компанії", $"кубики × {GameManager.Format(GameRules.LogisticsBoth)}"));
                    active = ownedInGroup - 1;
                    break;
                default:
                    int rent = GameRules.BaseRent(cell);
                    rows.Add(("Оренда", GameManager.Format(rent)));
                    rows.Add(("Уся група", GameManager.Format(rent * GameRules.MonopolyMultiplier)));
                    var terms = EventText.Terms;
                    string[] names = { "", $"1 {terms.Branch}", $"2 {terms.Branches}", $"3 {terms.Branches}", $"4 {terms.Branches}", GameTerms.Capital(terms.Office) };
                    for (int level = 1; level <= GameRules.HeadOfficeLevel; level++)
                    {
                        rows.Add((names[level], GameManager.Format(GameRules.LevelRent(cell, level))));
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
                ? $"{GameTerms.Capital(EventText.Terms.Branch)} {GameManager.Format(cell.BranchCost)} (продаж {GameManager.Format(cell.BranchSaleValue)}) · застава {GameManager.Format(cell.MortgageValue)} · викуп {GameManager.Format(cell.RedeemCost)}"
                : $"Застава {GameManager.Format(cell.MortgageValue)} · викуп {GameManager.Format(cell.RedeemCost)}";
            var footer = new TextBlock { Text = costs, FontSize = 15, Foreground = (Brush)FindResource("MutedTextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(footer, rows.Count);
            Grid.SetColumnSpan(footer, 2);
            RentTable.Children.Add(footer);
        }

        private static string DescribeSpecial(CellType type) => type switch
        {
            CellType.Start => $"Прохід або потрапляння — +{GameManager.Format(GameRules.StartBonus)}.",
            CellType.Jail => $"Пропуск наступного ходу — як «{EventText.Cells[20].Name}». Сюди ж ведуть три дублі поспіль і картка «{EventText.Cells[24].Name}». Картка «{EventText.Words.JailCard}» рятує від пропуску сама.",
            CellType.Casino => $"Ставка {EventText.CasinoRange} одразу після потрапляння: 50% — програш, 10% — повернення, 35% — ×2, 5% — ×3.",
            CellType.Rest => "Пропуск наступного ходу.",
            CellType.Chance => $"Картка з колоди «{EventText.Cells[24].Name}»: гроші, переміщення, пропуск ходу або картка, що рятує від нього.",
            _ => "",
        };

        // --- Нижние кнопки ---

        private void Trade_Click(object sender, RoutedEventArgs e)
        {
            if (Snapshot is null)
            {
                return;
            }
            var panel = new TradePanel(Snapshot, MyId, gameManager.PlayerColor);
            panel.Finished += async proposal =>
            {
                CloseTrade();
                if (proposal is not null)
                {
                    await SendAsync(proposal);
                }
            };
            TradeOverlay.Child = panel;
            TradeOverlay.Visibility = Visibility.Visible;
        }

        // Статистика партии — в том же окне поверх поля, что и обмен (§16).
        private void Stats_Click(object sender, RoutedEventArgs e) => ShowStats();

        private void ShowStats()
        {
            if (Snapshot is null)
            {
                return;
            }
            var panel = new StatsPanel(Snapshot, gameManager.PlayerColor, DurationText.Text);
            panel.Closed += CloseTrade;
            TradeOverlay.Child = panel;
            TradeOverlay.Visibility = Visibility.Visible;
        }

        private void CloseTrade()
        {
            TradeOverlay.Child = null;
            TradeOverlay.Visibility = Visibility.Collapsed;
        }

        // Щелчок по затемнению вокруг карточки обмена — отмена.
        private void TradeOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == TradeOverlay)
            {
                CloseTrade();
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
            SoundButton.Content = sounds.Enabled ? "🔊  Звук" : "🔈  Без звуку";
        }

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(Window.GetWindow(this)!, "Вийти з партії? Хост збереже ваше місце, повернутися можна під тим самим ім'ям.",
                "Монополія", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                await leave();
            }
        }
    }
}
