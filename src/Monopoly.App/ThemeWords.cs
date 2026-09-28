using Monopoly.Core;

namespace Monopoly.App
{
    // Фразы, которые зависят от доски (RULES.md, §15) и не сводятся к названию клетки: предлоги и падежи разные.
    // Названия самих клеток («Казино», «Шанс», «Старт») берутся с доски — Board.Create.
    // Файл общий с панелью администратора (Monopoly.Admin подключает его ссылкой).
    public sealed record ThemeWords(
        string InJail,        // «сидить у пєтушатні» / «сидить на гауптвахті»
        string ToJail,        // «вирушає до пєтушатні» / «вирушає на гауптвахту»
        string IntoJail,      // «потрапляє в пєтушатню» / «потрапляє на гауптвахту»
        string FromJail,      // «випустити з пєтушатні» / «випустити з гауптвахти»
        string JailCard,      // название карточки освобождения
        string RestSkipped,   // «… пропускає хід («Зачілься»)»
        string RestStarted,   // «… чілить і пропустить наступний хід»
        string RestNote,      // пометка у игрока: «чілить» / «у відпустці»
        string CasinoOffered) // «… у казино» / «… сідає за карти в бліндажі»
    {
        public static readonly ThemeWords Business = new(
            "у пєтушатні", "до пєтушатні", "в пєтушатню", "з пєтушатні", "Вийти з пєтушатні",
            "пропускає хід («Зачілься»)", "чілить і пропустить наступний хід", "чілить",
            "у казино");

        public static readonly ThemeWords Military = new(
            "на гауптвахті", "на гауптвахту", "на гауптвахту", "з гауптвахти", "Амністія від командира",
            "у відпустці й пропускає хід", "їде у відпустку й пропустить наступний хід", "у відпустці",
            "сідає за карти в бліндажі");

        public static readonly ThemeWords Government = new(
            "у СІЗО", "до СІЗО", "у СІЗО", "із СІЗО", "Помилування від президента",
            "у закордонному відрядженні й пропускає хід", "їде у закордонне відрядження й пропустить наступний хід", "у відрядженні",
            "тисне кнопки в Раді");

        public static readonly ThemeWords Crypto = new(
            "під блокуванням", "під блокування", "під блокування", "з-під блокування", "Верифікація KYC",
            "холдить і пропускає хід (HODL)", "іде в HODL і пропустить наступний хід", "холдить",
            "відкриває позицію з плечем");

        public static readonly ThemeWords Games = new(
            "у бані", "у бан", "у бан", "з бану", "Розбан від модератора",
            "в AFK і пропускає хід", "іде в AFK і пропустить наступний хід", "AFK",
            "відкриває кейси в CS");

        public static readonly ThemeWords Oligarchs = new(
            "під санкціями", "під санкції", "під санкції", "з-під санкцій", "Зняття санкцій",
            "на віллі в Монако й пропускає хід", "летить на віллу в Монако й пропустить наступний хід", "у Монако",
            "іде на рейдерське захоплення");

        public static readonly ThemeWords Kyiv = new(
            "у заторі", "у затор", "у затор", "із затору", "Об'їзд дворами",
            "відпочиває на Трухановому острові й пропускає хід", "їде на Трухановий острів і пропустить наступний хід", "на Трухановому",
            "грає з наперсточниками");

        public static ThemeWords For(BoardTheme theme) => theme switch
        {
            BoardTheme.Military => Military,
            BoardTheme.Government => Government,
            BoardTheme.Crypto => Crypto,
            BoardTheme.Games => Games,
            BoardTheme.Oligarchs => Oligarchs,
            BoardTheme.Kyiv => Kyiv,
            _ => Business,
        };

        public static string ThemeName(BoardTheme theme) => theme switch
        {
            BoardTheme.Military => "Військова інфраструктура України",
            BoardTheme.Government => "Уряд України",
            BoardTheme.Crypto => "Криптовалюти",
            BoardTheme.Games => "Відеоігри",
            BoardTheme.Oligarchs => "Битва олігархів",
            BoardTheme.Kyiv => "Київ",
            _ => "Українські бізнеси",
        };
    }
}
