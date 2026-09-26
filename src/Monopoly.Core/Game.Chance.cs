namespace Monopoly.Core
{
    // Колода «Шанса» (RULES.md, §9).
    public partial class Game
    {
        private void DrawChance(Player player, List<GameEvent> events)
        {
            if (State.ChanceDeck.Count == 0)
            {
                // Колода кончилась — сброс перемешивается и становится новой колодой.
                State.ChanceDeck.AddRange(State.ChanceDiscard);
                State.ChanceDiscard.Clear();
                if (options.ShuffleChanceDeck)
                    Shuffle(State.ChanceDeck);
            }

            var card = State.ChanceDeck[0];
            State.ChanceDeck.RemoveAt(0);
            events.Add(new ChanceCardDrawn(player.Id, card));

            // «Выйти из тюрьмы» остаётся у игрока; остальные — в сброс.
            if (card == ChanceCard.GetOutOfJail)
                player.JailCards++;
            else
                State.ChanceDiscard.Add(card);

            ApplyCard(player, card, events);
        }

        private void ApplyCard(Player player, ChanceCard card, List<GameEvent> events)
        {
            var others = State.ActivePlayers.Where(p => p != player).ToList();
            int bankAmount = ChanceCards.BankAmount(card);

            switch (card)
            {
                case ChanceCard.GetOutOfJail:
                    break;
                case ChanceCard.Birthday:
                    foreach (var other in others)
                        Charge(other, player, ChanceCards.BirthdayGift, events);
                    break;
                case ChanceCard.Charity:
                    foreach (var other in others)
                        Charge(player, other, ChanceCards.CharityGift, events);
                    break;
                case ChanceCard.TaxAudit:
                    var cells = State.CellsOf(player.Id).ToList();
                    int branches = cells.Where(c => c.Level < GameRules.HeadOfficeLevel).Sum(c => c.Level);
                    int headOffices = cells.Count(c => c.Level == GameRules.HeadOfficeLevel);
                    Charge(player, null, branches * ChanceCards.AuditPerBranch + headOffices * ChanceCards.AuditPerHeadOffice, events);
                    break;
                case ChanceCard.GoToStart:
                    MoveForwardTo(player, 0, events);
                    ResolveCell(player, events);
                    break;
                case ChanceCard.NovaPoshta:
                    MoveForwardTo(player, ChanceCards.NovaPoshtaCell, events);
                    ResolveCell(player, events);
                    break;
                case ChanceCard.Taxi:
                    MoveForwardTo(player, NearestGasStation(player.Position), events);
                    ResolveCell(player, events, rentMultiplier: 2);
                    break;
                case ChanceCard.Train:
                    MoveBack(player, ChanceCards.TrainStepsBack, events);
                    ResolveCell(player, events);
                    break;
                case ChanceCard.GoToJail:
                    SendToJail(player, JailReason.Card, events);
                    break;
                case var _ when bankAmount > 0:
                    player.Balance += bankAmount;
                    events.Add(new ReceivedFromBank(player.Id, bankAmount));
                    break;
                case var _ when bankAmount < 0:
                    Charge(player, null, -bankAmount, events);
                    break;
            }
        }

        private int NearestGasStation(int from)
        {
            int count = State.Board.Count;
            for (int step = 1; step <= count; step++)
            {
                int index = (from + step) % count;
                if (State.Board[index].Type == CellType.GasStation)
                    return index;
            }
            return from;
        }
    }
}
