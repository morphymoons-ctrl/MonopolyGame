using System.Collections.Generic;
using System.Linq;
using Monopoly.Core;

namespace Monopoly.App
{
    // Тексты для журнала, кнопок и карточек «Шанса».
    public static class EventText
    {
        public static readonly IReadOnlyList<BoardCell> Cells = Board.CreateDefault();

        public static string Describe(GameEvent e, GameSnapshot snapshot)
        {
            string Name(int id) => snapshot.FindPlayer(id)?.Name ?? $"Игрок #{id}";
            string Cell(int index) => Cells[index].Name;
            string Creditor(int? id) => id is int c ? $"игроку {Name(c)}" : "банку";

            return e switch
            {
                GameStarted g => $"Партия началась. Порядок ходов: {string.Join(", ", g.TurnOrder.Select(Name))}."
                    + (g.Seed is int seed ? $" Зерно: {seed}." : ""),
                TurnStarted t => $"Ходит {Name(t.PlayerId)}.",
                TurnSkipped s => $"{Name(s.PlayerId)} пропускает ход (отдых).",
                GameOver g => $"Игра окончена! Победил {Name(g.WinnerId)}.",

                DiceRolled d => $"{Name(d.PlayerId)} бросил кубики: {d.Die1} + {d.Die2} = {d.Total}" + (d.IsDouble ? " — дубль!" : "."),
                RollAgain r => $"{Name(r.PlayerId)} бросает ещё раз.",
                PlayerMoved m => $"{Name(m.PlayerId)} переходит на «{Cell(m.To)}».",
                PassedStart p => $"{Name(p.PlayerId)} проходит «Старт»: +{p.Amount} грн.",
                RestStarted r => $"{Name(r.PlayerId)} отдыхает и пропустит следующий ход.",

                SentToJail j => j.Reason == JailReason.ThreeDoubles
                    ? $"Три дубля подряд — {Name(j.PlayerId)} отправляется в тюрьму."
                    : $"{Name(j.PlayerId)} отправляется в тюрьму.",
                JailRollFailed f => $"{Name(f.PlayerId)} не выбросил дубль и остаётся в тюрьме (попытка {f.Attempt} из {GameRules.MaxJailAttempts}).",
                LeftJail l => l.How switch
                {
                    JailExit.Double => $"{Name(l.PlayerId)} выбросил дубль и выходит из тюрьмы.",
                    JailExit.Bail => $"{Name(l.PlayerId)} платит залог и выходит из тюрьмы.",
                    JailExit.Card => $"{Name(l.PlayerId)} выходит из тюрьмы по карточке.",
                    _ => $"Третья попытка — {Name(l.PlayerId)} обязан заплатить залог и выходит из тюрьмы.",
                },

                PurchaseOffered p => $"«{Cell(p.CellIndex)}» свободна — можно купить за {p.Price} грн.",
                PropertyBought b => $"{Name(b.PlayerId)} купил «{Cell(b.CellIndex)}» за {b.Price} грн.",
                PurchaseDeclined d => $"{Name(d.PlayerId)} не стал покупать «{Cell(d.CellIndex)}».",
                AuctionStarted a => $"Аукцион: «{Cell(a.CellIndex)}». Ставки от {GameRules.AuctionStep} грн, делать их может каждый.",
                BidPlaced b => $"{Name(b.PlayerId)} ставит {b.Amount} грн.",
                AuctionPassed p => $"{Name(p.PlayerId)} пасует.",
                AuctionWon w => $"{Name(w.PlayerId)} выигрывает аукцион: «{Cell(w.CellIndex)}» за {w.Amount} грн.",
                AuctionUnsold u => $"Никто не купил «{Cell(u.CellIndex)}» — компания остаётся у банка.",

                RentPaid r => $"{Name(r.PayerId)} платит аренду {r.Amount} грн игроку {Name(r.OwnerId)}.",
                RentSkipped r => $"«{Cell(r.CellIndex)}» заложена — аренды нет.",
                PaidToBank p => $"{Name(p.PlayerId)} платит банку {p.Amount} грн.",
                ReceivedFromBank r => $"{Name(r.PlayerId)} получает от банка {r.Amount} грн.",
                PaidToPlayer p => $"{Name(p.FromId)} платит {p.Amount} грн игроку {Name(p.ToId)}.",
                DebtIncurred d => $"{Name(d.DebtorId)} не хватает наличных: долг {d.Amount} грн {Creditor(d.CreditorId)}.",
                DebtPaid d => $"{Name(d.DebtorId)} закрывает долг {d.Amount} грн {Creditor(d.CreditorId)}.",
                PlayerBankrupt b => $"{Name(b.PlayerId)} — банкрот и выбывает из игры. Имущество переходит {Creditor(b.CreditorId)}.",

                CasinoOffered c => $"{Name(c.PlayerId)} в казино: можно сделать ставку или пройти мимо.",
                CasinoPlayed c => c.Multiplier switch
                {
                    0 => $"Казино: {Name(c.PlayerId)} проигрывает {c.Bet} грн.",
                    1 => $"Казино: ставка {c.Bet} грн возвращается к игроку {Name(c.PlayerId)}.",
                    _ => $"Казино: {Name(c.PlayerId)} выигрывает — ×{c.Multiplier}, выплата {c.Payout} грн!",
                },
                ChanceCardDrawn c => $"«Шанс» для игрока {Name(c.PlayerId)}: {CardText(c.Card)}",

                BranchBuilt b => b.Level == GameRules.HeadOfficeLevel
                    ? $"{Name(b.PlayerId)} открывает головной офис на «{Cell(b.CellIndex)}» за {b.Cost} грн."
                    : $"{Name(b.PlayerId)} открывает филиал на «{Cell(b.CellIndex)}» за {b.Cost} грн (всего {b.Level}).",
                BranchSold s => $"{Name(s.PlayerId)} продаёт филиал на «{Cell(s.CellIndex)}» за {s.Amount} грн.",
                CompanyMortgaged m => $"{Name(m.PlayerId)} закладывает «{Cell(m.CellIndex)}» и получает {m.Amount} грн.",
                CompanyRedeemed r => $"{Name(r.PlayerId)} выкупает «{Cell(r.CellIndex)}» за {r.Amount} грн.",

                TradeProposed t => $"{Name(t.Offer.FromId)} предлагает обмен игроку {Name(t.Offer.ToId)}: {DescribeOffer(t.Offer, snapshot)}",
                TradeAccepted t => $"{Name(t.Offer.ToId)} соглашается на обмен.",
                TradeRejected t => $"{Name(t.ToId)} отказывается от обмена.",
                TradeCancelled t => $"{Name(t.FromId)} отзывает предложение обмена.",
                _ => e.ToString(),
            };
        }

