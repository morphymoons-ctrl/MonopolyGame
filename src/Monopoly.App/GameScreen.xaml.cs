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
    // Какие кнопки показать, решает хост — через список доступных действий.
    public partial class GameScreen : UserControl
    {
        private readonly GameClient client;
        private readonly GameManager gameManager;
        private readonly Func<Task> leave;
        private IReadOnlyList<GameAction> available = Array.Empty<GameAction>();
        private bool sending;

        public GameScreen(GameClient client, GameStartInfo info, Func<Task> leave)
        {
            InitializeComponent();
            this.client = client;
            this.leave = leave;
            gameManager = new GameManager(ActionLog, GameBoardCanvas, PlayersPanel, info);
            gameManager.CellClicked += cell =>
            {
                gameManager.Select(cell);
                ShowCell();
            };
            gameManager.DrawBoard();
            Refresh();
        }

        private int MyId => gameManager.MyPlayerId;
        private GameSnapshot? Snapshot => gameManager.Snapshot;

        public void Apply(GameUpdate update)
        {
            available = update.AvailableActions;
            sending = false;
            gameManager.Apply(update);
            Refresh();
        }

        public void AddLog(string text) => gameManager.AddLog(text);

        private void Refresh()
        {
            ShowTurn();
            ShowActions();
            ShowCell();
        }

        private void ShowTurn()
        {
            if (Snapshot is not { } s)
            {
                TurnText.Text = "";
                ContextText.Text = "";
                return;
            }

            var current = s.FindPlayer(s.CurrentPlayerId)!;
            string Name(int? id) => id is int value ? s.FindPlayer(value)?.Name ?? "?" : "банку";
            TurnText.Text = s.WinnerId is int winner
                ? $"Победил {Name(winner)}!"
                : s.CurrentPlayerId == MyId ? "Ваш ход!" : $"Ходит {current.Name}";

            var me = s.FindPlayer(MyId);
            ContextText.Text = s.Phase switch
            {
                TurnPhase.GameOver => "Партия окончена.",
                TurnPhase.Debt when s.Debt!.DebtorId == MyId =>
                    $"Не хватает наличных: долг {s.Debt.Amount} грн {DebtTarget(s.Debt.CreditorId, s)}. " +
                    "Нажмите на свою клетку, чтобы продать филиалы или заложить компанию. Долг спишется сам, как только хватит денег.",
                TurnPhase.Debt => $"Ждём: {Name(s.Debt!.DebtorId)} собирает деньги на долг {s.Debt.Amount} грн.",
                TurnPhase.Auction => DescribeAuction(s.Auction!, s),
                TurnPhase.TradeOffer when s.Trade!.ToId == MyId =>
                    $"{Name(s.Trade.FromId)} предлагает вам обмен: {EventText.DescribeOffer(s.Trade, s)}",
                TurnPhase.TradeOffer when s.Trade!.FromId == MyId =>
                    $"Ждём ответа {Name(s.Trade.ToId)}: вы {EventText.DescribeOffer(s.Trade, s)}",
                TurnPhase.TradeOffer => $"{Name(s.Trade!.FromId)} предлагает обмен игроку {Name(s.Trade.ToId)}.",
                TurnPhase.BuyDecision when s.CurrentPlayerId == MyId =>
                    $"«{EventText.Cells[s.PendingPurchase!.Value].Name}» свободна: купите её или откажитесь — тогда будет аукцион.",
                TurnPhase.AwaitingRoll when s.CurrentPlayerId == MyId && me?.IsInJail == true =>
                    $"Вы в тюрьме: заплатите залог {GameRules.BailAmount} грн, используйте карточку или бросьте кубики — нужен дубль.",
                _ when s.CasinoAvailable && s.CurrentPlayerId == MyId =>
                    "Вы в казино. Ставка 50–300 грн: 50% — проигрыш, 10% — возврат, 35% — ×2, 5% — ×3. Можно не играть.",
                _ => "",
            };
        }

        private static string DebtTarget(int? creditorId, GameSnapshot s) =>
            creditorId is int id ? $"игроку {s.FindPlayer(id)?.Name}" : "банку";

        private static string DescribeAuction(AuctionSnapshot auction, GameSnapshot s)
        {
            string lot = EventText.Cells[auction.CellIndex].Name;
            string bid = auction.LeaderId is int leader
                ? $"ставка {auction.HighBid} грн у игрока {s.FindPlayer(leader)?.Name}"
                : "ставок пока нет";
            var passed = auction.PassedIds.Select(id => s.FindPlayer(id)?.Name).ToList();
            string passedText = passed.Count > 0 ? $" Спасовали: {string.Join(", ", passed)}." : "";
            return $"Аукцион «{lot}»: {bid}.{passedText}";
        }

        // Кнопки для всех доступных действий, кроме действий с клеткой (они в панели клетки).
        private void ShowActions()
        {
            ActionsPanel.Children.Clear();
            TradeButton.IsEnabled = !sending && available.OfType<ProposeTrade>().Any();
            if (Snapshot is not { } s || sending)
            {
                return;
            }

            if (available.OfType<PlaceBid>().FirstOrDefault() is { } minBid && s.Auction is { } auction)
            {
                int balance = s.FindPlayer(MyId)?.Balance ?? 0;
                int baseBid = auction.LeaderId is null ? 0 : auction.HighBid;
                var amounts = new[] { minBid.Amount, baseBid + 50, baseBid + 100 }
                    .Where(a => a >= minBid.Amount && a <= balance)
                    .Distinct();
                foreach (int amount in amounts)
                {
                    AddButton(ActionsPanel, $"Ставка {amount} грн", new PlaceBid(MyId, amount));
                }
            }

            foreach (var action in available)
            {
                if (action is PlaceBid or ProposeTrade or BuildBranch or SellBranch or MortgageCompany or RedeemCompany)
                {
                    continue;
                }
                AddButton(ActionsPanel, EventText.ActionLabel(action, s), action);
            }
        }

        private void ShowCell()
        {
            CellActions.Children.Clear();
            if (gameManager.SelectedCell is not int index)
            {
                return;
            }

            var cell = EventText.Cells[index];
            var state = Snapshot?.Cells[index];
            CellTitle.Text = cell.Name;
            CellInfo.Text = DescribeCell(cell, state);

            if (sending)
            {
                return;
            }
            foreach (var action in available)
            {
                string? label = action switch
                {
                    BuildBranch b when b.CellIndex == index => state?.Level == GameRules.HeadOfficeLevel - 1
                        ? $"Головной офис ({cell.BranchCost} грн)"
                        : $"Построить филиал ({cell.BranchCost} грн)",
                    SellBranch s when s.CellIndex == index => $"Продать филиал (+{cell.BranchSaleValue} грн)",
                    MortgageCompany m when m.CellIndex == index => $"Заложить (+{cell.MortgageValue} грн)",
                    RedeemCompany r when r.CellIndex == index => $"Выкупить ({cell.RedeemCost} грн)",
                    _ => null,
                };
                if (label is not null)
                {
                    AddButton(CellActions, label, action);
                }
            }
        }

        private string DescribeCell(BoardCell cell, CellSnapshot? state)
        {
            if (!cell.IsPurchasable)
            {
                return cell.Type switch
                {
                    CellType.Start => $"Проход или попадание — +{GameRules.StartBonus} грн.",
                    CellType.Jail => $"Здесь просто в гостях. В тюрьму попадают по карточке «Шанса» или за три дубля подряд. Залог — {GameRules.BailAmount} грн.",
                    CellType.Casino => "Ставка 50–300 грн сразу после попадания: 50% — проигрыш, 10% — возврат, 35% — ×2, 5% — ×3.",
                    CellType.Rest => "Пропуск следующего хода.",
                    CellType.Chance => "Карточка из колоды «Шанса»: деньги, перемещение, тюрьма или выход из неё.",
                    _ => "",
                };
            }

            string owner = state?.OwnerId is int id ? Snapshot?.FindPlayer(id)?.Name ?? "?" : "банк";
            var lines = new List<string> { $"{EventText.GroupName(cell.Type)} · цена {cell.Price} грн · владелец: {owner}" };
            if (state is { IsMortgaged: true })
            {
                lines.Add($"Заложена — аренды нет. Выкуп {cell.RedeemCost} грн.");
            }
            else if (state is { Level: > 0 })
            {
                lines.Add(state.Level == GameRules.HeadOfficeLevel ? "Головной офис." : $"Филиалов: {state.Level}.");
            }

            switch (cell.Type)
            {
                case CellType.GasStation:
                    lines.Add($"Аренда: {string.Join(" / ", GameRules.GasStationRent.Skip(1))} грн за 1 / 2 / 3 / 4 АЗС у владельца.");
                    break;
                case CellType.Logistics:
                    lines.Add($"Аренда: сумма кубиков ×{GameRules.LogisticsSingle}, если у владельца одна компания, ×{GameRules.LogisticsBoth} — если обе.");
                    break;
                default:
                    int rent = GameRules.BaseRent(cell);
                    var levels = GameRules.LevelMultipliers.Skip(1).Select(m => rent * m).ToList();
                    lines.Add($"Аренда: {rent} · монополия {rent * GameRules.MonopolyMultiplier} · филиалы {string.Join(" / ", levels.Take(4))} · головной офис {levels[4]} грн.");
                    lines.Add($"Филиал {cell.BranchCost} грн (продажа {cell.BranchSaleValue}).");
                    break;
            }
            lines.Add($"Залог {cell.MortgageValue} грн, выкуп {cell.RedeemCost} грн.");
            return string.Join("\n", lines);
        }

        private void AddButton(Panel panel, string label, GameAction action)
        {
            var button = new Button
            {
                Content = label,
                Height = 48,
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 12, 12),
                FontSize = 20
            };
            button.Click += async (_, _) => await SendAsync(action);
            panel.Children.Add(button);
        }

        private async Task SendAsync(GameAction action)
        {
            // До ответа хоста кнопки скрыты, чтобы не отправить действие дважды.
            sending = true;
            ShowActions();
            ShowCell();

            var error = await client.SendActionAsync(action);
            if (error is not null)
            {
                gameManager.AddLog(error);
                sending = false;
                ShowActions();
                ShowCell();
            }
        }

        private async void Trade_Click(object sender, RoutedEventArgs e)
        {
            if (Snapshot is null)
            {
                return;
            }
            var window = new TradeWindow(Snapshot, MyId, gameManager.PlayerColor) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true && window.Proposal is { } proposal)
            {
                await SendAsync(proposal);
            }
        }

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
