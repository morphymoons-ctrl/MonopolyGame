namespace Monopoly.Core
{
    // Обмен между игроками (RULES.md, §11).
    public partial class Game
    {
        // Кто сейчас может предложить обмен: текущий игрок в свой ход или должник, пока висит его долг — даже не в свой ход.
        private string? ValidateTradeTiming(Player player) =>
            State.Phase == TurnPhase.Debt && State.Debts[0].DebtorId == player.Id
                ? null
                : RequireTurn(player, TurnPhase.AwaitingRoll, TurnPhase.Manage);

        private string? ValidateProposal(Player player, ProposeTrade proposal)
        {
            var error = ValidateTradeTiming(player);
            if (error is not null)
                return error;
            var target = State.FindPlayer(proposal.TargetId);
            if (target is null || target.IsBankrupt || target == player)
                return "Оберіть, з ким мінятися.";
            return ValidateOffer(new TradeOffer(player.Id, target.Id, proposal.Give, proposal.Take));
        }

        // Проверяется и при предложении, и при согласии.
        private string? ValidateOffer(TradeOffer offer)
        {
            if (offer.Give is null || offer.Take is null || offer.Give.Cells is null || offer.Take.Cells is null)
                return "Неправильні умови обміну.";
            if (offer.Give.IsEmpty && offer.Take.IsEmpty)
                return "Обмін порожній: додайте компанії, гроші або картки.";
            var from = State.FindPlayer(offer.FromId)!;
            var to = State.FindPlayer(offer.ToId)!;
            return ValidateTerms(from, offer.Give, "у вас") ?? ValidateTerms(to, offer.Take, $"у гравця {to.Name}");
        }

        private string? ValidateTerms(Player owner, TradeTerms terms, string whose)
        {
            if (terms.Money < 0 || terms.JailCards < 0)
                return "Неправильні умови обміну.";
            // Суммы в игре круглые (§5, §11): до сотен, от миллиона — до сотен тысяч.
            if (terms.Money != GameRules.RoundMoney(terms.Money))
                return $"Сума в обміні має бути круглою, наприклад {Money(GameRules.RoundMoney(terms.Money))}.";
            if (terms.Money > owner.Balance)
                return $"Стільки грошей {whose} немає: {Money(owner.Balance)}.";
            if (terms.JailCards > owner.JailCards)
                return $"Стільки карток звільнення {whose} немає.";
            if (terms.Cells.Distinct().Count() != terms.Cells.Count)
                return "Компанію вказано двічі.";
            foreach (int index in terms.Cells)
            {
                if (index < 0 || index >= State.Board.Count)
                    return "Немає такої клітинки.";
                var cell = State.Board[index];
                if (cell.OwnerId != owner.Id)
                    return $"«{cell.Name}» {whose} немає.";
                if (State.GroupOf(cell).Any(c => c.Level > 0))
                    return $"«{cell.Name}» не можна обміняти: у групі є {State.Terms.Branches}.";
            }
            return null;
        }

        private string? ValidateAnswer(Player player)
        {
            if (State.Phase != TurnPhase.TradeOffer || State.Trade is null)
                return State.Phase == TurnPhase.GameOver ? "Гру закінчено." : "Зараз немає пропозицій обміну.";
            return State.Trade.ToId == player.Id ? null : "Відповісти на обмін може лише той, кому його запропонували.";
        }

        // Встречное предложение (§11): только тот, кому предложили, пока торг не исчерпан; условия — как у обычного обмена.
        private string? ValidateCounter(Player player, CounterTrade counter)
        {
            var error = ValidateAnswer(player);
            if (error is not null)
                return error;
            if (State.Trade!.Counters >= GameRules.MaxTradeCounters)
                return $"Змінювати умови можна не більше {GameRules.MaxTradeCounters} разів — прийміть обмін або відмовтеся.";
            return ValidateOffer(new TradeOffer(player.Id, State.Trade.FromId, counter.Give, counter.Take));
        }

        // Можно ли сейчас изменить условия — для кнопки «Змінити умови».
        private bool CanCounter(Player player) =>
            ValidateAnswer(player) is null && State.Trade!.Counters < GameRules.MaxTradeCounters;

        private void Counter(Player player, CounterTrade counter, List<GameEvent> events)
        {
            var old = State.Trade!;
            var offer = new TradeOffer(player.Id, old.FromId,
                counter.Give with { Cells = counter.Give.Cells.ToList() },
                counter.Take with { Cells = counter.Take.Cells.ToList() },
                old.Counters + 1);
            State.Trade = offer;
            events.Add(new TradeCountered(offer));
        }

        private string? ValidateCancel(Player player)
        {
            if (State.Phase != TurnPhase.TradeOffer || State.Trade is null)
                return "Зараз немає пропозицій обміну.";
            return State.Trade.FromId == player.Id ? null : "Відкликати обмін може лише той, хто його запропонував.";
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
