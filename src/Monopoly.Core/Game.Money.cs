namespace Monopoly.Core
{
    // Платежи, долги и банкротство (RULES.md, §12).
    public partial class Game
    {
        // Платёж игрока банку (creditor = null) или другому игроку.
        // Хватает наличных — платит сразу; не хватает — долг; не хватит даже после продажи всего — банкротство.
        // paidEvent — событие на случай оплаты сразу; без него — PaidToBank или PaidToPlayer.
        private void Charge(Player payer, Player? creditor, int amount, List<GameEvent> events, GameEvent? paidEvent = null)
        {
            if (amount <= 0 || payer.IsBankrupt)
                return;

            bool hasDebts = State.Debts.Any(d => d.DebtorId == payer.Id);
            if (!hasDebts && payer.Balance >= amount)
            {
                Transfer(payer, creditor, amount);
                events.Add(paidEvent ?? (creditor is null
                    ? new PaidToBank(payer.Id, amount)
                    : new PaidToPlayer(payer.Id, creditor.Id, amount)));
                return;
            }

            events.Add(new DebtIncurred(payer.Id, creditor?.Id, amount));
            int alreadyOwed = State.Debts.Where(d => d.DebtorId == payer.Id).Sum(d => d.Amount);
            if (LiquidationValue(payer) - alreadyOwed < amount)
            {
                GoBankrupt(payer, creditor?.Id, events);
                return;
            }
            State.Debts.Add(new Debt(payer.Id, creditor?.Id, amount));
        }

        private static void Transfer(Player payer, Player? creditor, int amount)
        {
            payer.Balance -= amount;
            if (creditor is not null)
                creditor.Balance += amount;
        }

        // Сколько игрок соберёт, если продаст все филиалы и заложит все компании.
        private int LiquidationValue(Player player) =>
            player.Balance + State.CellsOf(player.Id).Sum(c =>
                c.Level * c.BranchSaleValue + (c.IsMortgaged ? 0 : c.MortgageValue));

        // Долги по очереди: первый закрывается, как только у должника хватает денег.
        private void SettleDebts(List<GameEvent> events)
        {
            while (State.Debts.Count > 0)
            {
                var debt = State.Debts[0];
                var debtor = State.FindPlayer(debt.DebtorId)!;
                if (debtor.Balance < debt.Amount)
                    return;

                var creditor = debt.CreditorId is int id ? State.FindPlayer(id) : null;
                // Кредитор мог выбыть — тогда деньги уходят банку.
                if (creditor is { IsBankrupt: true })
                    creditor = null;
                Transfer(debtor, creditor, debt.Amount);
                State.Debts.RemoveAt(0);
                events.Add(new DebtPaid(debtor.Id, creditor?.Id, debt.Amount));
            }
        }

        // Банкротство (§12): филиалы продаются банку за полцены, компании возвращаются банку свободными —
        // их снова можно купить. Деньги и карточки — кредитору; при долге банку — банку и в колоду.
        private void GoBankrupt(Player player, int? creditorId, List<GameEvent> events)
        {
            var creditor = creditorId is int id ? State.FindPlayer(id) : null;
            if (creditor is { IsBankrupt: true })
                creditor = null;

            foreach (var cell in State.CellsOf(player.Id).ToList())
            {
                player.Balance += cell.Level * cell.BranchSaleValue;
                cell.Level = 0;
                cell.OwnerId = null;
                cell.IsMortgaged = false;
            }

            if (creditor is not null)
            {
                creditor.Balance += player.Balance;
                creditor.JailCards += player.JailCards;
            }
            else
            {
                for (int i = 0; i < player.JailCards; i++)
                    State.ChanceDiscard.Add(ChanceCard.GetOutOfJail);
            }

            player.Balance = 0;
            player.JailCards = 0;
            player.IsInJail = false;
            player.IsResting = false;
            player.IsBankrupt = true;

            // Долги перед выбывшим SettleDebts отдаст банку.
            State.Debts.RemoveAll(d => d.DebtorId == player.Id);
            if (player == State.CurrentPlayer)
            {
                State.PendingPurchase = null;
                State.CasinoAvailable = false;
            }

            events.Add(new PlayerBankrupt(player.Id, creditor?.Id));
        }
    }
}
