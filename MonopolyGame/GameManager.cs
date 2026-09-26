using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Input;

namespace MonopolyGame
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

        private bool isResting = false;
        private bool isInJail = false;
        private int jailTurnsLeft = 0;
        private int jailPlayerIndex = -1;

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
            board.Clear();
            board.Add(new BoardCell("Старт", CellType.Start));
            board.Add(new BoardCell("АТБ", CellType.Supermarket, 100));
            board.Add(new BoardCell("Варус", CellType.Supermarket, 120));
            board.Add(new BoardCell("Сильпо", CellType.Supermarket, 140));
            board.Add(new BoardCell("WOG", CellType.GasStation, 160));
            board.Add(new BoardCell("АрселорМитал", CellType.Factory, 200));
            board.Add(new BoardCell("Завод Стасика", CellType.Factory, 220));
            board.Add(new BoardCell("Азовсталь", CellType.Factory, 240));
            board.Add(new BoardCell("Тюрьма", CellType.Jail));
            board.Add(new BoardCell("ТЕТ", CellType.TV, 100));
            board.Add(new BoardCell("Новый канал", CellType.TV, 120));
            board.Add(new BoardCell("Интел", CellType.TV, 140));
            board.Add(new BoardCell("ОККО", CellType.GasStation, 160));
            board.Add(new BoardCell("Пузата хата", CellType.Food, 200));
            board.Add(new BoardCell("Pizza Day", CellType.Food, 220));
            board.Add(new BoardCell("Булочная №1", CellType.Food, 240));
            board.Add(new BoardCell("Казино", CellType.Casino));
            board.Add(new BoardCell("Отдых", CellType.Rest));
            board.Add(new BoardCell("Розетка", CellType.OnlineShop, 100));
            board.Add(new BoardCell("Пром", CellType.OnlineShop, 120));
            board.Add(new BoardCell("ОЛХ", CellType.OnlineShop, 140));
            board.Add(new BoardCell("UPG", CellType.GasStation, 160));
            board.Add(new BoardCell("Нова пошта", CellType.Logistics, 200));
            board.Add(new BoardCell("Укрпошта", CellType.Logistics, 220));
            board.Add(new BoardCell("Шанс", CellType.Chance));
            board.Add(new BoardCell("ПУМБ", CellType.Bank, 100));
            board.Add(new BoardCell("Приватбанк", CellType.Bank, 120));
            board.Add(new BoardCell("Монобанк", CellType.Bank, 140));
            board.Add(new BoardCell("Укрнафта", CellType.GasStation, 160));
            board.Add(new BoardCell("Алло", CellType.NetworkShop, 200));
            board.Add(new BoardCell("Цитрус", CellType.NetworkShop, 220));
            board.Add(new BoardCell("Фокстрот", CellType.NetworkShop, 240));
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
            else if (IsPurchasable(cell))
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

            if (!IsPurchasable(lastLandedCell))
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

        private bool IsPurchasable(BoardCell cell)
        {
            return cell.Type == CellType.Supermarket ||
                   cell.Type == CellType.GasStation ||
                   cell.Type == CellType.Factory ||
                   cell.Type == CellType.TV ||
                   cell.Type == CellType.Food ||
                   cell.Type == CellType.OnlineShop ||
                   cell.Type == CellType.Logistics ||
                   cell.Type == CellType.Bank ||
                   cell.Type == CellType.NetworkShop;
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

    // Простое окно для ввода числа (ставки)
    public class InputBox : Window
    {
        public string InputText { get; private set; }
        private TextBox inputBox;
        private bool result = false;

        public InputBox(string prompt, string title)
        {
            Title = title;
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;

            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = prompt, FontSize = 18, Margin = new Thickness(0, 0, 0, 10) });
            inputBox = new TextBox { FontSize = 18, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(inputBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            var cancelBtn = new Button { Content = "Отмена", Width = 80 };
            okBtn.Click += (s, e) => { InputText = inputBox.Text; result = true; Close(); };
            cancelBtn.Click += (s, e) => { result = false; Close(); };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            panel.Children.Add(btnPanel);

            Content = panel;
        }

        public new bool? ShowDialog()
        {
            base.ShowDialog();
            return result;
        }
    }
}
