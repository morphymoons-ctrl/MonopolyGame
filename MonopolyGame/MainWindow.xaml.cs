using System.Windows;
using System.Windows.Controls;

namespace MonopolyGame
{
    public partial class MainWindow : Window
    {
        private GameManager gameManager;

        public MainWindow()
        {
            InitializeComponent();
            gameManager = new GameManager(ActionLog, GameBoardCanvas);
            gameManager.DrawBoard();
        }

        private void RollDice_Click(object sender, RoutedEventArgs e)
        {
            gameManager.RollDice();
        }

        private void Buy_Click(object sender, RoutedEventArgs e)
        {
            gameManager.Buy();
        }

        private void EndTurn_Click(object sender, RoutedEventArgs e)
        {
            gameManager.EndTurn();
        }

        private void PayBail_Click(object sender, RoutedEventArgs e)
        {
            gameManager.PayBail();
        }

        private void Casino_Click(object sender, RoutedEventArgs e)
        {
            gameManager.Casino();
        }
    }
}
