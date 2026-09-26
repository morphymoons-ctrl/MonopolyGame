using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.App
{
    // Рисует поле, фишки, игроков и журнал по данным от хоста и проигрывает события с анимацией.
    // Правил здесь нет — они в Monopoly.Core.
    public class GameManager
    {

        private readonly IReadOnlyList<BoardCell> board = EventText.Cells;
        private readonly GameStartInfo startInfo;
        private readonly ListBox actionLog;
        private readonly Canvas boardCanvas;
        private readonly Canvas tokenCanvas;
        private readonly StackPanel playersPanel;
        private readonly Dictionary<int, Ellipse> tokens = new();
        // Где фишка нарисована сейчас (во время анимации отстаёт от снимка).
        private readonly Dictionary<int, int> shownPositions = new();

        private const int topCount = 9;
        private const int rightCount = 8;
        private const int bottomCount = 8;

        // Последнее состояние от хоста; null — ещё не пришло.
        public GameSnapshot? Snapshot { get; private set; }
        // Клетка, выбранная щелчком; её данные и кнопки показывает карточка компании.
        public int? SelectedCell { get; private set; }

        // Подключение игроков и кого ждёт игра — из последнего обновления хоста.
        private IReadOnlyList<SeatStatus> seats = Array.Empty<SeatStatus>();
        private DateTime seatsReceived = DateTime.UtcNow;
        private IReadOnlyList<int> awaited = Array.Empty<int>();

        public event Action<int>? CellClicked;

        public GameManager(ListBox log, Canvas canvas, Canvas tokenLayer, StackPanel players, GameStartInfo info)
        {
            actionLog = log;
            boardCanvas = canvas;
            tokenCanvas = tokenLayer;
            playersPanel = players;
            startInfo = info;
        }

        public int MyPlayerId => startInfo.MyPlayerId;

        public static string Format(int amount) => GameRules.Money(amount);

        // Крупная сумма (цена на клетке, баланс) — нейтральным шрифтом GameFonts.Money.
        private static void FillMoney(TextBlock block, int amount)
        {
            block.Text = GameRules.Money(amount);
            block.FontFamily = GameFonts.Money;
            block.FontWeight = FontWeights.Normal;
        }

        public Brush PlayerColor(int playerId) =>
            PlayerPalette.Get(startInfo.ColorByPlayerId.TryGetValue(playerId, out int color) ? color : playerId);

        // Проигрывает события по одному: строка в журнал, звук, анимация кубиков и фишек.
        public async Task PlayAsync(GameUpdate update, DiceView die1, DiceView die2, Sounds sounds)
        {
            // Имена нужны для журнала уже на первых событиях.
            Snapshot ??= update.Snapshot;
            bool coin = false;
            foreach (var e in update.Events)
            {
                AddLog(EventText.Describe(e, update.Snapshot));
                switch (e)
                {
                    case DiceRolled d:
                        sounds.Dice();
                        await Task.WhenAll(die1.RollAsync(d.Die1, 0), die2.RollAsync(d.Die2, 3));
                        break;
                    case PlayerMoved m:
                        await AnimateMoveAsync(m.PlayerId, m.From, m.To);
                        break;
                    case SentToJail j:
                        shownPositions[j.PlayerId] = GameRules.JailCell;
                        PlaceToken(j.PlayerId, GameRules.JailCell, 0, 250);
                        await Task.Delay(250);
                        break;
                    case PassedStart or ReceivedFromBank or PropertyBought or AuctionWon or RentPaid or PaidToPlayer or DebtPaid:
                        coin = true;
                        break;
                    case TurnStarted t when t.PlayerId == MyPlayerId:
                        sounds.Turn();
                        break;
                    case GameOver:
                        sounds.Win();
                        break;
                }
            }
            if (coin)
            {
                sounds.Coin();
            }

            Snapshot = update.Snapshot;
            DrawBoard();
            LayoutTokens(animationMs: 200);
        }

        // Возвращение в партию: вся история — сразу в журнал, без анимации.
        public void LoadHistory(GameUpdate update)
        {
            Snapshot = update.Snapshot;
            foreach (var e in update.Events)
            {
                AddLog(EventText.Describe(e, update.Snapshot));
            }
            DrawBoard();
            LayoutTokens(animationMs: 0);
        }

        public void SetStatus(IReadOnlyList<SeatStatus>? seatStatuses, IReadOnlyList<int>? awaitedIds)
        {
            seats = seatStatuses ?? Array.Empty<SeatStatus>();
            seatsReceived = DateTime.UtcNow;
            awaited = awaitedIds ?? Array.Empty<int>();
            DrawPlayersPanel();
        }

        // Раз в секунду — чтобы шёл отсчёт до передачи места боту.
        public void RefreshPlayers()
        {
            if (seats.Any(s => s.Connection == SeatConnection.Offline))
            {
                DrawPlayersPanel();
            }
        }

        public void Select(int cellIndex)
        {
            SelectedCell = cellIndex;
            DrawBoard();
        }

        // Крайние ряды глубже, чем шаг клетки вдоль края, — как на настоящей доске: клетки крупнее,
        // центр поля меньше. Верхний и нижний ряды — высотой RowDepth, левый и правый столбцы — шириной ColumnDepth.
        // Углы — прямоугольники ColumnDepth × RowDepth, между углами — по 7 клеток.
        public const double RowDepth = 150;
        public const double ColumnDepth = 190;

        private double AlongX => (boardCanvas.Width - 2 * ColumnDepth) / (topCount - 2);
        private double AlongY => (boardCanvas.Height - 2 * RowDepth) / (topCount - 2);
        // Единица для размеров шрифтов и значков: самый узкий шаг клетки.
        private double Unit => Math.Min(AlongX, AlongY);

        // Прямоугольник клетки: по часовой стрелке от «Старта» в левом верхнем углу.
        private Rect CellRect(int i)
        {
            double right = boardCanvas.Width - ColumnDepth, bottom = boardCanvas.Height - RowDepth;
            int side = topCount - 1;
            if (i < side)
            {
                return i == 0
                    ? new Rect(0, 0, ColumnDepth, RowDepth)
                    : new Rect(ColumnDepth + (i - 1) * AlongX, 0, AlongX, RowDepth);
            }
            if (i < 2 * side)
            {
                int k = i - side;
                return k == 0
                    ? new Rect(right, 0, ColumnDepth, RowDepth)
                    : new Rect(right, RowDepth + (k - 1) * AlongY, ColumnDepth, AlongY);
            }
            if (i < 3 * side)
            {
                int k = i - 2 * side;
                return k == 0
                    ? new Rect(right, bottom, ColumnDepth, RowDepth)
                    : new Rect(right - k * AlongX, bottom, AlongX, RowDepth);
            }
            int m = i - 3 * side;
            return m == 0
                ? new Rect(0, bottom, ColumnDepth, RowDepth)
                : new Rect(0, bottom - m * AlongY, ColumnDepth, AlongY);
        }

        public void DrawBoard()
        {
            boardCanvas.Children.Clear();
            double u = Unit;
            var line = (Brush)Application.Current.Resources["BoardLineBrush"];
            var paper = (Brush)Application.Current.Resources["BoardBrush"];
            var text = (Brush)Application.Current.Resources["BoardTextBrush"];
            var muted = (Brush)Application.Current.Resources["BoardMutedBrush"];

            for (int i = 0; i < board.Count; i++)
            {
                var r = CellRect(i);
                var cell = board[i];
                var state = Snapshot?.Cells[i];

                Place(new Rectangle
                {
                    Width = r.Width,
                    Height = r.Height,
                    Stroke = line,
                    StrokeThickness = 1,
                    Fill = state?.IsMortgaged == true ? PlayerPalette.Make("#DAD7CF") : cell.IsPurchasable ? paper : SpecialBackground(cell.Type)
                }, r.X, r.Y);

                if (cell.IsPurchasable)
                {
                    DrawCompany(cell, state, i, r, u, text, muted);
                }
                else
                {
                    AddText(GroupPalette.Icon(cell.Type) ?? "", r.X, r.Y + r.Height * 0.12, r.Width, u * 0.34, FontWeights.Bold, text, GameFonts.Icons);
                    AddText(cell.Name, r.X, r.Y + r.Height * 0.52, r.Width, u * 0.17, FontWeights.Bold, text);
                }

                if (SelectedCell == i)
                {
                    Place(new Rectangle
                    {
                        Width = r.Width - 2,
                        Height = r.Height - 2,
                        Stroke = (Brush)Application.Current.Resources["AccentPressedBrush"],
                        StrokeThickness = 5,
                        IsHitTestVisible = false
                    }, r.X + 1, r.Y + 1);
                }

                // Щелчок по клетке — прозрачная кнопка поверх неё (стиль CellButton в GameScreen.xaml).
                int index = i;
                var hit = new Button { Width = r.Width, Height = r.Height, Style = (Style)boardCanvas.FindResource("CellButton") };
                AutomationProperties.SetName(hit, cell.Name);
                // На клетке может быть логотип — название видно при наведении.
                hit.ToolTip = cell.Name;
                AutomationProperties.SetAutomationId(hit, $"Cell{i}");
                hit.Click += (_, _) => CellClicked?.Invoke(index);
                Place(hit, r.X, r.Y);
            }
            DrawPlayersPanel();
        }

        // Полоса группы у боковых клеток — вертикальная, у внешнего края поля:
        // у левого столбца — слева, у правого — справа; у верхнего и нижнего рядов — сверху.
        // Content — остальная часть клетки: название, цена, фишки.
        private (Rect Bar, Rect Content) CompanyLayout(int i, Rect r)
        {
            double bar = Unit * 0.22;
            int side = topCount - 1;
            bool rightColumn = i > side && i < 2 * side;
            bool leftColumn = i > 3 * side;
            if (leftColumn)
            {
                return (new Rect(r.X + 1, r.Y + 1, bar, r.Height - 2), new Rect(r.X + bar, r.Y, r.Width - bar, r.Height));
            }
            if (rightColumn)
            {
                return (new Rect(r.Right - bar - 1, r.Y + 1, bar, r.Height - 2), new Rect(r.X, r.Y, r.Width - bar, r.Height));
            }
            return (new Rect(r.X + 1, r.Y + 1, r.Width - 2, bar), new Rect(r.X, r.Y + bar, r.Width, r.Height - bar));
        }

        // Компания: полоса группы, название, цена или филиалы, полоса владельца снизу.
        private void DrawCompany(BoardCell cell, CellSnapshot? state, int index, Rect r, double u, Brush text, Brush muted)
        {
            var (bar, c) = CompanyLayout(index, r);
            Place(new Rectangle { Width = bar.Width, Height = bar.Height, Fill = GroupPalette.Get(cell.Type), IsHitTestVisible = false }, bar.X, bar.Y);
            if (GroupPalette.Icon(cell.Type) is { } icon)
            {
                double iconSize = u * 0.16;
                AddText(icon, bar.X, bar.Y + (bar.Height - iconSize * 1.35) / 2, bar.Width, iconSize, FontWeights.Bold, GroupPalette.IconColor(cell.Type), GameFonts.Icons);
            }

            // Логотип (или название, если логотипа нет) и цена под ним — одной группой по центру клетки.
            // Центрируем в месте над рядом фишек и полосой владельца: они внизу клетки.
            double areaTop = c.Y + u * 0.04;
            double areaBottom = r.Bottom - u * 0.32;
            double priceHeight = u * 0.19, innerGap = u * 0.05;
            double detail;
            if (Logos.For(index) is { } logo)
            {
                double logoHeight = Math.Min(u * 0.6, areaBottom - areaTop - priceHeight - innerGap);
                double top = areaTop + (areaBottom - areaTop - logoHeight - innerGap - priceHeight) / 2;
                var image = new Image
                {
                    Source = logo,
                    Width = c.Width - u * 0.04,
                    Height = logoHeight,
                    Stretch = Stretch.Uniform,
                    Opacity = state?.IsMortgaged == true ? 0.4 : 1,
                    IsHitTestVisible = false
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                Place(image, c.X + u * 0.02, top);
                detail = top + logoHeight + innerGap;
            }
            else
            {
                double nameHeight = u * 0.2;
                double top = areaTop + (areaBottom - areaTop - nameHeight - innerGap - priceHeight) / 2;
                AddText(cell.Name, c.X + 2, top, c.Width - 4, u * 0.16, FontWeights.Bold, text);
                detail = top + nameHeight + innerGap;
            }
            if (state?.IsMortgaged == true)
            {
                AddText("ЗАСТАВА", c.X, detail, c.Width, u * 0.14, FontWeights.Black, (Brush)Application.Current.Resources["DangerBrush"]);
            }
            else if (state is { Level: > 0 })
            {
                DrawBranches(state.Level, c, detail, u);
            }
            else if (state?.OwnerId is null)
            {
                var price = new TextBlock { Width = c.Width, TextAlignment = TextAlignment.Center, FontSize = u * 0.16, Foreground = muted, IsHitTestVisible = false };
                FillMoney(price, cell.Price);
                Place(price, c.X, detail);
            }

            if (state?.OwnerId is int owner)
            {
                Place(new Rectangle { Width = c.Width - 2, Height = u * 0.1, Fill = PlayerColor(owner), IsHitTestVisible = false },
                    c.X + 1, r.Bottom - u * 0.1 - 1);
            }
        }

        // Филиалы — зелёные домики, головной офис — красное здание.
        private void DrawBranches(int level, Rect r, double top, double u)
        {
            if (level == GameRules.HeadOfficeLevel)
            {
                var office = new Border
                {
                    Width = u * 0.6,
                    Height = u * 0.2,
                    CornerRadius = new CornerRadius(3),
                    Background = PlayerPalette.Make("#C62828"),
                    Child = new TextBlock { Text = "ОФІС", Foreground = Brushes.White, FontSize = u * 0.12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                    IsHitTestVisible = false
                };
                Place(office, r.X + (r.Width - u * 0.6) / 2, top);
                return;
            }
            double house = u * 0.16, gap = u * 0.05;
            double start = r.X + (r.Width - level * house - (level - 1) * gap) / 2;
            for (int k = 0; k < level; k++)
            {
                Place(new Rectangle
                {
                    Width = house,
                    Height = house,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = PlayerPalette.Make("#2E9E5B"),
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                }, start + k * (house + gap), top + u * 0.02);
            }
        }

        private static Brush SpecialBackground(CellType type) => type switch
        {
            CellType.Start => PlayerPalette.Make("#DDF3E4"),
            CellType.Jail => PlayerPalette.Make("#EFE3D3"),
            CellType.Casino => PlayerPalette.Make("#F7DDEA"),
            CellType.Rest => PlayerPalette.Make("#DDEBFA"),
            CellType.Chance => PlayerPalette.Make("#FFF1C7"),
            _ => Brushes.White,
        };

        private void AddText(string text, double x, double y, double width, double fontSize, FontWeight weight, Brush color, FontFamily? font = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                Width = width,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = color,
                FontSize = fontSize,
                FontWeight = weight,
                IsHitTestVisible = false
            };
            if (font is not null)
            {
                tb.FontFamily = font;
            }
            Place(tb, x, y);
        }

        private void Place(UIElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            boardCanvas.Children.Add(element);
        }

        // --- Фишки ---

        private Ellipse Token(int playerId)
        {
            if (!tokens.TryGetValue(playerId, out var token))
            {
                double d = Unit * 0.22;
                token = new Ellipse
                {
                    Width = d,
                    Height = d,
                    Fill = PlayerColor(playerId),
                    Stroke = Brushes.White,
                    StrokeThickness = 2.5,
                    Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Opacity = 0.5 }
                };
                tokens[playerId] = token;
                tokenCanvas.Children.Add(token);
                var start = CellRect(0);
                Canvas.SetLeft(token, start.X);
                Canvas.SetTop(token, start.Y);
            }
            return token;
        }

        // slot — место фишки на клетке, чтобы несколько фишек стояли рядом.
        private void PlaceToken(int playerId, int cell, int slot, int animationMs)
        {
            double u = Unit;
            var r = board[cell].IsPurchasable ? CompanyLayout(cell, CellRect(cell)).Content : CellRect(cell);
            double x = r.X + u * 0.06 + slot * u * 0.24;
            double y = r.Bottom - u * 0.3;
            var token = Token(playerId);
            if (animationMs <= 0)
            {
                token.BeginAnimation(Canvas.LeftProperty, null);
                token.BeginAnimation(Canvas.TopProperty, null);
                Canvas.SetLeft(token, x);
                Canvas.SetTop(token, y);
                return;
            }
            var duration = TimeSpan.FromMilliseconds(animationMs);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            token.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(x, duration) { EasingFunction = ease });
            token.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(y, duration) { EasingFunction = ease });
        }

        // Фишка идёт по клеткам. Назад — только на «Поездке Укрзализныцей»; длинные переезды по карточкам — быстрее.
        private async Task AnimateMoveAsync(int playerId, int from, int to)
        {
            int n = board.Count;
            int forward = (to - from + n) % n;
            int backward = (from - to + n) % n;
            bool back = forward > 12 && backward <= ChanceCards.TrainStepsBack;
            int steps = back ? backward : forward;
            int stepMs = steps <= 12 ? 130 : Math.Max(45, 1500 / Math.Max(1, steps));
            for (int s = 1; s <= steps; s++)
            {
                int cell = ((from + (back ? -s : s)) % n + n) % n;
                PlaceToken(playerId, cell, 0, (int)(stepMs * 0.85));
                await Task.Delay(stepMs);
            }
            shownPositions[playerId] = to;
        }

        private void LayoutTokens(int animationMs)
        {
            if (Snapshot is null)
            {
                return;
            }
            foreach (var player in Snapshot.Players)
            {
                if (player.IsBankrupt)
                {
                    Token(player.Id).Visibility = Visibility.Collapsed;
                    continue;
                }
                shownPositions[player.Id] = player.Position;
            }
            foreach (var group in Snapshot.Players.Where(p => !p.IsBankrupt).GroupBy(p => p.Position))
            {
                int slot = 0;
                foreach (var player in group)
                {
                    var token = Token(player.Id);
                    token.StrokeThickness = player.Id == Snapshot.CurrentPlayerId ? 4 : 2.5;
                    PlaceToken(player.Id, group.Key, slot++, animationMs);
                }
            }
        }

        // --- Игроки и журнал ---

        private void DrawPlayersPanel()
        {
            playersPanel.Children.Clear();
            if (Snapshot is null)
            {
                return;
            }
            foreach (var player in Snapshot.Players)
            {
                bool isCurrent = player.Id == Snapshot.CurrentPlayerId && Snapshot.WinnerId is null;
                var notes = new List<string>();
                if (player.Id == MyPlayerId)
                {
                    notes.Add("ви");
                }
                if (player.IsBankrupt)
                {
                    notes.Add("вибув");
                }
                else if (seats.FirstOrDefault(s => s.PlayerId == player.Id) is { } seat)
                {
                    if (seat.Connection == SeatConnection.Bot)
                    {
                        notes.Add("грає бот");
                    }
                    else if (seat.Connection == SeatConnection.Offline)
                    {
                        int left = Math.Max(0, (seat.SecondsToBot ?? 0) - (int)(DateTime.UtcNow - seatsReceived).TotalSeconds);
                        notes.Add($"не в мережі · бот через {left / 60}:{left % 60:00}");
                    }
                }
                if (!isCurrent && awaited.Contains(player.Id) && Snapshot.WinnerId is null)
                {
                    notes.Add("чекаємо відповіді");
                }
                if (player.IsInJail)
                {
                    notes.Add("у пєтушатні");
                }
                if (player.IsResting)
                {
                    notes.Add("чілить");
                }
                int companies = Snapshot.Cells.Count(c => c.OwnerId == player.Id);
                if (companies > 0)
                {
                    notes.Add($"компаній: {companies}");
                }
                if (player.JailCards > 0)
                {
                    notes.Add($"карток «Вийти з пєтушатні»: {player.JailCards}");
                }

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var dot = new Ellipse { Width = 30, Height = 30, Fill = PlayerColor(player.Id), Stroke = Brushes.White, StrokeThickness = 2, Margin = new Thickness(0, 0, 14, 0) };
                grid.Children.Add(dot);

                var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                names.Children.Add(new TextBlock
                {
                    Text = player.Name,
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    TextDecorations = player.IsBankrupt ? TextDecorations.Strikethrough : null
                });
                if (notes.Count > 0)
                {
                    names.Children.Add(new TextBlock
                    {
                        Text = string.Join(" · ", notes),
                        FontSize = 15,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                    });
                }
                Grid.SetColumn(names, 1);
                grid.Children.Add(names);

                var balance = new TextBlock
                {
                    FontSize = 25,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isCurrent ? (Brush)Application.Current.Resources["AccentBrush"] : (Brush)Application.Current.Resources["TextBrush"]
                };
                FillMoney(balance, player.Balance);
                Grid.SetColumn(balance, 2);
                grid.Children.Add(balance);

                playersPanel.Children.Add(new Border
                {
                    Child = grid,
                    Background = (Brush)Application.Current.Resources[isCurrent ? "SurfaceRaisedBrush" : "SurfaceBrush"],
                    BorderBrush = isCurrent ? (Brush)Application.Current.Resources["AccentBrush"] : (Brush)Application.Current.Resources["SurfaceBorderBrush"],
                    BorderThickness = new Thickness(isCurrent ? 2 : 1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(14, 10, 16, 10),
                    Margin = new Thickness(0, 0, 0, 8),
                    Opacity = player.IsBankrupt ? 0.45 : 1
                });
            }
        }

        // Строка журнала; имена игроков — жирным, цветом их фишки.
        public void AddLog(string text)
        {
            var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
            foreach (var (part, playerId) in SplitNames(text))
            {
                block.Inlines.Add(playerId is int id
                    ? new Run(part) { Foreground = PlayerLogColor(id), FontWeight = FontWeights.Bold }
                    : new Run(part));
            }
            // Отдельный элемент, чтобы прокрутка шла к нему, а не к первой такой же строке.
            var item = new ListBoxItem { Content = block };
            actionLog.Items.Add(item);
            actionLog.ScrollIntoView(item);
        }

        // Куски строки: имя с Id игрока или обычный текст.
        // В событиях движка имена помечены (EventText.TagName); в сообщениях хоста и ошибках — ищем по списку игроков.
        private IEnumerable<(string Part, int? PlayerId)> SplitNames(string text)
        {
            var tagged = new Regex($"{EventText.NameStart}(\\d+){EventText.NameSplit}(.*?){EventText.NameEnd}");
            int position = 0;
            foreach (Match match in tagged.Matches(text))
            {
                foreach (var piece in FindNames(text[position..match.Index]))
                    yield return piece;
                yield return (match.Groups[2].Value, int.Parse(match.Groups[1].Value));
                position = match.Index + match.Length;
            }
            foreach (var piece in FindNames(text[position..]))
                yield return piece;
        }

        private IEnumerable<(string Part, int? PlayerId)> FindNames(string text)
        {
            var players = Snapshot?.Players.OrderByDescending(p => p.Name.Length).ToList();
            if (players is null || players.Count == 0 || text.Length == 0)
            {
                yield return (text, null);
                yield break;
            }
            // Имя — отдельным словом: «Бот 1» не совпадёт с «Бот 10», «банк» — с «банку».
            var names = new Regex($"(?<!\\w)({string.Join("|", players.Select(p => Regex.Escape(p.Name)))})(?!\\w)");
            int position = 0;
            foreach (Match match in names.Matches(text))
            {
                if (match.Index > position)
                    yield return (text[position..match.Index], null);
                yield return (match.Value, players.First(p => p.Name == match.Value).Id);
                position = match.Index + match.Length;
            }
            if (position < text.Length)
                yield return (text[position..], null);
        }

        // Цвет фишки, чуть светлее: журнал тёмный, синий и фиолетовый на нём иначе плохо видно.
        private Brush PlayerLogColor(int playerId) =>
            PlayerPalette.GetLight(startInfo.ColorByPlayerId.TryGetValue(playerId, out int color) ? color : playerId);
    }
}
