using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Круглые суммы (RULES.md, §5, §11) и доски, где суммы показываются в миллионах (§15).
    public class MoneyTests
    {
        // Между тысячами в игре — неразрывный пробел; здесь сравниваем с обычным.
        private static string Plain(string text) => text.Replace(' ', ' ');

        [Theory]
        [InlineData(15_250, 15_300)]
        [InlineData(12_140, 12_100)]
        [InlineData(104_500, 104_500)]
        [InlineData(1_254_000, 1_300_000)]
        [InlineData(1_026_000, 1_000_000)]
        public void RoundMoney_ToHundreds_AndFromMillion_ToHundredThousands(int amount, int rounded)
        {
            Assert.Equal(rounded, GameRules.RoundMoney(amount));
        }

        [Fact]
        public void RentWithHeadOffice_IsRounded()
        {
            // 12% от 190 000 = 22 800; ×55 = 1 254 000 → 1 300 000, коротко «1,3м».
            var cell = new BoardCell("Холдинг", CellType.Factory, 190_000);
            Assert.Equal(1_300_000, GameRules.LevelRent(cell, GameRules.HeadOfficeLevel));
            Assert.Equal("1,3м", GameRules.ShortMoney(GameRules.LevelRent(cell, GameRules.HeadOfficeLevel)));
        }

        [Fact]
        public void MillionsBoards_ShowSameEconomy_TimesThousand()
        {
            Assert.Equal("90 млн грн", Plain(GameRules.Money(90_000, BoardTheme.Military)));
            Assert.Equal("1 500 млн грн", Plain(GameRules.Money(GameRules.StartingBalance, BoardTheme.Oligarchs)));
            Assert.Equal("104,5 млн грн", Plain(GameRules.Money(104_500, BoardTheme.Oligarchs)));
            Assert.Equal("10,8м", GameRules.ShortMoney(10_800, BoardTheme.Military));
            Assert.Equal("1,3млрд", GameRules.ShortMoney(1_300_000, BoardTheme.Oligarchs));
            Assert.Equal("млн грн", GameRules.Currency(BoardTheme.Military));
            Assert.Equal("грн", GameRules.Currency(BoardTheme.Kyiv));
        }

        [Theory]
        [InlineData("1 500 000", BoardTheme.Business, 1_500_000)]
        [InlineData("15 250 грн", BoardTheme.Business, 15_300)]
        [InlineData("$50 000", BoardTheme.Crypto, 50_000)]
        [InlineData("150", BoardTheme.Military, 150_000)]
        [InlineData("1,5", BoardTheme.Oligarchs, 1_500)]
        [InlineData("0.25", BoardTheme.Oligarchs, 300)]
        public void ParseMoney_InBoardUnits(string text, BoardTheme theme, int amount)
        {
            Assert.Equal(amount, GameRules.ParseMoney(text, theme));
            Assert.Null(GameRules.ParseMoney("багато", theme));
        }

        [Fact]
        public void MoneyInput_RoundTrips()
        {
            Assert.Equal("1500", GameRules.MoneyInput(GameRules.StartingBalance, BoardTheme.Oligarchs));
            Assert.Equal("1.5", GameRules.MoneyInput(1_500, BoardTheme.Oligarchs).Replace(",", "."));
            Assert.Equal(104_500, GameRules.ParseMoney(GameRules.MoneyInput(104_500, BoardTheme.Military), BoardTheme.Military));
        }

        [Fact]
        public void TradeMoney_MustBeRound()
        {
            var game = Create();
            var offer = new ProposeTrade(0, 1, new TradeTerms(Array.Empty<int>(), 15_250, 0), new TradeTerms(Array.Empty<int>(), 0, 0));

            Assert.Equal("Сума в обміні має бути круглою, наприклад 15 300 грн.", Plain(game.Error(offer)!));
            Assert.Null(game.Error(offer with { Give = new TradeTerms(Array.Empty<int>(), 15_300, 0) }));
        }
    }
}
