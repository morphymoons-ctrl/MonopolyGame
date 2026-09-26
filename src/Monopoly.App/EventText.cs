using System;
using System.Collections.Generic;
using System.Linq;
using Monopoly.Core;

namespace Monopoly.App
{
    // Тексты для журнала, кнопок и карточек «Шанса». Язык игры — украинский.
    // Глаголы в настоящем времени: у них нет рода, фраза подходит любому игроку.
    public static class EventText
    {
        public static readonly IReadOnlyList<BoardCell> Cells = Board.CreateDefault();

        // Имя игрока в тексте журнала помечается: NameStart, Id, NameSplit, имя, NameEnd.
        // Журнал по этим меткам красит имя в цвет фишки (GameManager.AddLog).
        public const char NameStart = '', NameSplit = '', NameEnd = '';

        public static string TagName(int id, string name) => $"{NameStart}{id}{NameSplit}{name}{NameEnd}";

        // Текст события для журнала — с помеченными именами игроков.
        public static string Describe(GameEvent e, GameSnapshot snapshot)
        {
            string Name(int id) => TagName(id, snapshot.FindPlayer(id)?.Name ?? $"Гравець #{id}");
            string Cell(int index) => Cells[index].Name;
            string Creditor(int? id) => id is int c ? $"гравцю {Name(c)}" : "банку";

            return e switch
            {
                // Зерно генератора игрокам не показываем: оно хранится в сохранении и нужно только для отладки.
                GameStarted g => $"Партію розпочато. Порядок ходів: {string.Join(", ", g.TurnOrder.Select(Name))}.",
                TurnStarted t => $"Ходить {Name(t.PlayerId)}.",
                TurnSkipped s => $"{Name(s.PlayerId)} пропускає хід («Зачілься»).",
                GameOver g => $"Гру закінчено! Перемога: {Name(g.WinnerId)}.",

                DiceRolled d => $"{Name(d.PlayerId)} кидає кубики: {d.Die1} + {d.Die2} = {d.Total}" + (d.IsDouble ? " — дубль!" : "."),
                RollAgain r => $"{Name(r.PlayerId)} кидає ще раз.",
                PlayerMoved m => $"{Name(m.PlayerId)} переходить на «{Cell(m.To)}».",
                PassedStart p => $"{Name(p.PlayerId)} проходить «Старт»: +{GameRules.Money(p.Amount)}.",
                RestStarted r => $"{Name(r.PlayerId)} чілить і пропустить наступний хід.",

                SentToJail j => j.Reason == JailReason.ThreeDoubles
                    ? $"Три дублі поспіль — {Name(j.PlayerId)} вирушає до пєтушатні."
                    : $"{Name(j.PlayerId)} вирушає до пєтушатні.",
                JailRollFailed f => $"{Name(f.PlayerId)} не викидає дубль і лишається у пєтушатні (спроба {f.Attempt} з {GameRules.MaxJailAttempts}).",
                LeftJail l => l.How switch
                {
                    JailExit.Double => $"{Name(l.PlayerId)} викидає дубль і виходить з пєтушатні.",
                    JailExit.Bail => $"{Name(l.PlayerId)} платить заставу й виходить з пєтушатні.",
                    JailExit.Card => $"{Name(l.PlayerId)} виходить з пєтушатні за карткою.",
                    _ => $"Третя спроба — {Name(l.PlayerId)} мусить заплатити заставу й виходить з пєтушатні.",
                },

                PurchaseOffered p => $"«{Cell(p.CellIndex)}» вільна — можна купити за {GameRules.Money(p.Price)}.",
                PropertyBought b => $"{Name(b.PlayerId)} купує «{Cell(b.CellIndex)}» за {GameRules.Money(b.Price)}.",
                PurchaseDeclined d => $"{Name(d.PlayerId)} не купує «{Cell(d.CellIndex)}».",
                AuctionStarted a => $"Аукціон: «{Cell(a.CellIndex)}». Ставки від {GameRules.Money(GameRules.AuctionStep)}, робити їх може кожен.",
                BidPlaced b => $"{Name(b.PlayerId)} ставить {GameRules.Money(b.Amount)}.",
                AuctionPassed p => $"{Name(p.PlayerId)} пасує.",
                AuctionWon w => $"{Name(w.PlayerId)} виграє аукціон: «{Cell(w.CellIndex)}» за {GameRules.Money(w.Amount)}.",
                AuctionUnsold u => $"Ніхто не купив «{Cell(u.CellIndex)}» — компанія лишається в банку.",

                RentPaid r => $"{Name(r.PayerId)} платить оренду {GameRules.Money(r.Amount)} гравцю {Name(r.OwnerId)}.",
                RentSkipped r => $"«{Cell(r.CellIndex)}» закладена — оренди немає.",
                PaidToBank p => $"{Name(p.PlayerId)} платить банку {GameRules.Money(p.Amount)}.",
                ReceivedFromBank r => $"{Name(r.PlayerId)} отримує від банку {GameRules.Money(r.Amount)}.",
                PaidToPlayer p => $"{Name(p.FromId)} платить {GameRules.Money(p.Amount)} гравцю {Name(p.ToId)}.",
                DebtIncurred d => $"{Name(d.DebtorId)}: бракує готівки, борг {GameRules.Money(d.Amount)} {Creditor(d.CreditorId)}.",
                DebtPaid d => $"{Name(d.DebtorId)} закриває борг {GameRules.Money(d.Amount)} {Creditor(d.CreditorId)}.",
                PlayerBankrupt b => $"{Name(b.PlayerId)} — банкрут і вибуває з гри. Майно переходить {Creditor(b.CreditorId)}.",

                CasinoOffered c => $"{Name(c.PlayerId)} у казино: можна зробити ставку або пройти повз.",
                CasinoPlayed c => c.Multiplier switch
                {
                    0 => $"Казино: {Name(c.PlayerId)} програє {GameRules.Money(c.Bet)}.",
                    1 => $"Казино: ставка {GameRules.Money(c.Bet)} повертається до гравця {Name(c.PlayerId)}.",
                    _ => $"Казино: {Name(c.PlayerId)} виграє — ×{c.Multiplier}, виплата {GameRules.Money(c.Payout)}!",
                },
                ChanceCardDrawn c => $"«Шанс» для гравця {Name(c.PlayerId)}: {CardText(c.Card)}",

                BranchBuilt b => b.Level == GameRules.HeadOfficeLevel
                    ? $"{Name(b.PlayerId)} відкриває головний офіс на «{Cell(b.CellIndex)}» за {GameRules.Money(b.Cost)}."
                    : $"{Name(b.PlayerId)} відкриває філію на «{Cell(b.CellIndex)}» за {GameRules.Money(b.Cost)} (усього {b.Level}).",
                BranchSold s => $"{Name(s.PlayerId)} продає філію на «{Cell(s.CellIndex)}» за {GameRules.Money(s.Amount)}.",
                CompanyMortgaged m => $"{Name(m.PlayerId)} закладає «{Cell(m.CellIndex)}» і отримує {GameRules.Money(m.Amount)}.",
                CompanyRedeemed r => $"{Name(r.PlayerId)} викуповує «{Cell(r.CellIndex)}» за {GameRules.Money(r.Amount)}.",

                TradeProposed t => $"{Name(t.Offer.FromId)} пропонує обмін гравцю {Name(t.Offer.ToId)}: {DescribeOffer(t.Offer, snapshot)}",
                TradeAccepted t => $"{Name(t.Offer.ToId)} погоджується на обмін.",
                TradeRejected t => $"{Name(t.ToId)} відмовляється від обміну.",
                TradeCancelled t => $"{Name(t.FromId)} відкликає пропозицію обміну.",
                _ => e.ToString(),
            };
        }

