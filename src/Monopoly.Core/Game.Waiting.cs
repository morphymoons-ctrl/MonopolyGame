namespace Monopoly.Core
{
    // Кого ждёт игра, что делать по истечении таймера и как восстановить партию (RULES.md, §13).
    public partial class Game
    {
        // Восстанавливает партию: те же имена, зерно и действия дают то же состояние.
        public static Game Replay(IReadOnlyList<string> playerNames, int seed, IEnumerable<GameAction> actions)
        {
            var game = Start(playerNames, seed);
            foreach (var action in actions)
            {
                var result = game.Execute(action);
                if (!result.Success)
                    throw new InvalidDataException($"Сохранение не подходит к правилам этой версии: {result.Error}");
            }
            return game;
        }

        // Игроки, от которых игра ждёт решения.
        public IReadOnlyList<int> AwaitedPlayers()
        {
            switch (State.Phase)
            {
                case TurnPhase.GameOver:
                    return Array.Empty<int>();
                case TurnPhase.Debt:
                    return new[] { State.Debts[0].DebtorId };
                case TurnPhase.TradeOffer:
                    return new[] { State.Trade!.ToId };
                case TurnPhase.Auction:
                    var auction = State.Auction!;
                    return State.ActivePlayers
                        .Where(p => !auction.Passed.Contains(p.Id) && p.Id != auction.LeaderId)
                        .Select(p => p.Id)
                        .ToList();
                default:
                    return new[] { State.CurrentPlayer.Id };
            }
        }

        // Автодействие по истечении таймера; null — от игрока сейчас ничего не ждут.
        public GameAction? TimeoutAction(int playerId)
        {
            if (!AwaitedPlayers().Contains(playerId))
                return null;
            return State.Phase switch
            {
                TurnPhase.AwaitingRoll => new RollDice(playerId),
                TurnPhase.BuyDecision => new DeclinePurchase(playerId),
                TurnPhase.Manage => new EndTurn(playerId),
                TurnPhase.Auction => new PassAuction(playerId),
                TurnPhase.TradeOffer => new RejectTrade(playerId),
                TurnPhase.Debt => LiquidationStep(playerId),
                _ => null,
            };
        }

        // Шаг сбора денег на долг: филиал с самой застроенной компании, иначе залог самой дешёвой.
        // Долг бывает, только если продажи хватит, так что до банкротства дойдёт лишь в крайнем случае.
        public GameAction LiquidationStep(int playerId)
        {
            var cells = Enumerable.Range(0, State.Board.Count).Where(i => State.Board[i].OwnerId == playerId).ToList();

            var sell = cells
                .OrderByDescending(i => State.Board[i].Level)
                .ThenByDescending(i => State.Board[i].Price)
                .Select(i => new SellBranch(playerId, i))
                .FirstOrDefault(CanExecute);
            if (sell is not null)
                return sell;

            var mortgage = cells
                .OrderBy(i => State.Board[i].Price)
                .Select(i => new MortgageCompany(playerId, i))
                .FirstOrDefault(CanExecute);
            return (GameAction?)mortgage ?? new DeclareBankruptcy(playerId);
        }
    }
}
