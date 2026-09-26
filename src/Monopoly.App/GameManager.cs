using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Input;
using Monopoly.Core;

namespace Monopoly.App
{
    public class GameManager
    {
        private List<Player> players = new();
        private int currentPlayerIndex = 0;
        private List<BoardCell> board = new();
        private ListBox actionLog;
        private Canvas boardCanvas;

        private const int topCount = 9;
        private const int rightCount = 8;
        private const int bottomCount = 8;
        private const int leftCount = 6;

        private BoardCell? lastLandedCell = null;

        private readonly Brush[] playerColors = new Brush[]
        {
            Brushes.Red,
            Brushes.Blue,
            Brushes.Green,
            Brushes.Orange,
            Brushes.Purple
        };

        public GameManager(ListBox log, Canvas canvas)
        {
            actionLog = log;
            boardCanvas = canvas;
            InitPlayers();
            InitBoard();
        }

        private void InitPlayers()
        {
            players.Clear();
            players.Add(new Player("Игрок 1"));
        }

        private void InitBoard()
        {
            board = Board.CreateDefault();
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
                if (board[i].OwnerId != -1 && board[i].OwnerId < playerColors.Length)
                {
                    cellFill = playerColors[board[i].OwnerId];
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
        }

        private void DrawPlayers()
        {
            double canvasWidth = boardCanvas.ActualWidth > 0 ? boardCanvas.ActualWidth : boardCanvas.Width;
            double canvasHeight = boardCanvas.ActualHeight > 0 ? boardCanvas.ActualHeight : boardCanvas.Height;
            double sizeX = canvasWidth / (topCount);
            double sizeY = canvasHeight / (topCount);
            double size = Math.Min(sizeX, sizeY);

            int total = board.Count;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
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
                    Fill = playerColors[i % playerColors.Length]
                };
                Canvas.SetLeft(ellipse, x);
                Canvas.SetTop(ellipse, y);
                boardCanvas.Children.Add(ellipse);
            }
        }

        public void RollDice()
        {
            var player = players[currentPlayerIndex];

            // Проверка на тюрьму
            if (player.IsInJail)
            {
                var rand = new Random();
                int dice1 = rand.Next(1, 7);
                int dice2 = rand.Next(1, 7);
                actionLog.Items.Add($"{player.Name} в тюрьме. Бросает кубики для выхода: {dice1} и {dice2}");

                if (dice1 == dice2)
                {
                    player.IsInJail = false;
                    player.JailTurns = 0;
                    actionLog.Items.Add($"{player.Name} выбил дубль и выходит из тюрьмы!");
                }
                else
                {
                    player.JailTurns++;
                    if (player.JailTurns >= 3)
                    {
                        player.IsInJail = false;
                        player.JailTurns = 0;
                        player.Balance -= 200;
                        actionLog.Items.Add($"{player.Name} отсидел 3 хода и заплатил 200 грн за выход.");
                    }
                    else
                    {
                        actionLog.Items.Add($"{player.Name} не выбил дубль. Осталось попыток: {3 - player.JailTurns}");
                        DrawBoard();
                        return;
                    }
                }
            }

            // Проверка на отдых
            if (player.IsResting)
            {
                player.IsResting = false;
                actionLog.Items.Add($"{player.Name} отдыхает и пропускает ход.");
                DrawBoard();
                return;
            }

            var randMove = new Random();
            int dice1m = randMove.Next(1, 7);
            int dice2m = randMove.Next(1, 7);
            int sum = dice1m + dice2m;

            actionLog.Items.Add($"{player.Name} бросил кубики: {dice1m} и {dice2m} (сумма: {sum})");

            player.Position = (player.Position + sum) % board.Count;
            var cell = board[player.Position];
            lastLandedCell = cell;
            actionLog.Items.Add($"{player.Name} переместился на: {cell.Name}");

            // Обработка специальных клеток
            if (cell.Type == CellType.Rest)
            {
                player.IsResting = true;
                actionLog.Items.Add($"{player.Name} попал на клетку отдыха и пропустит следующий ход.");
            }
            else if (cell.Type == CellType.Jail)
            {
                player.IsInJail = true;
                player.JailTurns = 0;
                actionLog.Items.Add($"{player.Name} попал в тюрьму и пропустит до 3 ходов или пока не выбьет дубль.");
            }
            else if (cell.Type == CellType.Casino)
            {
                Casino();
            }
            else if (cell.IsPurchasable)
            {
                if (cell.OwnerId == -1)
                {
                    actionLog.Items.Add($"{cell.Name} свободна. Можно купить.");
                }
                else if (cell.OwnerId != currentPlayerIndex)
                {
                    int rent = cell.Price / 2;
                    player.Balance -= rent;
                    players[cell.OwnerId].Balance += rent;
                    actionLog.Items.Add($"{player.Name} заплатил аренду {rent} грн игроку {players[cell.OwnerId].Name}.");
                }
                else
                {
                    actionLog.Items.Add($"{player.Name} попал на свою клетку.");
                }
            }

            DrawBoard();
        }