        public static string DescribeOffer(TradeOffer offer, GameSnapshot snapshot)
        {
            string Terms(TradeTerms terms)
            {
                var parts = terms.Cells.Select(i => $"«{Cells[i].Name}»").ToList();
                if (terms.Money > 0)
                    parts.Add($"{GameRules.Money(terms.Money)}");
                if (terms.JailCards > 0)
                    parts.Add(terms.JailCards == 1 ? "картку «Вийти з пєтушатні»" : $"картки «Вийти з пєтушатні» ({terms.JailCards})");
                return parts.Count == 0 ? "нічого" : string.Join(", ", parts);
            }

            return $"віддає {Terms(offer.Give)}, просить {Terms(offer.Take)}.";
        }

        public static string CardText(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => $"Повернення ПДВ: +{CardAmount(card)}.",
            ChanceCard.ProjectBonus => $"Премія за проєкт: +{CardAmount(card)}.",
            ChanceCard.DancerRefund => $"Ви сподобались танцівниці і вона повернула вам гроші за приватку: +{CardAmount(card)}.",
            ChanceCard.Cashback => $"Кешбек від Мінібанку: +{CardAmount(card)}.",
            ChanceCard.Birthday => $"День народження: кожен гравець платить вам {GameRules.Money(ChanceCards.BirthdayGift)}.",
            ChanceCard.ParkingFine => $"Штраф за паркування: −{CardAmount(card)}.",
            ChanceCard.Utilities => $"Оплата комуналки: −{CardAmount(card)}.",
            ChanceCard.Streaming => $"Підписка на всі стримінги: −{CardAmount(card)}.",
            ChanceCard.MassageFinish => $"Мастериця професійно зробила окончаніє, тому −{CardAmount(card)}.",
            ChanceCard.Charity => $"Благодійний марафон: заплатіть кожному гравцю {GameRules.Money(ChanceCards.CharityGift)}.",
            ChanceCard.TaxAudit => $"Податкова перевірка: {GameRules.Money(ChanceCards.AuditPerBranch)} за кожну філію і {GameRules.Money(ChanceCards.AuditPerHeadOffice)} — за головний офіс.",
            ChanceCard.GoToStart => "Вирушайте на «Старт».",
            ChanceCard.NovaPoshta => "Доставка Старою Поштою: уперед до «Стара Пошта».",
            ChanceCard.Taxi => "Таксі до найближчої АЗС. Якщо вона чужа — подвійна оренда.",
            ChanceCard.Train => "Поїздка Укрзалізницею: назад на 3 клітинки.",
            ChanceCard.GoToJail => "Вирушайте до пєтушатні.",
            ChanceCard.GetOutOfJail => "Вийти з пєтушатні безкоштовно. Картку можна зберегти або віддати в обміні.",
            _ => card.ToString(),
        };

