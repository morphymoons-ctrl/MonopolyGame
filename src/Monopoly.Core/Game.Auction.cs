namespace Monopoly.Core
{
    // Аукцион после отказа от покупки (RULES.md, §4). Очереди нет: ставки и пасы — в любом порядке.
    public partial class Game
    {
        private void Decline(Player player, List<GameEvent> events)
        {
            int cellIndex = State.PendingPurchase!.Value;
            State.PendingPurchase = null;
            State.Auction = new AuctionState(cellIndex);
            events.Add(new PurchaseDeclined(player.Id, cellIndex));
            events.Add(new AuctionStarted(cellIndex));
        }

        private string? ValidateBid(Player player, int amount)
        {
            var auction = State.Auction;
            if (State.Phase != TurnPhase.Auction || auction is null)
                return "Сейчас нет аукциона.";
            if (auction.Passed.Contains(player.Id))
                return "Вы уже спасовали.";
            if (auction.LeaderId == player.Id)
                return "Ваша ставка и так самая высокая.";
            if (amount < auction.MinBid)
                return $"Ставка должна быть не меньше {auction.MinBid} грн.";
            return amount > player.Balance ? $"Не хватает денег: у вас {player.Balance} грн." : null;
        }

        private string? ValidatePass(Player player)
        {
            var auction = State.Auction;
            if (State.Phase != TurnPhase.Auction || auction is null)
                return "Сейчас нет аукциона.";
            if (auction.Passed.Contains(player.Id))
                return "Вы уже спасовали.";
            return auction.LeaderId == player.Id ? "Лидер аукциона не может спасовать." : null;
        }

        private void Bid(Player player, int amount, List<GameEvent> events)
        {
            var auction = State.Auction!;
            auction.LeaderId = player.Id;
            auction.HighBid = amount;
            events.Add(new BidPlaced(player.Id, amount));
            FinishAuctionIfDone(events);
        }

        private void Pass(Player player, List<GameEvent> events)
        {
            State.Auction!.Passed.Add(player.Id);
            events.Add(new AuctionPassed(player.Id));
            FinishAuctionIfDone(events);
        }

        // Конец — когда спасовали все, кроме лидера (или все, если ставок не было).
        private void FinishAuctionIfDone(List<GameEvent> events)
        {
            var auction = State.Auction!;
            var remaining = State.ActivePlayers.Where(p => !auction.Passed.Contains(p.Id)).ToList();
            if (remaining.Any(p => p.Id != auction.LeaderId))
                return;

            var cell = State.Board[auction.CellIndex];
            if (auction.LeaderId is int winnerId)
            {
                // Пока идёт аукцион, деньги ни у кого не меняются, так что ставка по-прежнему по карману.
                var winner = State.FindPlayer(winnerId)!;
                winner.Balance -= auction.HighBid;
                cell.OwnerId = winnerId;
                events.Add(new AuctionWon(winnerId, auction.CellIndex, auction.HighBid));
            }
            else
            {
                events.Add(new AuctionUnsold(auction.CellIndex));
            }
            State.Auction = null;
        }
    }
}
