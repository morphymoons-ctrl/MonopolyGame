namespace Monopoly.Core
{
    // Слова для построек на доске (RULES.md, §15): на основной — «філія» и «головний офіс»,
    // на военной — «підрозділ» и «штаб», на «Уряді» — «відділ» и «головне управління». Механика одна, меняются только слова.
    public sealed record GameTerms(
        string Branch,          // одна: «філія» / «підрозділ»
        string BranchAccusative, // «відкриває філію» / «розгортає підрозділ»
        string Branches,        // несколько: «філії» / «підрозділи»
        string BranchesGenitive, // «немає філій» / «немає підрозділів»
        string Office,          // «головний офіс» / «штаб»
        string OfficeTag)       // метка на клетке: «ОФІС» / «ШТАБ»
    {
        public static readonly GameTerms Business = new("філія", "філію", "філії", "філій", "головний офіс", "ОФІС");
        public static readonly GameTerms Military = new("підрозділ", "підрозділ", "підрозділи", "підрозділів", "штаб", "ШТАБ");
        public static readonly GameTerms Government = new("відділ", "відділ", "відділи", "відділів", "головне управління", "ГУ");
        public static readonly GameTerms Crypto = new("нода", "ноду", "ноди", "нод", "дата-центр", "ДЦ");
        public static readonly GameTerms Games = new("сервер", "сервер", "сервери", "серверів", "турнірна арена", "АРЕНА");

        public static GameTerms For(BoardTheme theme) => theme switch
        {
            BoardTheme.Military => Military,
            BoardTheme.Government => Government,
            BoardTheme.Crypto => Crypto,
            BoardTheme.Games => Games,
            _ => Business,
        };

        // С большой буквы — для начала фразы.
        public static string Capital(string word) => word.Length == 0 ? word : char.ToUpper(word[0]) + word[1..];
    }
}
