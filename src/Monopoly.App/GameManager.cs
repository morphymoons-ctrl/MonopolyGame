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

        public static string Format(int amount) => EventText.Money(amount);

        // Крупная сумма (цена на клетке, баланс) — нейтральным шрифтом GameFonts.Money.
        private static void FillMoney(TextBlock block, int amount)
        {
            block.Text = EventText.Money(amount);
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
            // Цены (администратор, §14), владельцы и филиалы — в поле клиента: по ним считается аренда на клетках.
            update.Snapshot.ApplyTo(board);
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
                        PlaceToken(j.PlayerId, GameRules.JailCell, 0, 1, 250);
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
            update.Snapshot.ApplyTo(board);
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
            var text = (Brush)Application.Current.Resources["BoardTextBrush"];

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
                    Fill = cell.IsPurchasable ? CompanyBackground(i, state) : SpecialBackground(cell.Type)
                }, r.X, r.Y);

                if (cell.IsPurchasable)
                {
                    // Клетки компаний светлые — текст на них тёмный.
                    DrawCompany(cell, state, i, r, u, (Brush)Application.Current.Resources["CompanyTextBrush"],
                        (Brush)Application.Current.Resources["CompanyMutedBrush"]);
                }
                else
                {
                    AddText(GroupPalette.Icon(cell.Type, EventText.Theme) ?? "", r.X, r.Y + r.Height * 0.12, r.Width, u * 0.34, FontWeights.Bold, text, GameFonts.Icons);
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
            if (GroupPalette.Icon(cell.Type, EventText.Theme) is { } icon)
            {
                double iconSize = u * 0.16;
                AddText(icon, bar.X, bar.Y + (bar.Height - iconSize * 1.35) / 2, bar.Width, iconSize, FontWeights.Bold, GroupPalette.IconColor(cell.Type), GameFonts.Icons);
            }

            // Логотип — строго по центру клетки (без полосы группы), цена — сразу под ним, над полосой владельца.
            // Рамка логотипа одинаковая у всех клеток ряда, поэтому цены стоят на одной линии.
            double priceHeight = u * 0.18, gap = u * 0.02;
            double priceLimit = r.Bottom - u * 0.1 - priceHeight;
            double centerY = c.Y + c.Height / 2;
            double half = Math.Min(u * 0.34, priceLimit - gap - centerY);
            double boxWidth = c.Width - u * 0.12;
            double boxHeight = 2 * half;
            double boxTop = centerY - half;
            double detail = boxTop + boxHeight + gap;
            if (Logos.For(index, EventText.Theme) is { } logo)
            {
                var (width, height) = LogoSize(logo, boxWidth, boxHeight, u * u * 0.4);
                var image = new Image
                {
                    Source = logo,
                    Width = width,
                    Height = height,
                    Stretch = Stretch.Fill,
                    Opacity = state?.IsMortgaged == true ? 0.4 : 1,
                    IsHitTestVisible = false
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                Place(image, c.X + (c.Width - width) / 2, boxTop + (boxHeight - height) / 2);
            }
            else
            {
                // Логотипа нет (военная доска, §15) — название по центру рамки, с переносом по словам.
                // Не влезает — шрифт уменьшается, пока текст не поместится.
                var name = new TextBlock
                {
                    Text = cell.Name,
                    Width = boxWidth,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    Foreground = text,
                    IsHitTestVisible = false,
                };
                // Слово не должно рваться посередине: самое длинное слово тоже должно влезать в ширину.
                var longestWord = new TextBlock { Text = cell.Name.Split(' ').OrderByDescending(w => w.Length).First(), FontWeight = FontWeights.Bold };
                for (double size = u * 0.17; ; size -= u * 0.01)
                {
                    name.FontSize = longestWord.FontSize = size;
                    name.Measure(new Size(boxWidth, double.PositiveInfinity));
                    longestWord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    bool fits = name.DesiredSize.Height <= boxHeight && longestWord.DesiredSize.Width <= boxWidth;
                    if (fits || size <= u * 0.1)
                        break;
                }
                Place(name, c.X + (c.Width - boxWidth) / 2, boxTop + Math.Max(0, (boxHeight - name.DesiredSize.Height) / 2));
            }
            if (state?.IsMortgaged == true)
            {
                // Тёмная плашка: надпись читается на фоне любого цвета владельца.
                var mortgaged = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(u * 0.06, 0, u * 0.06, 0),
                    Background = PlayerPalette.Make("#CC1E2126"),
                    Child = new TextBlock { Text = state.MortgageTurnsLeft > 0 ? $"ЗАСТАВА · {state.MortgageTurnsLeft}" : "ЗАСТАВА · !", Foreground = Brushes.White, FontSize = u * 0.13, FontWeight = FontWeights.Black },
                    IsHitTestVisible = false
                };
                mortgaged.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Place(mortgaged, c.X + (c.Width - mortgaged.DesiredSize.Width) / 2, detail);
            }
            else if (state?.OwnerId is null)
            {
                var price = new TextBlock { Width = c.Width, TextAlignment = TextAlignment.Center, FontSize = u * 0.16, Foreground = muted, IsHitTestVisible = false };
                FillMoney(price, cell.Price);
                Place(price, c.X, detail);
            }
            else
            {
                DrawRent(cell, index, state.Level, c, detail, u, text);
            }

            // Владельца показывает цвет фона клетки (CompanyBackground).
        }

        // Купленная компания: аренда, которую заплатит вставший на клетку, — коротко («252к»), чтобы не путать с ценой покупки.
        // Перед ней — филиалы (зелёные квадратики) или головной офис. У логистики аренда зависит от кубиков.
        private void DrawRent(BoardCell cell, int index, int level, Rect c, double top, double u, Brush text)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
            if (level == GameRules.HeadOfficeLevel)
            {
                line.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(u * 0.04, 0, u * 0.04, 0),
                    Margin = new Thickness(0, 0, u * 0.05, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = PlayerPalette.Make("#C62828"),
                    Child = new TextBlock { Text = EventText.Terms.OfficeTag, Foreground = Brushes.White, FontSize = u * 0.1, FontWeight = FontWeights.Bold }
                });
            }
            else
            {
                for (int k = 0; k < level; k++)
                {
                    line.Children.Add(new Rectangle
                    {
                        Width = u * 0.1,
                        Height = u * 0.1,
                        RadiusX = 2,
                        RadiusY = 2,
                        Fill = PlayerPalette.Make("#2E9E5B"),
                        Stroke = Brushes.White,
                        StrokeThickness = 1,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, k == level - 1 ? u * 0.05 : u * 0.02, 0)
                    });
                }
            }

            // Та же формула, что у движка при оплате (поле клиента обновлено из снимка — GameSnapshot.ApplyTo).
            string rent = cell.Type == CellType.Logistics
                ? $"кубики ×{EventText.ShortMoney(GameRules.Rent(board, index, 1))}"
                : EventText.ShortMoney(GameRules.Rent(board, index, 0));
            line.Children.Add(new TextBlock
            {
                Text = rent,
                FontSize = u * 0.16,
                FontFamily = GameFonts.Money,
                FontWeight = FontWeights.Bold,
                Foreground = text,
                VerticalAlignment = VerticalAlignment.Center
            });

            line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Place(line, c.X + (c.Width - line.DesiredSize.Width) / 2, top);
        }

        // Фон клетки компании: градиент от центра поля к внешнему краю — в ту же сторону, где полоса группы.
        // Свободная — слоновая кость; купленная — цвет владельца, смешанный с фоном, чтобы логотипы читались;
        // заложенная — тот же цвет, но бледнее.
        private Brush CompanyBackground(int i, CellSnapshot? state)
        {
            int side = topCount - 1;
            var (from, to) = i < side ? (new Point(0.5, 1), new Point(0.5, 0))      // верхний ряд: наружу — вверх
                : i < 2 * side ? (new Point(0, 0.5), new Point(1, 0.5))             // правый столбец: вправо
                : i < 3 * side ? (new Point(0.5, 0), new Point(0.5, 1))             // нижний ряд: вниз
                : (new Point(1, 0.5), new Point(0, 0.5));                           // левый столбец: влево
            var inner = (Color)Application.Current.Resources["BoardInnerColor"];
            var outer = (Color)Application.Current.Resources["BoardOuterColor"];
            if (state?.OwnerId is int owner && PlayerColor(owner) is SolidColorBrush ownerBrush)
            {
                // Доля цвета владельца: у центра поля насыщеннее, к краю светлее.
                double strength = state.IsMortgaged ? 0.3 : 0.7;
                inner = Mix(inner, ownerBrush.Color, strength);
                outer = Mix(outer, ownerBrush.Color, strength * 0.75);
            }
            var brush = new LinearGradientBrush(inner, outer, from, to);
            brush.Freeze();
            return brush;
        }

        private static Color Mix(Color a, Color b, double t)
        {
            byte Channel(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
            return Color.FromRgb(Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));
        }

        // Размер логотипа в рамке: все логотипы получают примерно одинаковую площадь, чтобы квадратные
        // не были огромными рядом с узкими широкими. Широкие упираются в ширину рамки, высокие — в высоту.
        // area — одна на всё поле, чтобы логотипы везде были одного веса.
        private static (double Width, double Height) LogoSize(ImageSource logo, double boxWidth, double boxHeight, double area)
        {
            double aspect = logo.Width / logo.Height;
            double width = Math.Sqrt(area * aspect), height = width / aspect;
            double scale = Math.Min(1, Math.Min(boxWidth / width, boxHeight / height));
            return (width * scale, height * scale);
        }

        private static Brush SpecialBackground(CellType type) => type switch
        {
            // Графит с лёгким оттенком: особые клетки отличаются от компаний, светлый текст читается.
            CellType.Start => PlayerPalette.Make("#34453A"),
            CellType.Jail => PlayerPalette.Make("#4A4238"),
            CellType.Casino => PlayerPalette.Make("#4A3844"),
            CellType.Rest => PlayerPalette.Make("#36414F"),
            CellType.Chance => PlayerPalette.Make("#4D4632"),
            _ => (Brush)Application.Current.Resources["BoardBrush"],
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
                double d = TokenSize;
                token = new Ellipse
                {
                    Width = d,
                    Height = d,
                    Fill = PlayerColor(playerId),
                    Stroke = Brushes.White,
                    StrokeThickness = 3,
                    Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.6 }
                };
                tokens[playerId] = token;
                tokenCanvas.Children.Add(token);
                var start = CellRect(0);
                Canvas.SetLeft(token, start.X);
                Canvas.SetTop(token, start.Y);
            }
            return token;
        }

        // Крупные фишки — чтобы их было видно на поле издалека.
        private double TokenSize => Unit * 0.34;

        // slot из count — место фишки на клетке: несколько фишек стоят в ряд, а если не помещаются — заходят друг на друга.
        private void PlaceToken(int playerId, int cell, int slot, int count, int animationMs)
        {
            double u = Unit, d = TokenSize, pad = u * 0.05;
            bool company = board[cell].IsPurchasable;
            var r = company ? CompanyLayout(cell, CellRect(cell)).Content : CellRect(cell);
            double step = count <= 1 ? 0 : Math.Min(d + u * 0.03, (r.Width - 2 * pad - d) / (count - 1));
            double x = r.X + pad + slot * step;
            // На компании низ занят ценой — фишки стоят вверху клетки, поверх логотипа.
            double y = company ? r.Y + pad : r.Bottom - d - u * 0.08;
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
            int stepMs = steps <= 12 ? 220 : Math.Max(80, 2400 / Math.Max(1, steps));
            for (int s = 1; s <= steps; s++)
            {
                int cell = ((from + (back ? -s : s)) % n + n) % n;
                PlaceToken(playerId, cell, 0, 1, (int)(stepMs * 0.85));
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
                int slot = 0, count = group.Count();
                foreach (var player in group)
                {
                    var token = Token(player.Id);
                    bool current = player.Id == Snapshot.CurrentPlayerId;
                    token.StrokeThickness = current ? 5 : 3;
                    // Фишка того, кто ходит, — поверх остальных, если они заходят друг на друга.
                    Panel.SetZIndex(token, current ? 1 : 0);
                    PlaceToken(player.Id, group.Key, slot++, count, animationMs);
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
                    notes.Add(EventText.Words.InJail);
                }
                if (player.IsResting)
                {
                    notes.Add(EventText.Words.RestNote);
                }
                int companies = Snapshot.Cells.Count(c => c.OwnerId == player.Id);
                if (companies > 0)
                {
                    notes.Add($"компаній: {companies}");
                }
                if (player.JailCards > 0)
                {
                    notes.Add($"карток «{EventText.Words.JailCard}»: {player.JailCards}");
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
