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
                return "Нет такой клетки.";
            return State.Board[cellIndex].OwnerId == player.Id ? null : "Это не ваша компания.";
        }

        private string? ValidateBuild(Player player, int cellIndex)
        {
            var error = RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            var group = State.GroupOf(cell).ToList();
            if (!cell.IsBuildable)
                return "На АЗС и логистике филиалы не строятся.";
            if (!GameRules.IsMonopoly(State.Board, cell.Type, player.Id))
                return "Филиалы строятся, только когда у вас вся группа.";
            if (group.Any(c => c.IsMortgaged))
                return "В группе есть заложенная компания — сначала выкупите её.";
            if (cell.Level == GameRules.HeadOfficeLevel)
                return "Здесь уже головной офис.";
            if (cell.Level > group.Min(c => c.Level))
                return "Стройте равномерно: сначала на других компаниях группы.";
            return player.Balance < cell.BranchCost ? $"Не хватает денег: филиал стоит {cell.BranchCost} грн." : null;
        }

        private string? ValidateSell(Player player, int cellIndex)
        {
            var error = RequireManageOrDebt(player) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            if (cell.Level == 0)
                return "Здесь нет филиалов.";
            return cell.Level < State.GroupOf(cell).Max(c => c.Level)
                ? "Продавайте равномерно: сначала с других компаний группы."
                : null;
        }

        private string? ValidateMortgage(Player player, int cellIndex)
        {
            var error = RequireManageOrDebt(player) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            if (cell.IsMortgaged)
                return "Компания уже заложена.";
            return State.GroupOf(cell).Any(c => c.Level > 0) ? "Сначала продайте филиалы в этой группе." : null;
        }

        private string? ValidateRedeem(Player player, int cellIndex)
        {
            var error = RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage) ?? ValidateOwnCell(player, cellIndex);
            if (error is not null)
                return error;

            var cell = State.Board[cellIndex];
            if (!cell.IsMortgaged)
                return "Компания не заложена.";
            return player.Balance < cell.RedeemCost ? $"Не хватает денег: выкуп стоит {cell.RedeemCost} грн." : null;
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
