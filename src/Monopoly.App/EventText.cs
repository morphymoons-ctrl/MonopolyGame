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
        // Доска текущей партии (RULES.md, §15). Меняется при старте партии — UseTheme.
        public static BoardTheme Theme { get; private set; } = BoardTheme.Business;
        public static IReadOnlyList<BoardCell> Cells { get; private set; } = Board.CreateDefault();

        // Слова для построек («філія» / «підрозділ») и прочие слова доски.
        public static GameTerms Terms => GameTerms.For(Theme);
        public static ThemeWords Words => ThemeWords.For(Theme);

        public static void UseTheme(BoardTheme theme)
        {
            Theme = theme;
            Cells = Board.Create(theme);
        }

        public static string ThemeName(BoardTheme theme) => ThemeWords.ThemeName(theme);

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
            var words = Words;
            var terms = Terms;

            return e switch
            {
                // Зерно генератора игрокам не показываем: оно хранится в сохранении и нужно только для отладки.
                GameStarted g => $"Партію розпочато. Порядок ходів: {string.Join(", ", g.TurnOrder.Select(Name))}.",
                TurnStarted t => $"Ходить {Name(t.PlayerId)}.",
                TurnSkipped s => s.Reason == SkipReason.Jail
                    ? $"{Name(s.PlayerId)} сидить {words.InJail} {And(words.InJail)} пропускає хід."
                    : $"{Name(s.PlayerId)} {words.RestSkipped}.",
                GameOver g => $"Гру закінчено! Перемога: {Name(g.WinnerId)}.",

                DiceRolled d => $"{Name(d.PlayerId)} кидає кубики: {d.Die1} + {d.Die2} = {d.Total}" + (d.IsDouble ? " — дубль!" : "."),
                RollAgain r => $"{Name(r.PlayerId)} кидає ще раз.",
                PlayerMoved m => $"{Name(m.PlayerId)} переходить на «{Cell(m.To)}».",
                PassedStart p => $"{Name(p.PlayerId)} проходить «{Cell(0)}»: +{Money(p.Amount)}.",
                RestStarted r => $"{Name(r.PlayerId)} {words.RestStarted}.",

                SentToJail j => j.Reason switch
                {
                    JailReason.ThreeDoubles => $"Три дублі поспіль — {Name(j.PlayerId)} вирушає {words.ToJail}.",
                    JailReason.Landed => $"{Name(j.PlayerId)} потрапляє {words.IntoJail}.",
                    _ => $"{Name(j.PlayerId)} вирушає {words.ToJail}.",
                },
                JailCardUsed c => $"{Name(c.PlayerId)} показує картку «{words.JailCard}» — хід не пропускає.",

                PurchaseOffered p => $"«{Cell(p.CellIndex)}» вільна — можна купити за {Money(p.Price)}.",
                PropertyBought b => $"{Name(b.PlayerId)} купує «{Cell(b.CellIndex)}» за {Money(b.Price)}.",
                PurchaseDeclined d => $"{Name(d.PlayerId)} не купує «{Cell(d.CellIndex)}».",
                AuctionStarted a => $"Аукціон: «{Cell(a.CellIndex)}». Ставки від {Money(GameRules.AuctionStep)}, робити їх може кожен.",
                BidPlaced b => $"{Name(b.PlayerId)} ставить {Money(b.Amount)}.",
                AuctionPassed p => $"{Name(p.PlayerId)} пасує.",
                AuctionWon w => $"{Name(w.PlayerId)} виграє аукціон: «{Cell(w.CellIndex)}» за {Money(w.Amount)}.",
                AuctionUnsold u => $"Ніхто не купив «{Cell(u.CellIndex)}» — компанія лишається в банку.",

                RentPaid r => $"{Name(r.PayerId)} платить оренду {Money(r.Amount)} гравцю {Name(r.OwnerId)}.",
                RentSkipped r => $"«{Cell(r.CellIndex)}» закладена — оренди немає.",
                PaidToBank p => $"{Name(p.PlayerId)} платить банку {Money(p.Amount)}.",
                ReceivedFromBank r => $"{Name(r.PlayerId)} отримує від банку {Money(r.Amount)}.",
                PaidToPlayer p => $"{Name(p.FromId)} платить {Money(p.Amount)} гравцю {Name(p.ToId)}.",
                DebtIncurred d => $"{Name(d.DebtorId)}: бракує готівки, борг {Money(d.Amount)} {Creditor(d.CreditorId)}.",
                DebtPaid d => $"{Name(d.DebtorId)} закриває борг {Money(d.Amount)} {Creditor(d.CreditorId)}.",
                PlayerBankrupt b => $"{Name(b.PlayerId)} — банкрут і вибуває з гри. Гроші переходять {Creditor(b.CreditorId)}, а компанії повертаються банку — їх знову можна купити.",

                CasinoOffered c => $"{Name(c.PlayerId)} {words.CasinoOffered}: можна зробити ставку або пройти повз.",
                CasinoPlayed c => c.Multiplier switch
                {
                    0 => $"{Cell(16)}: {Name(c.PlayerId)} програє {Money(c.Bet)}.",
                    1 => $"{Cell(16)}: ставка {Money(c.Bet)} повертається до гравця {Name(c.PlayerId)}.",
                    _ => $"{Cell(16)}: {Name(c.PlayerId)} виграє — ×{c.Multiplier}, виплата {Money(c.Payout)}!",
                },
                ChanceCardDrawn c => $"«{Cell(24)}» для гравця {Name(c.PlayerId)}: {CardText(c.Card)}",

                BranchBuilt b => b.Level == GameRules.HeadOfficeLevel
                    ? $"{Name(b.PlayerId)} відкриває {terms.Office} на «{Cell(b.CellIndex)}» за {Money(b.Cost)}."
                    : $"{Name(b.PlayerId)} відкриває {terms.BranchAccusative} на «{Cell(b.CellIndex)}» за {Money(b.Cost)} (усього {b.Level}).",
                BranchSold s => $"{Name(s.PlayerId)} продає {terms.BranchAccusative} на «{Cell(s.CellIndex)}» за {Money(s.Amount)}.",
                CompanyMortgaged m => $"{Name(m.PlayerId)} закладає «{Cell(m.CellIndex)}» і отримує {Money(m.Amount)}.",
                CompanyRedeemed r => $"{Name(r.PlayerId)} викуповує «{Cell(r.CellIndex)}» за {Money(r.Amount)}.",
                MortgageExpired x => $"{Name(x.PlayerId)} не викупив «{Cell(x.CellIndex)}» за {GameRules.MortgageTurns} ходів — компанія повертається банку, її знову можна купити.",

                TradeProposed t => $"{Name(t.Offer.FromId)} пропонує обмін гравцю {Name(t.Offer.ToId)}: {DescribeOffer(t.Offer, snapshot)}",
                TradeAccepted t => $"{Name(t.Offer.ToId)} погоджується на обмін.",
                TradeRejected t => $"{Name(t.ToId)} відмовляється від обміну.",
                TradeCancelled t => $"{Name(t.FromId)} відкликає пропозицію обміну.",
                _ => e.ToString(),
            };
        }

        // Союз «і» / «й» по правилу милозвучності: после гласной — «й» («у пєтушатні й»), после согласной — «і» («під блокуванням і»).
        private static string And(string before) =>
            before.Length > 0 && "аеєиіїоуюяАЕЄИІЇОУЮЯ".Contains(before[^1]) ? "й" : "і";

        // Срок на выкуп заложенной компании (§10): «ще 7 ходів» или «останній хід».
        public static string MortgageLeft(int turnsLeft) => turnsLeft > 0 ? $"на викуп ходів: {turnsLeft}" : "останній хід на викуп";

        public static string DescribeOffer(TradeOffer offer, GameSnapshot snapshot)
        {
            string Terms(TradeTerms terms)
            {
                var parts = terms.Cells.Select(i => $"«{Cells[i].Name}»").ToList();
                if (terms.Money > 0)
                    parts.Add($"{Money(terms.Money)}");
                if (terms.JailCards > 0)
                    parts.Add(terms.JailCards == 1 ? $"картку «{Words.JailCard}»" : $"картки «{Words.JailCard}» ({terms.JailCards})");
                return parts.Count == 0 ? "нічого" : string.Join(", ", parts);
            }

            return $"віддає {Terms(offer.Give)}, просить {Terms(offer.Take)}.";
        }

        public static string CardText(ChanceCard card) => Theme switch
        {
            BoardTheme.Military => MilitaryCardText(card),
            BoardTheme.Government => GovernmentCardText(card),
            BoardTheme.Crypto => CryptoCardText(card),
            BoardTheme.Games => GamesCardText(card),
            _ => BusinessCardText(card),
        };

        // Карточки «Лутбокса» доски «Відеоігри»: те же действия, другие тексты (§15).
        private static string GamesCardText(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => $"Випав ніж із кейса — продали на ринку Steam: +{CardAmount(card)}.",
            ChanceCard.ProjectBonus => $"Виграли кіберспортивний турнір: +{CardAmount(card)}.",
            ChanceCard.DancerRefund => $"Донат від глядача на стрімі: +{CardAmount(card)}.",
            ChanceCard.Cashback => $"Steam повернув гроші за гру: +{CardAmount(card)}.",
            ChanceCard.Birthday => $"Ви затащили катку — кожен гравець кидає вам {Money(ChanceCards.BirthdayGift)} на скін.",
            ChanceCard.ParkingFine => $"Лаги на сервері, злили рейтинг: −{CardAmount(card)}.",
            ChanceCard.Utilities => $"Нова відеокарта: −{CardAmount(card)}.",
            ChanceCard.Streaming => $"Купили гру на старті, а вона вийшла сирою: −{CardAmount(card)}.",
            ChanceCard.MassageFinish => $"Мама вимкнула роутер посеред катки: −{CardAmount(card)}.",
            ChanceCard.Charity => $"Подарували гру кожному з друзів: заплатіть кожному гравцю {Money(ChanceCards.CharityGift)}.",
            ChanceCard.TaxAudit => $"Рахунки за хостинг: {Money(ChanceCards.AuditPerBranch)} за кожен сервер і {Money(ChanceCards.AuditPerHeadOffice)} — за турнірну арену.",
            ChanceCard.GoToStart => $"Вихід у головне меню: уперед до «{Cells[0].Name}».",
            ChanceCard.NovaPoshta => $"Запросили на стрім: уперед до «{Cells[22].Name}».",
            ChanceCard.Taxi => "До найближчої платформи. Якщо вона чужа — подвійна оренда.",
            ChanceCard.Train => "Відкат сейву: назад на 3 клітинки.",
            ChanceCard.GoToJail => "Античит спрацював: у бан!",
            ChanceCard.GetOutOfJail => "Розбан від модератора. Спрацює сам, коли потрапите в бан, — хід не пропустите. Картку можна віддати в обміні.",
            _ => card.ToString(),
        };

        // Карточки «Твіт Ілона» доски «Криптовалюти»: те же действия, другие тексты (§15).
        private static string CryptoCardText(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => $"Ілон твітнув про ваш коїн: +{CardAmount(card)}.",
            ChanceCard.ProjectBonus => $"Прилетів аірдроп: +{CardAmount(card)}.",
            ChanceCard.DancerRefund => $"Продали NFT з мавпою якомусь диваку: +{CardAmount(card)}.",
            ChanceCard.Cashback => $"Стейкінг приніс відсотки: +{CardAmount(card)}.",
            ChanceCard.Birthday => $"Ви запустили мемкоїн — кожен гравець купує на {Money(ChanceCards.BirthdayGift)}.",
            ChanceCard.ParkingFine => $"Комісія мережі Ethereum знову злетіла: −{CardAmount(card)}.",
            ChanceCard.Utilities => $"Рахунок за світло від майнінг-ферми: −{CardAmount(card)}.",
            ChanceCard.Streaming => $"Купили на хаях: −{CardAmount(card)}.",
            ChanceCard.MassageFinish => $"Відправили USDT не в ту мережу: −{CardAmount(card)}.",
            ChanceCard.Charity => $"Підписались на «сигнали» в Telegram: заплатіть кожному гравцю {Money(ChanceCards.CharityGift)}.",
            ChanceCard.TaxAudit => $"Податкова дізналася про ваш крипто-дохід: {Money(ChanceCards.AuditPerBranch)} за кожну ноду і {Money(ChanceCards.AuditPerHeadOffice)} — за дата-центр.",
            ChanceCard.GoToStart => $"Халвінг! Уперед до «{Cells[0].Name}».",
            ChanceCard.NovaPoshta => $"Втеча в стейбли: уперед до «{Cells[22].Name}».",
            ChanceCard.Taxi => "Терміново треба хешрейт: до найближчої майнінг-ферми. Якщо вона чужа — подвійна оренда.",
            ChanceCard.Train => "Ринок пішов у корекцію: назад на 3 клітинки.",
            ChanceCard.GoToJail => "Біржа заблокувала акаунт до з'ясування!",
            ChanceCard.GetOutOfJail => "Верифікація KYC пройдена. Спрацює сама, коли акаунт заблокують, — хід не пропустите. Картку можна віддати в обміні.",
            _ => card.ToString(),
        };

        // Карточки «Указу» доски «Уряд України»: те же действия, тексты — сатира на публичные мемы (§15).
        private static string GovernmentCardText(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => $"Нацкешбек повернувся з відсотками: +{CardAmount(card)}.",
            ChanceCard.ProjectBonus => $"Премія за «Велике будівництво»: +{CardAmount(card)}.",
            ChanceCard.DancerRefund => $"Вас покликали на телемарафон «Єдині новини» — гонорар: +{CardAmount(card)}.",
            ChanceCard.Cashback => $"«Вовина тисяча», тільки з нулями: +{CardAmount(card)}.",
            ChanceCard.Birthday => $"Єрмак сказав, що «все вирішено»: кожен гравець платить вам {Money(ChanceCards.BirthdayGift)}.",
            ChanceCard.ParkingFine => $"Кличко нагадав: «Не всі можуть дивитися в завтра». Ви не змогли — штраф −{CardAmount(card)}.",
            ChanceCard.Utilities => $"Уряд знову переглянув тарифи: −{CardAmount(card)}.",
            ChanceCard.Streaming => $"Підвищили військовий збір: −{CardAmount(card)}.",
            ChanceCard.MassageFinish => $"Проспали вечірнє звернення президента: −{CardAmount(card)}.",
            ChanceCard.Charity => $"Скидаємося на зйомки «Слуга народу 4»: заплатіть кожному гравцю {Money(ChanceCards.CharityGift)}.",
            ChanceCard.TaxAudit => $"Перевірка НАБУ: {Money(ChanceCards.AuditPerBranch)} за кожен відділ і {Money(ChanceCards.AuditPerHeadOffice)} — за головне управління.",
            ChanceCard.GoToStart => $"Термінове засідання на Банковій: уперед до «{Cells[0].Name}».",
            ChanceCard.NovaPoshta => $"Позачергова нарада в енергетиці: уперед до «{Cells[22].Name}».",
            ChanceCard.Taxi => "Поїхали «на лікування» за кордон: до найближчого КПП. Якщо він чужий — подвійна оренда.",
            ChanceCard.Train => "Реформу знову відклали: назад на 3 клітинки.",
            ChanceCard.GoToJail => "«Весна прийде — саджати будемо»: у СІЗО!",
            ChanceCard.GetOutOfJail => "Помилування від президента. Спрацює саме, коли потрапите в СІЗО, — хід не пропустите. Картку можна віддати в обміні.",
            _ => card.ToString(),
        };

        // Карточки «Наказу» военной доски: те же действия, другие тексты (§15).
        private static string MilitaryCardText(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => $"Допомога від союзників: +{CardAmount(card)}.",
            ChanceCard.ProjectBonus => $"Бойові виплати: +{CardAmount(card)}.",
            ChanceCard.DancerRefund => $"Волонтери закрили збір: +{CardAmount(card)}.",
            ChanceCard.Cashback => $"Премія за влучання: +{CardAmount(card)}.",
            ChanceCard.Birthday => $"День ЗСУ: кожен гравець вітає вас {Money(ChanceCards.BirthdayGift)}.",
            ChanceCard.ParkingFine => $"Штраф за порушення статуту: −{CardAmount(card)}.",
            ChanceCard.Utilities => $"Ремонт техніки: −{CardAmount(card)}.",
            ChanceCard.Streaming => $"Спорядження за власний кошт: −{CardAmount(card)}.",
            ChanceCard.MassageFinish => $"Загублений дрон: −{CardAmount(card)}.",
            ChanceCard.Charity => $"Збір на пікап: заплатіть кожному гравцю {Money(ChanceCards.CharityGift)}.",
            ChanceCard.TaxAudit => $"Інспекція Генштабу: {Money(ChanceCards.AuditPerBranch)} за кожен підрозділ і {Money(ChanceCards.AuditPerHeadOffice)} — за штаб.",
            ChanceCard.GoToStart => $"Повернення до пункту збору: уперед до «{Cells[0].Name}».",
            ChanceCard.NovaPoshta => $"Ешелон: уперед до «{Cells[22].Name}».",
            ChanceCard.Taxi => "Марш до найближчого оперативного командування. Якщо воно чуже — подвійна оренда.",
            ChanceCard.Train => "Тактичний відхід: назад на 3 клітинки.",
            ChanceCard.GoToJail => "На гауптвахту!",
            ChanceCard.GetOutOfJail => "Амністія від командира. Спрацює сама, коли потрапите на гауптвахту, — хід не пропустите. Картку можна віддати в обміні.",
            _ => card.ToString(),
        };

        private static string BusinessCardText(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => $"Повернення ПДВ: +{CardAmount(card)}.",
            ChanceCard.ProjectBonus => $"Премія за проєкт: +{CardAmount(card)}.",
            ChanceCard.DancerRefund => $"Ви сподобались танцівниці і вона повернула вам гроші за приватку: +{CardAmount(card)}.",
            ChanceCard.Cashback => $"Кешбек від Мінібанку: +{CardAmount(card)}.",
            ChanceCard.Birthday => $"День народження: кожен гравець платить вам {Money(ChanceCards.BirthdayGift)}.",
            ChanceCard.ParkingFine => $"Штраф за паркування: −{CardAmount(card)}.",
            ChanceCard.Utilities => $"Оплата комуналки: −{CardAmount(card)}.",
            ChanceCard.Streaming => $"Підписка на всі стримінги: −{CardAmount(card)}.",
            ChanceCard.MassageFinish => $"Мастериця професійно зробила окончаніє, тому −{CardAmount(card)}.",
            ChanceCard.Charity => $"Благодійний марафон: заплатіть кожному гравцю {Money(ChanceCards.CharityGift)}.",
            ChanceCard.TaxAudit => $"Податкова перевірка: {Money(ChanceCards.AuditPerBranch)} за кожну філію і {Money(ChanceCards.AuditPerHeadOffice)} — за головний офіс.",
            ChanceCard.GoToStart => "Вирушайте на «Старт».",
            ChanceCard.NovaPoshta => "Доставка Старою Поштою: уперед до «Стара Пошта».",
            ChanceCard.Taxi => "Таксі до найближчої АЗС. Якщо вона чужа — подвійна оренда.",
            ChanceCard.Train => "Поїздка Укрзалізницею: назад на 3 клітинки.",
            ChanceCard.GoToJail => "Вирушайте до пєтушатні.",
            ChanceCard.GetOutOfJail => "Вийти з пєтушатні безкоштовно. Спрацює сама, коли потрапите в пєтушатню, — хід не пропустите. Картку можна віддати в обміні.",
            _ => card.ToString(),
        };

        // Сумма карточки «Шанса» без знака — знак стоит в тексте.
        private static string CardAmount(ChanceCard card) => Money(Math.Abs(ChanceCards.BankAmount(card)));

        // Суммы — в валюте доски партии (§15): гривны, на «Криптовалютах» — доллары.
        public static string Money(int amount) => GameRules.Money(amount, Theme);

        public static string ShortMoney(int amount) => GameRules.ShortMoney(amount, Theme);

        // Ставки казино для подсказок: «50 000 грн – 300 000 грн» или «$50 000 – $300 000».
        public static string CasinoRange => $"{Money(GameRules.CasinoBets[0])} – {Money(GameRules.CasinoBets[^1])}";

        // Подписи кнопок для действий без выбора клетки.
        public static string ActionLabel(GameAction action, GameSnapshot snapshot) => action switch
        {
            RollDice => snapshot.LastRoll is null ? "Кинути кубики" : "Кинути ще раз",
            BuyProperty => snapshot.PendingPurchase is int cell ? $"Купити за {Money(Cells[cell].Price)}" : "Купити",
            DeclinePurchase => "Не купувати (аукціон)",
            EndTurn => "Завершити хід",
            PlayCasino c => $"{Cells[16].Name}: ставка {Money(c.Bet)}",
            PassAuction => "Пас",
            AcceptTrade => "Прийняти обмін",
            RejectTrade => "Відмовитися",
            CancelTrade => "Відкликати обмін",
            DeclareBankruptcy => "Оголосити банкрутство",
            _ => action.GetType().Name,
        };

        public static string GroupName(CellType type) => Theme switch
        {
            BoardTheme.Military => MilitaryGroupName(type),
            BoardTheme.Government => GovernmentGroupName(type),
            BoardTheme.Crypto => CryptoGroupName(type),
            BoardTheme.Games => GamesGroupName(type),
            _ => BusinessGroupName(type),
        };

        private static string GamesGroupName(CellType type) => type switch
        {
            CellType.Supermarket => "Мобільні ігри",
            CellType.GasStation => "Платформи",
            CellType.Factory => "Шутери",
            CellType.TV => "Інді-ігри",
            CellType.Food => "Українські хіти",
            CellType.Nightlife => "Пісочниці",
            CellType.Logistics => "Стрімінг",
            CellType.Bank => "Королівські битви",
            CellType.NetworkShop => "Легенди",
            _ => "",
        };

        private static string CryptoGroupName(CellType type) => type switch
        {
            CellType.Supermarket => "Мемкоїни",
            CellType.GasStation => "Майнінг-ферми",
            CellType.Factory => "Біржі",
            CellType.TV => "Гаманці",
            CellType.Food => "Блокчейни",
            CellType.Nightlife => "NFT-колекції",
            CellType.Logistics => "Стейблкоїни",
            CellType.Bank => "DeFi",
            CellType.NetworkShop => "Королі ринку",
            _ => "",
        };

        private static string GovernmentGroupName(CellType type) => type switch
        {
            CellType.Supermarket => "Правоохоронці",
            CellType.GasStation => "Пункти пропуску",
            CellType.Factory => "Спецслужби",
            CellType.TV => "Антикорупційні органи",
            CellType.Food => "Суди",
            CellType.Nightlife => "Фіскальні служби",
            CellType.Logistics => "Держкомпанії",
            CellType.Bank => "Цифрові сервіси",
            CellType.NetworkShop => "Вища влада",
            _ => "",
        };

        private static string MilitaryGroupName(CellType type) => type switch
        {
            CellType.Supermarket => "Сухопутні війська",
            CellType.GasStation => "Оперативні командування",
            CellType.Factory => "Оборонна промисловість",
            CellType.TV => "Зв'язок і розвідка",
            CellType.Food => "Артилерія",
            CellType.Nightlife => "Безпілотні системи",
            CellType.Logistics => "Тилове забезпечення",
            CellType.Bank => "Військово-морські сили",
            CellType.NetworkShop => "Протиповітряна оборона",
            _ => "",
        };

        private static string BusinessGroupName(CellType type) => type switch
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
