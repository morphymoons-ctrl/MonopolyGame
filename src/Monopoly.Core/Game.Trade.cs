namespace Monopoly.Core
{
    // Обмен между игроками (RULES.md, §11).
    public partial class Game
    {
        private string? ValidateProposal(Player player, ProposeTrade proposal)
        {
            var error = RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage);
            if (error is not null)
                return error;
            var target = State.FindPlayer(proposal.TargetId);
            if (target is null || target.IsBankrupt || target == player)
                return "Выберите, с кем меняться.";
            return ValidateOffer(new TradeOffer(player.Id, target.Id, proposal.Give, proposal.Take));
        }

        // Проверяется и при предложении, и при согласии.
        private string? ValidateOffer(TradeOffer offer)
        {
            if (offer.Give is null || offer.Take is null || offer.Give.Cells is null || offer.Take.Cells is null)
                return "Неверные условия обмена.";
            if (offer.Give.IsEmpty && offer.Take.IsEmpty)
                return "Обмен пустой: добавьте компании, деньги или карточки.";
            var from = State.FindPlayer(offer.FromId)!;
            var to = State.FindPlayer(offer.ToId)!;
            return ValidateTerms(from, offer.Give, "у вас") ?? ValidateTerms(to, offer.Take, $"у игрока {to.Name}");
        }

        private string? ValidateTerms(Player owner, TradeTerms terms, string whose)
        {
            if (terms.Money < 0 || terms.JailCards < 0)
                return "Неверные условия обмена.";
            if (terms.Money > owner.Balance)
                return $"Столько денег {whose} нет: {owner.Balance} грн.";
            if (terms.JailCards > owner.JailCards)
                return $"Столько карточек «Выйти из тюрьмы» {whose} нет.";
            if (terms.Cells.Distinct().Count() != terms.Cells.Count)
                return "Компания указана дважды.";
            foreach (int index in terms.Cells)
            {
                if (index < 0 || index >= State.Board.Count)
                    return "Нет такой клетки.";
                var cell = State.Board[index];
                if (cell.OwnerId != owner.Id)
                    return $"«{cell.Name}» {whose} нет.";
                if (State.GroupOf(cell).Any(c => c.Level > 0))
                    return $"«{cell.Name}» нельзя менять: в группе стоят филиалы.";
            }
            return null;
        }

        private string? ValidateAnswer(Player player)
        {
            if (State.Phase != TurnPhase.TradeOffer || State.Trade is null)
                return State.Phase == TurnPhase.GameOver ? "Игра окончена." : "Сейчас нет предложений обмена.";
            return State.Trade.ToId == player.Id ? null : "Ответить на обмен может только тот, кому его предложили.";
        }

        private string? ValidateCancel(Player player)
        {
            if (State.Phase != TurnPhase.TradeOffer || State.Trade is null)
                return "Сейчас нет предложений обмена.";
            return State.Trade.FromId == player.Id ? null : "Отозвать обмен может только тот, кто его предложил.";
        }

        private void Propose(Player player, ProposeTrade proposal, List<GameEvent> events)
        {
            // Копии списков: предложение хранится в состоянии и не должно зависеть от чужих объектов.
            var offer = new TradeOffer(player.Id, proposal.TargetId,
                proposal.Give with { Cells = proposal.Give.Cells.ToList() },
                proposal.Take with { Cells = proposal.Take.Cells.ToList() });
            State.Trade = offer;
            events.Add(new TradeProposed(offer));
        }

        private void AcceptOffer(List<GameEvent> events)
        {
            var offer = State.Trade!;
            var from = State.FindPlayer(offer.FromId)!;
            var to = State.FindPlayer(offer.ToId)!;
            Hand(from, to, offer.Give);
            Hand(to, from, offer.Take);
            State.Trade = null;
            events.Add(new TradeAccepted(offer));
        }

        // Заложенные компании переходят заложенными.
        private void Hand(Player giver, Player receiver, TradeTerms terms)
        {
            giver.Balance -= terms.Money;
            receiver.Balance += terms.Money;
            giver.JailCards -= terms.JailCards;
            receiver.JailCards += terms.JailCards;
            foreach (int index in terms.Cells)
                State.Board[index].OwnerId = receiver.Id;
        }
    }
}
