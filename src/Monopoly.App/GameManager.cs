using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.App
{
    // Рисует поле, игроков и журнал по снимку состояния от хоста. Правил здесь нет — они в Monopoly.Core.
    public class GameManager
    {
        private readonly IReadOnlyList<BoardCell> board = EventText.Cells;
        private readonly GameStartInfo startInfo;
        private readonly ListBox actionLog;
        private readonly Canvas boardCanvas;
        private readonly StackPanel playersPanel;

        private const int topCount = 9;
        private const int rightCount = 8;
        private const int bottomCount = 8;

        // Последнее состояние от хоста; null — ещё не пришло.
        public GameSnapshot? Snapshot { get; private set; }
        // Клетка, выбранная щелчком; её данные и кнопки показывает панель клетки.
        public int? SelectedCell { get; private set; }

        public event Action<int>? CellClicked;

        public GameManager(ListBox log, Canvas canvas, StackPanel players, GameStartInfo info)
        {
            actionLog = log;
            boardCanvas = canvas;
            playersPanel = players;
            startInfo = info;
        }

        public int MyPlayerId => startInfo.MyPlayerId;

        public void Apply(GameUpdate update)
        {
            Snapshot = update.Snapshot;
            foreach (var e in update.Events)
            {
                AddLog(EventText.Describe(e, update.Snapshot));
            }
            DrawBoard();
        }

        public void Select(int cellIndex)
        {
            SelectedCell = cellIndex;
            DrawBoard();
        }

        public Brush PlayerColor(int playerId) =>
            PlayerPalette.Get(startInfo.ColorByPlayerId.TryGetValue(playerId, out int color) ? color : playerId);

        private double CellSize
        {
            get
            {
                double canvasWidth = boardCanvas.ActualWidth > 0 ? boardCanvas.ActualWidth : boardCanvas.Width;
                double canvasHeight = boardCanvas.ActualHeight > 0 ? boardCanvas.ActualHeight : boardCanvas.Height;
                return Math.Min(canvasWidth / topCount, canvasHeight / topCount);
            }
        }

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

            for (int i = 0; i < board.Count; i++)
            {
                var origin = CellOrigin(i, size);
                var cell = board[i];
                var state = Snapshot?.Cells[i];

                var rect = new Rectangle
                {
                    Width = size,
                    Height = size,
                    Stroke = SelectedCell == i ? Brushes.DarkOrange : Brushes.Black,
                    StrokeThickness = SelectedCell == i ? 4 : 1,
                    Fill = state?.IsMortgaged == true ? Brushes.LightGray : Brushes.White
                };
                Place(rect, origin.X, origin.Y);

                AddText(cell.Name, origin.X, origin.Y + size * 0.06, size, size / 7, FontWeights.SemiBold);

                // Под названием: цена свободной компании, филиалы или отметка залога.
                string detail = "";
                if (state?.IsMortgaged == true)
                {
                    detail = "заложена";
                }
                else if (state?.Level == GameRules.HeadOfficeLevel)
                {
                    detail = "головной офис";
                }
                else if (state?.Level > 0)
                {
                    detail = new string('●', state.Level);
                }
                else if (cell.IsPurchasable && state?.OwnerId is null)
                {
                    detail = $"{cell.Price} грн";
                }
                AddText(detail, origin.X, origin.Y + size * 0.42, size, size / 8, FontWeights.Normal);

                // Владелец — цветная полоса внизу клетки.
                if (state?.OwnerId is int owner)
                {
                    var strip = new Rectangle
                    {
                        Width = size - 2,
                        Height = size * 0.14,
                        Fill = PlayerColor(owner),
                        IsHitTestVisible = false
                    };
                    Place(strip, origin.X + 1, origin.Y + size * 0.86 - 1);
                }

                // Щелчок по клетке — прозрачная кнопка поверх неё (стиль CellButton в GameScreen.xaml).
                int index = i;
                var hit = new Button
                {
                    Width = size,
                    Height = size,
                    Style = (Style)boardCanvas.FindResource("CellButton")
                };
                AutomationProperties.SetName(hit, cell.Name);
                AutomationProperties.SetAutomationId(hit, $"Cell{i}");
                hit.Click += (_, _) => CellClicked?.Invoke(index);
                Place(hit, origin.X, origin.Y);
            }
            DrawPlayers(size);
            DrawPlayersPanel();
        }

        private void AddText(string text, double x, double y, double width, double fontSize, FontWeight weight)
        {
            var tb = new TextBlock
            {
                Text = text,
                Width = width,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Black,
                FontSize = fontSize,
                FontWeight = weight,
                IsHitTestVisible = false
            };
            Place(tb, x, y);
        }

        private void Place(UIElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            boardCanvas.Children.Add(element);
        }

        // Фишки на одной клетке стоят рядом, а не друг на друге.
        private void DrawPlayers(double size)
        {
            if (Snapshot is null)
            {
                return;
            }
            foreach (var group in Snapshot.Players.Where(p => !p.IsBankrupt).GroupBy(p => p.Position))
            {
                var origin = CellOrigin(group.Key, size);
                int k = 0;
                foreach (var player in group)
                {
                    var token = new Ellipse
                    {
                        Width = size * 0.17,
                        Height = size * 0.17,
                        Fill = PlayerColor(player.Id),
                        Stroke = Brushes.Black,
                        StrokeThickness = player.Id == Snapshot.CurrentPlayerId ? 3 : 1,
                        IsHitTestVisible = false
                    };
                    Place(token, origin.X + size * 0.05 + k * size * 0.18, origin.Y + size * 0.64);
                    k++;
                }
            }
        }

        // Список игроков с балансом; тот, чей ход, — жирным.
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
                if (player.JailCards > 0)
                {
                    notes.Add($"карточек «Выйти из тюрьмы»: {player.JailCards}");
                }
                string suffix = notes.Count > 0 ? $" ({string.Join(", ", notes)})" : "";
                playersPanel.Children.Add(new TextBlock
                {
                    Text = $"{(isCurrent ? "▶ " : "")}{player.Name} — {player.Balance} грн{suffix}",
                    Foreground = PlayerColor(player.Id),
                    FontSize = 24,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                    TextDecorations = player.IsBankrupt ? TextDecorations.Strikethrough : null,
                    TextWrapping = TextWrapping.Wrap
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
