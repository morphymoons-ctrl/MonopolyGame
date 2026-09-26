using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using Monopoly.Core;

namespace Monopoly.App
{
    // Показывает состояние движка и передаёт ему действия. Правил здесь нет — они в Monopoly.Core.
    public class GameManager
    {
        private readonly Game game;
        private readonly ListBox actionLog;
        private readonly Canvas boardCanvas;
        private readonly StackPanel playersPanel;

        private const int topCount = 9;
        private const int rightCount = 8;
        private const int bottomCount = 8;
        private const int leftCount = 6;

        private readonly Brush[] playerColors = new Brush[]
        {
            Brushes.Red,
            Brushes.Blue,
            Brushes.Green,
            Brushes.Orange,
            Brushes.Purple
        };

        public GameManager(ListBox log, Canvas canvas, StackPanel players)
        {
            actionLog = log;
            boardCanvas = canvas;
            playersPanel = players;
            // Пока все игроки за одним компьютером; лобби и сеть — этап 2.
            game = Game.Start(new[] { "Игрок 1", "Игрок 2" });
            foreach (var e in game.History)
            {
                AddLog(EventText.Describe(e, State));
            }
        }

        private GameState State => game.State;
        private int CurrentId => State.CurrentPlayer.Id;

        public bool CanRollDice => game.CanExecute(new RollDice(CurrentId));
        public bool CanBuy => game.CanExecute(new BuyProperty(CurrentId));
        public bool CanDecline => game.CanExecute(new DeclinePurchase(CurrentId));
        public bool CanEndTurn => game.CanExecute(new EndTurn(CurrentId));

        public void DrawBoard()
        {
            var board = State.Board;
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
                if (board[i].OwnerId is int owner)
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
            double canvasWidth = boardCanvas.ActualWidth > 0 ? boardCanvas.ActualWidth : boardCanvas.Width;
            double canvasHeight = boardCanvas.ActualHeight > 0 ? boardCanvas.ActualHeight : boardCanvas.Height;
            double sizeX = canvasWidth / (topCount);
            double sizeY = canvasHeight / (topCount);
            double size = Math.Min(sizeX, sizeY);

            int total = State.Board.Count;
            foreach (var player in State.Players)
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
                    Fill = PlayerColor(player.Id)
                };
                Canvas.SetLeft(ellipse, x);
                Canvas.SetTop(ellipse, y);
                boardCanvas.Children.Add(ellipse);
            }
        }

        // Список игроков с балансом; текущий — жирным.
        private void DrawPlayersPanel()
        {
            playersPanel.Children.Clear();
            foreach (var player in State.Players)
            {
                bool isCurrent = player == State.CurrentPlayer;
                playersPanel.Children.Add(new TextBlock
                {
                    Text = $"{(isCurrent ? "▶ " : "")}{player.Name} — {player.Balance} грн",
                    Foreground = PlayerColor(player.Id),
                    FontSize = 28,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal
                });
            }
        }

        private Brush PlayerColor(int playerId) => playerColors[playerId % playerColors.Length];

        public void RollDice() => Execute(new RollDice(CurrentId));

        public void Buy() => Execute(new BuyProperty(CurrentId));

        public void DeclineBuy() => Execute(new DeclinePurchase(CurrentId));

        public void EndTurn() => Execute(new EndTurn(CurrentId));

        // Пока игра на одном ПК, действует всегда тот, чей ход.
        private void Execute(GameAction action)
        {
            var result = game.Execute(action);
            if (!result.Success)
            {
                AddLog(result.Error!);
                return;
            }
            foreach (var e in result.Events)
            {
                AddLog(EventText.Describe(e, State));
            }
            DrawBoard();
        }

        private void AddLog(string text)
        {
            // Отдельный элемент, чтобы прокрутка шла к нему, а не к первой такой же строке.
            var item = new ListBoxItem { Content = text };
            actionLog.Items.Add(item);
            actionLog.ScrollIntoView(item);
        }
    }
}
