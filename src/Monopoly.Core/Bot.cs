namespace Monopoly.Core
{
    // Бот для свободных мест и отключившихся игроков (RULES.md, §13).
    // Играет осторожно и без случайностей: одно и то же состояние — одно и то же решение.
    public static class Bot
    {
        // Сколько денег бот оставляет себе после покупки, стройки или ставки.
        public const int Reserve = 300_000;

        // Следующее действие бота; null — сейчас от него ничего не ждут.
        public static GameAction? Choose(Game game, int playerId)
        {
            var state = game.State;
            var me = state.FindPlayer(playerId);
            if (me is null || me.IsBankrupt || !game.AwaitedPlayers().Contains(playerId))
                return null;

            switch (state.Phase)
            {
                case TurnPhase.Debt:
                    return game.LiquidationStep(playerId);
                case TurnPhase.TradeOffer:
                    return new RejectTrade(playerId);
                case TurnPhase.Auction:
                    var auction = state.Auction!;
                    int bid = auction.MinBid;
                    bool worth = bid <= state.Board[auction.CellIndex].Price && me.Balance - bid >= Reserve;
                    return worth ? new PlaceBid(playerId, bid) : new PassAuction(playerId);
                case TurnPhase.BuyDecision:
                    return me.Balance - state.CurrentCell.Price >= Reserve
                        ? new BuyProperty(playerId)
                        : new DeclinePurchase(playerId);
                case TurnPhase.AwaitingRoll:
                    if (me.IsInJail && game.CanExecute(new UseJailCard(playerId)))
                        return new UseJailCard(playerId);
                    return Improve(game, me) ?? new RollDice(playerId);
                case TurnPhase.Manage:
                    return Improve(game, me) ?? new EndTurn(playerId);
                default:
                    return null;
            }
        }

        // Сначала выкупить заложенное, потом строить — если остаётся запас.
        private static GameAction? Improve(Game game, Player me)
        {
            var board = game.State.Board;
            var mine = Enumerable.Range(0, board.Count).Where(i => board[i].OwnerId == me.Id).ToList();

            var redeem = mine
                .Where(i => board[i].IsMortgaged && me.Balance - board[i].RedeemCost >= Reserve)
                .OrderByDescending(i => board[i].Price)
                .Select(i => new RedeemCompany(me.Id, i))
                .FirstOrDefault(game.CanExecute);
            if (redeem is not null)
                return redeem;

            return mine
                .Where(i => me.Balance - board[i].BranchCost >= Reserve)
                .OrderBy(i => board[i].Level)
                .ThenByDescending(i => board[i].Price)
                .Select(i => new BuildBranch(me.Id, i))
                .FirstOrDefault(game.CanExecute);
        }
    }
}