        // Сумма карточки «Шанса» без знака — знак стоит в тексте.
        private static string CardAmount(ChanceCard card) => GameRules.Money(Math.Abs(ChanceCards.BankAmount(card)));

        // Ставки казино для подсказок: «50 000–300 000 грн».
        public static string CasinoRange =>
            $"{GameRules.CasinoBets[0].ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("uk-UA"))}–{GameRules.Money(GameRules.CasinoBets[^1])}";

        // Подписи кнопок для действий без выбора клетки.
        public static string ActionLabel(GameAction action, GameSnapshot snapshot) => action switch
        {
            RollDice => snapshot.LastRoll is null ? "Кинути кубики" : "Кинути ще раз",
            BuyProperty => snapshot.PendingPurchase is int cell ? $"Купити за {GameRules.Money(Cells[cell].Price)}" : "Купити",
            DeclinePurchase => "Не купувати (аукціон)",
            EndTurn => "Завершити хід",
            PayBail => $"Застава {GameRules.Money(GameRules.BailAmount)}",
            UseJailCard => "Картка «Вийти з пєтушатні»",
            PlayCasino c => $"Казино: ставка {GameRules.Money(c.Bet)}",
            PassAuction => "Пас",
            AcceptTrade => "Прийняти обмін",
            RejectTrade => "Відмовитися",
            CancelTrade => "Відкликати обмін",
            DeclareBankruptcy => "Оголосити банкрутство",
            _ => action.GetType().Name,
        };

        public static string GroupName(CellType type) => type switch
        {
            CellType.Supermarket => "Супермаркети",
            CellType.GasStation => "АЗС",
            CellType.Factory => "Заводи",
            CellType.TV => "Телеканали",
            CellType.Food => "Їжа",
            CellType.Nightlife => "Нічні заклади",
            CellType.Logistics => "Логістика",
            CellType.Bank => "Банки",
            CellType.NetworkShop => "Мережеві магазини",
            _ => "",
        };
    }
}
