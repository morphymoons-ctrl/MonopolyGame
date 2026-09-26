namespace Monopoly.Core
{
    // Филиалы (RULES.md, §5) и залог (§10).
    public partial class Game
    {
        // Продавать и закладывать можно в свой ход, а должнику — и во время долга.
        private string? RequireManageOrDebt(Player player) =>
            State.Phase == TurnPhase.Debt && State.Debts[0].DebtorId == player.Id
                ? null
                : RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage);

        private string? ValidateOwnCell(Player player, int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= State.Board.Count)
                return "Немає такої клітинки.";
            return State.Board[cellIndex].OwnerId == player.Id ? null : "Це не ваша компанія.";
        }

        private string? ValidateBuild(Player player, int cellIndex)
        {
            var error = RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            var group = State.GroupOf(cell).ToList();
            if (!cell.IsBuildable)
                return "На АЗС і логістиці філії не будуються.";
            if (!GameRules.IsMonopoly(State.Board, cell.Type, player.Id))
                return "Філії будуються, лише коли у вас уся група.";
            if (group.Any(c => c.IsMortgaged))
                return "У групі є закладена компанія — спершу викупіть її.";
            if (cell.Level == GameRules.HeadOfficeLevel)
                return "Тут уже головний офіс.";
            if (cell.Level > group.Min(c => c.Level))
                return "Будуйте рівномірно: спершу на інших компаніях групи.";
            return player.Balance < cell.BranchCost ? $"Не вистачає грошей: філія коштує {GameRules.Money(cell.BranchCost)}." : null;
        }

        private string? ValidateSell(Player player, int cellIndex)
        {
            var error = RequireManageOrDebt(player) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            if (cell.Level == 0)
                return "Тут немає філій.";
            return cell.Level < State.GroupOf(cell).Max(c => c.Level)
                ? "Продавайте рівномірно: спершу з інших компаній групи."
                : null;
        }

        private string? ValidateMortgage(Player player, int cellIndex)
        {
            var error = RequireManageOrDebt(player) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            if (cell.IsMortgaged)
                return "Компанію вже закладено.";
            return State.GroupOf(cell).Any(c => c.Level > 0) ? "Спершу продайте філії в цій групі." : null;
        }

        private string? ValidateRedeem(Player player, int cellIndex)
        {
            var error = RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            if (!cell.IsMortgaged)
                return "Компанію не закладено.";
            return player.Balance < cell.RedeemCost ? $"Не вистачає грошей: викуп коштує {GameRules.Money(cell.RedeemCost)}." : null;
        }

        private void Build(Player player, int cellIndex, List<GameEvent> events)
        {
            var cell = State.Board[cellIndex];
            player.Balance -= cell.BranchCost;
            cell.Level++;
            events.Add(new BranchBuilt(player.Id, cellIndex, cell.Level, cell.BranchCost));
        }

        private void Sell(Player player, int cellIndex, List<GameEvent> events)
        {
            var cell = State.Board[cellIndex];
            player.Balance += cell.BranchSaleValue;
            cell.Level--;
            events.Add(new BranchSold(player.Id, cellIndex, cell.Level, cell.BranchSaleValue));
        }

        private void Mortgage(Player player, int cellIndex, List<GameEvent> events)
        {
            var cell = State.Board[cellIndex];
            player.Balance += cell.MortgageValue;
            cell.IsMortgaged = true;
            events.Add(new CompanyMortgaged(player.Id, cellIndex, cell.MortgageValue));
        }

        private void Redeem(Player player, int cellIndex, List<GameEvent> events)
        {
            var cell = State.Board[cellIndex];
            player.Balance -= cell.RedeemCost;
            cell.IsMortgaged = false;
            events.Add(new CompanyRedeemed(player.Id, cellIndex, cell.RedeemCost));
        }
    }
}
