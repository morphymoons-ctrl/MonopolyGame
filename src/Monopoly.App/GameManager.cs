using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
        private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("uk-UA");

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

        public static string Format(int amount) => $"{amount.ToString("N0", Money)} грн";

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

        public void Select(int cellIndex)
        {
            SelectedCell = cellIndex;
            DrawBoard();
        }

        private double CellSize => boardCanvas.Width / topCount;

        // Левый верхний угол клетки: по часовой стрелке от «Старта» в левом верхнем углу.
        private static Point CellOrigin(int i, double size)
        {
            if (i < topCount)
            {
                return new Point(i * size, 0);
            }
            if (i < topCount + rightCount)
            {
                return new Point((topCount - 1) * size, (i - topCount + 1) * size);
            }
            if (i < topCount + rightCount + bottomCount)
            {
                return new Point((topCount - 1 - (i - topCount - rightCount + 1)) * size, (topCount - 1) * size);
            }
            return new Point(0, (topCount - 1 - (i - topCount - rightCount - bottomCount + 1)) * size);
        }

        public void DrawBoard()
        {
            boardCanvas.Children.Clear();
            double size = CellSize;
            var line = (Brush)Application.Current.Resources["BoardLineBrush"];
            var paper = (Brush)Application.Current.Resources["BoardBrush"];
            var text = (Brush)Application.Current.Resources["BoardTextBrush"];
            var muted = (Brush)Application.Current.Resources["BoardMutedBrush"];

            for (int i = 0; i < board.Count; i++)
            {
                var origin = CellOrigin(i, size);
                var cell = board[i];
                var state = Snapshot?.Cells[i];

                Place(new Rectangle
                {
                    Width = size,
                    Height = size,
                    Stroke = line,
                    StrokeThickness = 1,
                    Fill = state?.IsMortgaged == true ? PlayerPalette.Make("#DAD7CF") : cell.IsPurchasable ? paper : SpecialBackground(cell.Type)
                }, origin.X, origin.Y);

                if (cell.IsPurchasable)
                {
                    DrawCompany(cell, state, origin, size, text, muted);
                }
                else
                {
                    AddText(GroupPalette.Icon(cell.Type) ?? "", origin.X, origin.Y + size * 0.05, size, size * 0.28, FontWeights.Bold, text, "Segoe UI Symbol");
                    AddText(cell.Name, origin.X, origin.Y + size * 0.44, size, size * 0.13, FontWeights.Bold, text);
                }

                if (SelectedCell == i)
                {
                    Place(new Rectangle
                    {
                        Width = size - 2,
                        Height = size - 2,
                        Stroke = (Brush)Application.Current.Resources["AccentPressedBrush"],
                        StrokeThickness = 5,
                        IsHitTestVisible = false
                    }, origin.X + 1, origin.Y + 1);
                }

                // Щелчок по клетке — прозрачная кнопка поверх неё (стиль CellButton в GameScreen.xaml).
                int index = i;
                var hit = new Button { Width = size, Height = size, Style = (Style)boardCanvas.FindResource("CellButton") };
                AutomationProperties.SetName(hit, cell.Name);
                AutomationProperties.SetAutomationId(hit, $"Cell{i}");
                hit.Click += (_, _) => CellClicked?.Invoke(index);
                Place(hit, origin.X, origin.Y);
            }
            DrawPlayersPanel();
        }

        // Компания: полоса группы сверху, название, цена или филиалы, полоса владельца снизу.
        private void DrawCompany(BoardCell cell, CellSnapshot? state, Point origin, double size, Brush text, Brush muted)
        {
            double bar = size * 0.2;
            Place(new Rectangle { Width = size - 2, Height = bar, Fill = GroupPalette.Get(cell.Type), IsHitTestVisible = false }, origin.X + 1, origin.Y + 1);
            if (GroupPalette.Icon(cell.Type) is { } icon)
            {
                AddText(icon, origin.X, origin.Y + 1, size, bar * 0.72, FontWeights.Bold, Brushes.White, "Segoe UI Symbol");
            }

            AddText(cell.Name, origin.X + 2, origin.Y + bar + size * 0.04, size - 4, size * 0.125, FontWeights.Bold, text);

            if (state?.IsMortgaged == true)
            {
                AddText("ЗАЛОГ", origin.X, origin.Y + size * 0.5, size, size * 0.12, FontWeights.Black, (Brush)Application.Current.Resources["DangerBrush"]);
            }
            else if (state is { Level: > 0 })
            {
                DrawBranches(state.Level, origin, size);
            }
            else if (state?.OwnerId is null)
            {
                AddText(Format(cell.Price), origin.X, origin.Y + size * 0.5, size, size * 0.12, FontWeights.SemiBold, muted);
            }

            if (state?.OwnerId is int owner)
            {
                Place(new Rectangle { Width = size - 2, Height = size * 0.1, Fill = PlayerColor(owner), IsHitTestVisible = false },
                    origin.X + 1, origin.Y + size * 0.9 - 1);
            }
        }

        // Филиалы — зелёные домики, головной офис — красное здание.
        private void DrawBranches(int level, Point origin, double size)
        {
            if (level == GameRules.HeadOfficeLevel)
            {
                var office = new Border
                {
                    Width = size * 0.5,
                    Height = size * 0.18,
                    CornerRadius = new CornerRadius(3),
                    Background = PlayerPalette.Make("#C62828"),
                    Child = new TextBlock { Text = "ОФИС", Foreground = Brushes.White, FontSize = size * 0.1, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                    IsHitTestVisible = false
                };
                Place(office, origin.X + size * 0.25, origin.Y + size * 0.5);
                return;
            }
            double house = size * 0.15, gap = size * 0.05;
            double start = origin.X + (size - level * house - (level - 1) * gap) / 2;
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
                }, start + k * (house + gap), origin.Y + size * 0.52);
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

        private void AddText(string text, double x, double y, double width, double fontSize, FontWeight weight, Brush color, string? font = null)
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
                tb.FontFamily = new FontFamily(font);
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
                double d = CellSize * 0.2;
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
                var start = CellOrigin(0, CellSize);
                Canvas.SetLeft(token, start.X);
                Canvas.SetTop(token, start.Y);
            }
            return token;
        }

        // slot — место фишки на клетке, чтобы несколько фишек стояли рядом.
        private void PlaceToken(int playerId, int cell, int slot, int animationMs)
        {
            double size = CellSize;
            var origin = CellOrigin(cell, size);
            double x = origin.X + size * 0.05 + slot * size * 0.185;
            double y = origin.Y + size * 0.68;
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
                    notes.Add("вы");
                }
                if (player.IsBankrupt)
                {
                    notes.Add("выбыл");
                }
                if (player.IsInJail)
                {
                    notes.Add("в тюрьме");
                }
                if (player.IsResting)
                {
                    notes.Add("отдыхает");
                }
                int companies = Snapshot.Cells.Count(c => c.OwnerId == player.Id);
                if (companies > 0)
                {
                    notes.Add($"компаний: {companies}");
                }
                if (player.JailCards > 0)
                {
                    notes.Add($"карточек «Выйти из тюрьмы»: {player.JailCards}");
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
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
                    });
                }
                Grid.SetColumn(names, 1);
                grid.Children.Add(names);

                var balance = new TextBlock
                {
                    Text = Format(player.Balance),
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isCurrent ? (Brush)Application.Current.Resources["AccentBrush"] : (Brush)Application.Current.Resources["TextBrush"]
                };
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

        public void AddLog(string text)
        {
            // Отдельный элемент, чтобы прокрутка шла к нему, а не к первой такой же строке.
            var item = new ListBoxItem { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } };
            actionLog.Items.Add(item);
            actionLog.ScrollIntoView(item);
        }
    }
}
