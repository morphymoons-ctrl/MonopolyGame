namespace Monopoly.Core
{
    // Действия администратора (RULES.md, §14). Тихие: своих событий нет, в журнал не пишутся.
    // События могут появиться только от обычных правил после изменения — например, закрытие долга.
    public partial class Game
    {
        public const int AdminMaxBalance = 100_000_000;
        public const int AdminMinPrice = 1_000, AdminMaxPrice = 10_000_000;

        public ActionResult ExecuteAdmin(AdminAction action)
        {
            ArgumentNullException.ThrowIfNull(action);

            var error = ValidateAdmin(action);
            if (error is not null)
                return ActionResult.Fail(error);

            switch (action)
            {
                case AdminSetBalance balance:
                    State.FindPlayer(balance.TargetId)!.Balance = balance.Amount;
                    break;
                case AdminSetPrice price:
                    State.Board[price.CellIndex].Price = price.Price;
                    break;
                case AdminSetOwner owner:
                    var cell = State.Board[owner.CellIndex];
                    cell.OwnerId = owner.OwnerId;
                    if (owner.OwnerId is null)
                        cell.IsMortgaged = false;
                    break;
                case AdminSetJail jail:
                    SetJail(State.FindPlayer(jail.TargetId)!, jail.InJail);
                    break;
            }

            var events = new List<GameEvent>();
            Continue(events);
            history.AddRange(events);
            actions.Add(action);
            return ActionResult.Ok(events);
        }

        private string? ValidateAdmin(AdminAction action)
        {
            if (State.Phase == TurnPhase.GameOver)
                return "Гру закінчено.";

            switch (action)
            {
                case AdminSetBalance balance:
                    return ValidateAdminTarget(balance.TargetId)
                        ?? (balance.Amount < 0 || balance.Amount > AdminMaxBalance
                            ? $"Баланс — від 0 до {Money(AdminMaxBalance)}."
                            : null);

                case AdminSetPrice price:
                {
                    if (AdminCompany(price.CellIndex) is not { } cell)
                        return "Це не компанія.";
                    if (cell.OwnerId is not null)
                        return $"«{cell.Name}» вже куплена — ціну можна змінити лише вільній компанії.";
                    return price.Price < AdminMinPrice || price.Price > AdminMaxPrice
                        ? $"Ціна — від {Money(AdminMinPrice)} до {Money(AdminMaxPrice)}."
                        : null;
                }

                case AdminSetOwner owner:
                {
                    if (AdminCompany(owner.CellIndex) is not { } cell)
                        return "Це не компанія.";
                    if (owner.OwnerId is int id && ValidateAdminTarget(id) is { } targetError)
                        return targetError;
                    if (cell.OwnerId == owner.OwnerId)
                        return owner.OwnerId is null ? $"«{cell.Name}» і так у банку." : $"«{cell.Name}» і так у цього гравця.";
                    if (State.PendingPurchase == owner.CellIndex || State.Auction?.CellIndex == owner.CellIndex)
                        return $"«{cell.Name}» зараз продається — зачекайте, поки гравці вирішать.";
                    if (State.GroupOf(cell).Any(c => c.Level > 0))
                        return $"У групі «{cell.Name}» є {State.Terms.Branches} — спершу власник має їх продати.";
                    return null;
                }

                case AdminSetJail jail:
                {
                    var error = ValidateAdminTarget(jail.TargetId);
                    if (error is not null)
                        return error;
                    var player = State.FindPlayer(jail.TargetId)!;
                    if (player.IsInJail == jail.InJail)
                        return jail.InJail
                            ? $"{player.Name} вже сидить («{State.Board[GameRules.JailCell].Name}»)."
                            : $"{player.Name} і так не сидить («{State.Board[GameRules.JailCell].Name}»).";
                    return null;
                }

                default:
                    return "Невідома дія.";
            }
        }

        private string? ValidateAdminTarget(int playerId)
        {
            var player = State.FindPlayer(playerId);
            if (player is null)
                return "Немає такого гравця.";
            return player.IsBankrupt ? $"{player.Name} вибув із гри." : null;
        }

        private BoardCell? AdminCompany(int cellIndex) =>
            cellIndex >= 0 && cellIndex < State.Board.Count && State.Board[cellIndex].IsPurchasable ? State.Board[cellIndex] : null;

        private void SetJail(Player player, bool inJail)
        {
            // Карточка при этом не тратится: посадил администратор — пропуск хода будет.
            player.IsInJail = inJail;
            if (!inJail)
                return;

            player.Position = GameRules.JailCell;
            // Как при обычном попадании: если он сейчас ходит, бросков в этом ходу больше нет.
            if (player == State.CurrentPlayer)
            {
                State.DoublesInRow = 0;
                State.Stage = TurnPhase.Manage;
            }
        }
    }
}
