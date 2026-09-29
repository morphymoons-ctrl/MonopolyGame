namespace Monopoly.Core
{
    // Аукцион после отказа от покупки (RULES.md, §4). Очереди нет: ставки и пасы — в любом порядке.
    public partial class Game
    {
        private void Decline(Player player, List<GameEvent> events)
        {
            int cellIndex = State.PendingPurchase!.Value;
            State.PendingPurchase = null;
            int price = State.Board[cellIndex].Price;
            // 30% от продажи — только тому, кому не хватило денег: отказ при деньгах ничего не приносит (§4).
            int? finderId = player.Balance < price ? player.Id : null;
            State.Auction = new AuctionState(cellIndex, GameRules.AuctionStartBid(price), finderId);
            events.Add(new PurchaseDeclined(player.Id, cellIndex));
            events.Add(new AuctionStarted(cellIndex, State.Auction.StartBid, finderId));
        }

        private string? ValidateBid(Player player, int amount)
        {
            var auction = State.Auction;
            if (State.Phase != TurnPhase.Auction || auction is null)
                return "Зараз немає аукціону.";
            if (auction.Passed.Contains(player.Id))
                return "Ви вже спасували.";
            if (auction.LeaderId == player.Id)
                return "Ваша ставка й так найвища.";
            if (amount < auction.MinBid)
                return $"Ставка має бути не менше {Money(auction.MinBid)}.";
            return amount > player.Balance ? $"Не вистачає грошей: у вас {Money(player.Balance)}." : null;
        }

        private string? ValidatePass(Player player)
        {
            var auction = State.Auction;
            if (State.Phase != TurnPhase.Auction || auction is null)
                return "Зараз немає аукціону.";
            if (auction.Passed.Contains(player.Id))
                return "Ви вже спасували.";
            return auction.LeaderId == player.Id ? "Лідер аукціону не може спасувати." : null;
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
                State.Stats.Bought(winnerId);
                events.Add(new AuctionWon(winnerId, auction.CellIndex, auction.HighBid));
                // Находчику — 30% ставки из денег победителя, банку — остальное (§4).
                if (auction.FinderId is int finderId && finderId != winnerId && State.FindPlayer(finderId) is { IsBankrupt: false } finder)
                {
                    int share = GameRules.FinderShare(auction.HighBid);
                    finder.Balance += share;
                    events.Add(new FinderPaid(finderId, auction.CellIndex, share));
                }
            }
            else
            {
                events.Add(new AuctionUnsold(auction.CellIndex));
            }
            State.Auction = null;
        }
    }
}
