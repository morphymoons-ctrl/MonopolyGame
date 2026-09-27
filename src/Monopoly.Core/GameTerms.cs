namespace Monopoly.Core
{
    // Слова для построек на доске (RULES.md, §15): на основной — «філія» и «головний офіс»,
    // на военной — «підрозділ» и «штаб». Механика одна, меняются только слова в текстах.
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

        public static GameTerms For(BoardTheme theme) => theme == BoardTheme.Military ? Military : Business;

        // С большой буквы — для начала фразы.
        public static string Capital(string word) => word.Length == 0 ? word : char.ToUpper(word[0]) + word[1..];
    }
}
