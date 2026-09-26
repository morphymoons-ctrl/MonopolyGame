using System.Windows;
using System.Windows.Controls;

namespace Monopoly.App
{
    public partial class MainWindow : Window
    {
        private GameManager gameManager;

        public MainWindow()
        {
            InitializeComponent();
            gameManager = new GameManager(ActionLog, GameBoardCanvas, PlayersPanel);
            gameManager.DrawBoard();
            UpdateButtons();
        }

        // Доступны только действия, которые движок разрешает прямо сейчас.
        private void UpdateButtons()
        {
            RollDiceButton.IsEnabled = gameManager.CanRollDice;
            BuyButton.IsEnabled = gameManager.CanBuy;
            DeclineButton.IsEnabled = gameManager.CanDecline;
            EndTurnButton.IsEnabled = gameManager.CanEndTurn;
        }

        private void RollDice_Click(object sender, RoutedEventArgs e)
        {
            gameManager.RollDice();
            UpdateButtons();
        }

        private void Buy_Click(object sender, RoutedEventArgs e)
        {
            gameManager.Buy();
            UpdateButtons();
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            gameManager.DeclineBuy();
            UpdateButtons();
        }

        private void EndTurn_Click(object sender, RoutedEventArgs e)
        {
            gameManager.EndTurn();
            UpdateButtons();
        }
    }
}
