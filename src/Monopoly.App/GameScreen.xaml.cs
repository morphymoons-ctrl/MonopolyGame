using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.App
{
    // Экран партии: показывает состояние от хоста и отправляет ему действия игрока.
    public partial class GameScreen : UserControl
    {
        private readonly GameClient client;
        private readonly GameManager gameManager;
        private readonly Func<Task> leave;
        private IReadOnlyList<GameAction> available = Array.Empty<GameAction>();

        public GameScreen(GameClient client, GameStartInfo info, Func<Task> leave)
        {
            InitializeComponent();
            this.client = client;
            this.leave = leave;
            gameManager = new GameManager(ActionLog, GameBoardCanvas, PlayersPanel, info);
            gameManager.DrawBoard();
            UpdateButtons();
        }

        public void Apply(GameUpdate update)
        {
            available = update.AvailableActions;
            gameManager.Apply(update);
            var current = update.Snapshot.FindPlayer(update.Snapshot.CurrentPlayerId);
            TurnText.Text = update.Snapshot.CurrentPlayerId == gameManager.MyPlayerId
                ? "Ваш ход!"
                : $"Ходит {current?.Name}";
            UpdateButtons();
        }

        public void AddLog(string text) => gameManager.AddLog(text);

        // Кнопки включаются по списку от хоста: что этому игроку можно сделать прямо сейчас.
        private void UpdateButtons()
        {
            RollDiceButton.IsEnabled = available.OfType<RollDice>().Any();
            BuyButton.IsEnabled = available.OfType<BuyProperty>().Any();
            DeclineButton.IsEnabled = available.OfType<DeclinePurchase>().Any();
            EndTurnButton.IsEnabled = available.OfType<EndTurn>().Any();
        }

        private async Task SendAsync(GameAction action)
        {
            // До ответа хоста кнопки выключены, чтобы не отправить действие дважды.
            var previous = available;
            available = Array.Empty<GameAction>();
            UpdateButtons();

            var error = await client.SendActionAsync(action);
            if (error is not null)
            {
                gameManager.AddLog(error);
                available = previous;
                UpdateButtons();
            }
        }

        private async void RollDice_Click(object sender, RoutedEventArgs e) =>
            await SendAsync(new RollDice(gameManager.MyPlayerId));

        private async void Buy_Click(object sender, RoutedEventArgs e) =>
            await SendAsync(new BuyProperty(gameManager.MyPlayerId));

        private async void Decline_Click(object sender, RoutedEventArgs e) =>
            await SendAsync(new DeclinePurchase(gameManager.MyPlayerId));

        private async void EndTurn_Click(object sender, RoutedEventArgs e) =>
            await SendAsync(new EndTurn(gameManager.MyPlayerId));

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(Window.GetWindow(this)!, "Выйти из партии? Вернуться в неё пока нельзя.",
                "Монополия", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                await leave();
            }
        }
    }
}