        public void Buy()
        {
            var player = players[currentPlayerIndex];
            if (lastLandedCell == null)
            {
                actionLog.Items.Add("Сначала бросьте кубики.");
                return;
            }

            if (!lastLandedCell.IsPurchasable)
            {
                actionLog.Items.Add("Эту клетку нельзя купить.");
                return;
            }

            if (lastLandedCell.OwnerId != -1)
            {
                actionLog.Items.Add("Клетка уже куплена.");
                return;
            }

            if (player.Balance < lastLandedCell.Price)
            {
                actionLog.Items.Add("Недостаточно средств для покупки.");
                return;
            }

            player.Balance -= lastLandedCell.Price;
            lastLandedCell.OwnerId = currentPlayerIndex;
            actionLog.Items.Add($"{player.Name} купил {lastLandedCell.Name} за {lastLandedCell.Price} грн.");
            DrawBoard();
        }

        public void EndTurn()
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            actionLog.Items.Add($"Ход передан: теперь ходит {players[currentPlayerIndex].Name}");
        }

        public void PayBail()
        {
            var player = players[currentPlayerIndex];
            if (player.IsInJail && player.Balance >= 200)
            {
                player.Balance -= 200;
                player.IsInJail = false;
                player.JailTurns = 0;
                actionLog.Items.Add($"{player.Name} заплатил 200 грн и вышел из тюрьмы.");
                DrawBoard();
            }
            else
            {
                actionLog.Items.Add("Нельзя выйти из тюрьмы или недостаточно средств.");
            }
        }

        public void Casino()
        {
            var player = players[currentPlayerIndex];
            int bet = 0;

            // Простое окно для ввода ставки
            var input = new InputBox("Введите ставку для казино (грн):", "Казино");
            if (input.ShowDialog() == true)
            {
                if (!int.TryParse(input.InputText, out bet) || bet <= 0 || bet > player.Balance)
                {
                    actionLog.Items.Add("Некорректная ставка или недостаточно средств.");
                    return;
                }
            }
            else
            {
                actionLog.Items.Add("Ставка отменена.");
                return;
            }

            // Рулетка: 0x, 1x, 2x, 3x
            var rand = new Random();
            int[] multipliers = { 0, 1, 2, 3 };
            int result = multipliers[rand.Next(multipliers.Length)];
            int win = bet * result - bet;

            player.Balance += win;
            string resText = result switch
            {
                0 => $"Проигрыш! Потеряно {bet} грн.",
                1 => $"Ничья! Ставка возвращена.",
                2 => $"Выигрыш! Получено {bet} грн.",
                3 => $"Джекпот! Получено {bet * 2} грн.",
                _ => ""
            };
            actionLog.Items.Add($"{player.Name} сыграл в казино: {resText}");
            DrawBoard();
        }
    }
}
