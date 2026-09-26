using System;
using System.Collections.Generic;
using System.Windows.Controls;
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
        private readonly IReadOnlyList<BoardCell> board = Board.CreateDefault();
        private readonly GameStartInfo startInfo;
        private readonly ListBox actionLog;
        private readonly Canvas boardCanvas;
        private readonly StackPanel playersPanel;

        private const int topCount = 9;
        private const int rightCount = 8;
        private const int bottomCount = 8;
        private const int leftCount = 6;

        // Последнее состояние от хоста; null — ещё не пришло.
        public GameSnapshot? Snapshot { get; private set; }

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

        public void DrawBoard()
        {
            boardCanvas.Children.Clear();
            double canvasWidth = boardCanvas.ActualWidth > 0 ? boardCanvas.ActualWidth : boardCanvas.Width;
            double canvasHeight = boardCanvas.ActualHeight > 0 ? boardCanvas.ActualHeight : boardCanvas.Height;
            double sizeX = canvasWidth / (topCount);
            double sizeY = canvasHeight / (topCount);
            double size = Math.Min(sizeX, sizeY);
            int total = board.Count;

            for (int i = 0; i < total; i++)
            {
                double x = 0, y = 0;
                if (i < topCount)
                {
                    x = i * size;
                    y = 0;
                }
                else if (i < topCount + rightCount)
                {
                    x = (topCount - 1) * size;
                    y = (i - topCount + 1) * size;
                }
                else if (i < topCount + rightCount + bottomCount)
                {
                    x = (topCount - 1 - (i - topCount - rightCount + 1)) * size;
                    y = (topCount - 1) * size;
                }
                else
                {
                    x = 0;
                    y = (topCount - 1 - (i - topCount - rightCount - bottomCount + 1)) * size;
                }

                Brush cellFill = Brushes.White;
                if (Snapshot?.Owners[i] is int owner)
                {
                    cellFill = PlayerColor(owner);
                }
                Rectangle rect = new Rectangle
                {
                    Width = size,
                    Height = size,
                    Stroke = Brushes.Black,
                    Fill = cellFill
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                boardCanvas.Children.Add(rect);

                TextBlock tb = new TextBlock
                {
                    Text = board[i].Name,
                    Width = size,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Black,
                    FontSize = size / 6
                };
                Canvas.SetLeft(tb, x);
                Canvas.SetTop(tb, y + size / 3);
                boardCanvas.Children.Add(tb);
            }
            DrawPlayers();
            DrawPlayersPanel();
        }

        private void DrawPlayers()
        {
            if (Snapshot is null)
            {
                return;
            }
            double canvasWidth = boardCanvas.ActualWidth > 0 ? boardCanvas.ActualWidth : boardCanvas.Width;
            double canvasHeight = boardCanvas.ActualHeight > 0 ? boardCanvas.ActualHeight : boardCanvas.Height;
            double sizeX = canvasWidth / (topCount);
            double sizeY = canvasHeight / (topCount);
            double size = Math.Min(sizeX, sizeY);

            int total = board.Count;
            foreach (var player in Snapshot.Players)
            {
                int pos = player.Position % total;
                double x = 0, y = 0;
                if (pos < topCount)
                {
                    x = pos * size + size * 0.35;
                    y = size * 0.15;
                }
                else if (pos < topCount + rightCount)
                {
                    x = (topCount - 1) * size + size * 0.35;
                    y = (pos - topCount + 1) * size + size * 0.15;
                }
                else if (pos < topCount + rightCount + bottomCount)
                {
                    x = (topCount - 1 - (pos - topCount - rightCount + 1)) * size + size * 0.35;
                    y = (topCount - 1) * size + size * 0.15;
                }
                else
                {
                    x = size * 0.35;
                    y = (topCount - 1 - (pos - topCount - rightCount - bottomCount + 1)) * size + size * 0.15;
                }

                Ellipse ellipse = new Ellipse
                {
                    Width = size * 0.3,
                    Height = size * 0.3,
                    Fill = PlayerColor(player.Id),
                    Stroke = Brushes.Black
                };
                Canvas.SetLeft(ellipse, x);
                Canvas.SetTop(ellipse, y);
                boardCanvas.Children.Add(ellipse);
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
                bool isCurrent = player.Id == Snapshot.CurrentPlayerId;
                string me = player.Id == MyPlayerId ? " (вы)" : "";
                playersPanel.Children.Add(new TextBlock
                {
                    Text = $"{(isCurrent ? "▶ " : "")}{player.Name}{me} — {player.Balance} грн",
                    Foreground = PlayerColor(player.Id),
                    FontSize = 28,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal
                });
            }
        }

        private Brush PlayerColor(int playerId) =>
            PlayerPalette.Get(startInfo.ColorByPlayerId.TryGetValue(playerId, out int color) ? color : playerId);

        public void AddLog(string text)
        {
            // Отдельный элемент, чтобы прокрутка шла к нему, а не к первой такой же строке.
            var item = new ListBoxItem { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } };
            actionLog.Items.Add(item);
            actionLog.ScrollIntoView(item);
        }
    }
}
