namespace Monopoly.Core
{
    // Колода «Шанса» (RULES.md, §9). Тексты карточек — в интерфейсе.
    public enum ChanceCard
    {
        TaxRefund,      // Возврат НДС: +150
        ProjectBonus,   // Премия за проект: +100
        DancerRefund,   // Танцовщица вернула деньги за приватный танец: +60
        Cashback,       // Кэшбэк Мінібанка: +50
        Birthday,       // День рождения: каждый платит вам 20
        ParkingFine,    // Штраф за парковку: −50
        Utilities,      // Оплата коммуналки: −80
        Streaming,      // Подписка на все стриминги: −100
        MassageFinish,  // Мастерица в «Масажке»: −50
        Charity,        // Благотворительный марафон: вы платите каждому 25
        TaxAudit,       // Налоговая проверка: 25 за филиал, 100 за головной офис
        GoToStart,      // Отправляйтесь на Старт
        NovaPoshta,     // Доставка Новой почтой: вперёд до «Стара Пошта»
        Taxi,           // Такси до ближайшей АЗС: чужая — двойная аренда
        Train,          // Поездка Укрзализныцей: назад на 3 клетки
        GoToJail,       // Отправляйтесь в тюрьму
        GetOutOfJail,   // Выйти из тюрьмы бесплатно
    }

    public static class ChanceCards
    {
        public const int BirthdayGift = 20_000;
        public const int CharityGift = 25_000;
        public const int AuditPerBranch = 25_000;
        public const int AuditPerHeadOffice = 100_000;
        public const int TrainStepsBack = 3;
        public const int NovaPoshtaCell = 22;

        // Сколько банк платит игроку (плюс) или игрок банку (минус).
        public static int BankAmount(ChanceCard card) => card switch
        {
            ChanceCard.TaxRefund => 150_000,
            ChanceCard.ProjectBonus => 100_000,
            ChanceCard.DancerRefund => 60_000,
            ChanceCard.Cashback => 50_000,
            ChanceCard.ParkingFine => -50_000,
            ChanceCard.Utilities => -80_000,
            ChanceCard.Streaming => -100_000,
            ChanceCard.MassageFinish => -50_000,
            _ => 0,
        };
    }
}