        public static string DescribeOffer(TradeOffer offer, GameSnapshot snapshot)
        {
            string Terms(TradeTerms terms)
            {
                var parts = terms.Cells.Select(i => $"«{Cells[i].Name}»").ToList();
                if (terms.Money > 0)
                    parts.Add($"{terms.Money} грн");
                if (terms.JailCards > 0)
                    parts.Add(terms.JailCards == 1 ? "карточку «Выйти из тюрьмы»" : $"карточки «Выйти из тюрьмы» ({terms.JailCards})");
                return parts.Count == 0 ? "ничего" : string.Join(", ", parts);
            }

            return $"отдаёт {Terms(offer.Give)}, просит {Terms(offer.Take)}.";
        }

        public static string CardText(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => "Возврат НДС: +150 грн.",
            ChanceCard.ProjectBonus => "Премия за проект: +100 грн.",
            ChanceCard.SoldLaptop => "Продали старый ноутбук на ОЛХ: +60 грн.",
            ChanceCard.Cashback => "Кэшбэк Монобанка: +50 грн.",
            ChanceCard.Birthday => $"День рождения: каждый игрок платит вам {ChanceCards.BirthdayGift} грн.",
            ChanceCard.ParkingFine => "Штраф за парковку: −50 грн.",
            ChanceCard.Utilities => "Оплата коммуналки: −80 грн.",
            ChanceCard.Streaming => "Подписка на все стриминги: −100 грн.",
            ChanceCard.Charity => $"Благотворительный марафон: заплатите каждому игроку {ChanceCards.CharityGift} грн.",
            ChanceCard.TaxAudit => $"Налоговая проверка: {ChanceCards.AuditPerBranch} грн за каждый филиал и {ChanceCards.AuditPerHeadOffice} за головной офис.",
            ChanceCard.GoToStart => "Отправляйтесь на «Старт».",
            ChanceCard.NovaPoshta => "Доставка Новой почтой: вперёд до «Нова пошта».",
            ChanceCard.Taxi => "Такси до ближайшей АЗС. Если она чужая — двойная аренда.",
            ChanceCard.Train => "Поездка Укрзализныцей: назад на 3 клетки.",
            ChanceCard.GoToJail => "Отправляйтесь в тюрьму.",
            ChanceCard.GetOutOfJail => "Выйти из тюрьмы бесплатно. Карточку можно сохранить или отдать в обмене.",
            _ => card.ToString(),
        };

        // Подписи кнопок для действий без выбора клетки.
        public static string ActionLabel(GameAction action, GameSnapshot snapshot) => action switch
        {
            RollDice => snapshot.LastRoll is null ? "Бросить кубики" : "Бросить ещё раз",
            BuyProperty => snapshot.PendingPurchase is int cell ? $"Купить за {Cells[cell].Price} грн" : "Купить",
            DeclinePurchase => "Не покупать (аукцион)",
            EndTurn => "Завершить ход",
            PayBail => $"Залог {GameRules.BailAmount} грн",
            UseJailCard => "Карточка «Выйти из тюрьмы»",
            PlayCasino c => $"Казино: ставка {c.Bet}",
            PassAuction => "Пас",
            AcceptTrade => "Принять обмен",
            RejectTrade => "Отказаться",
            CancelTrade => "Отозвать обмен",
            DeclareBankruptcy => "Объявить банкротство",
            _ => action.GetType().Name,
        };

        public static string GroupName(CellType type) => type switch
        {
            CellType.Supermarket => "Супермаркеты",
            CellType.GasStation => "АЗС",
            CellType.Factory => "Заводы",
            CellType.TV => "ТВ",
            CellType.Food => "Еда",
            CellType.OnlineShop => "Онлайн-магазины",
            CellType.Logistics => "Логистика",
            CellType.Bank => "Банки",
            CellType.NetworkShop => "Сетевые магазины",
            _ => "",
        };
    }
}
