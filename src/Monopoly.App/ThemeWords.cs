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
        string CasinoOffered, // «… у казино» / «… сідає за карти в бліндажі»
        string StationShort)  // группа с арендой по числу клеток: «2 АЗС у власника» / «2 ОК у власника»
    {
        public static readonly ThemeWords Business = new(
            "у пєтушатні", "до пєтушатні", "в пєтушатню", "з пєтушатні", "Вийти з пєтушатні",
            "пропускає хід («Зачілься»)", "чілить і пропустить наступний хід", "чілить",
            "у казино", "АЗС");

        public static readonly ThemeWords Military = new(
            "на гауптвахті", "на гауптвахту", "на гауптвахту", "з гауптвахти", "Амністія від командира",
            "у відпустці й пропускає хід", "їде у відпустку й пропустить наступний хід", "у відпустці",
            "сідає за карти в бліндажі", "ОК");

        public static ThemeWords For(BoardTheme theme) => theme == BoardTheme.Military ? Military : Business;

        public static string ThemeName(BoardTheme theme) => theme switch
        {
            BoardTheme.Military => "Військова інфраструктура України",
            _ => "Українські бізнеси",
        };
    }
}
