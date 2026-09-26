using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Аукцион (RULES.md, §4). Три игрока: Аня (0) отказывается от «Сільпо», дальше торгуются все.
    public class AuctionTests
    {
        private static Game DeclinedSilpo()
        {
            var game = CreateFor(3, 1, 2);
            game.Do(new RollDice(0));
            game.Do(new DeclinePurchase(0));
            return game;
        }

        [Fact]
        public void Decline_StartsAuction()
        {
            var game = CreateFor(3, 1, 2);
            game.Do(new RollDice(0));

            var result = game.Do(new DeclinePurchase(0));

            Assert.Equal(TurnPhase.Auction, game.State.Phase);
            Assert.Contains(new AuctionStarted(Silpo), result.Events);
            Assert.Equal("Зараз триває аукціон.", game.Error(new EndTurn(0)));
        }

        [Fact]
        public void EveryonePasses_CompanyStaysWithBank()
        {
            var game = DeclinedSilpo();

            game.Do(new PassAuction(1));
            game.Do(new PassAuction(2));
            var last = game.Do(new PassAuction(0));

            Assert.Contains(new AuctionUnsold(Silpo), last.Events);
            Assert.Null(game.State.Board[Silpo].OwnerId);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void HighestBidderWins_WhenOthersPass()
        {
            var game = DeclinedSilpo();

            game.Do(new PlaceBid(1, 10));
            Assert.Equal("Ставка має бути не менше 20 грн.", game.Error(new PlaceBid(2, 15)));
            game.Do(new PlaceBid(2, 20));
            game.Do(new PassAuction(0));
            var last = game.Do(new PassAuction(1));

            Assert.Contains(new AuctionWon(2, Silpo, 20), last.Events);
            Assert.Equal(2, game.State.Board[Silpo].OwnerId);
            Assert.Equal(GameRules.StartingBalance - 20, game.P(2).Balance);
            Assert.Null(game.State.Auction);
        }

        [Fact]
        public void DeclinerCanBidToo()
        {
            var game = DeclinedSilpo();

            game.Do(new PlaceBid(0, 10));
            game.Do(new PassAuction(1));
            game.Do(new PassAuction(2));

            Assert.Equal(0, game.State.Board[Silpo].OwnerId);
            Assert.Equal(GameRules.StartingBalance - 10, game.P(0).Balance);
        }

        [Fact]
        public void Rules_ForLeaderAndPassed()
        {
            var game = DeclinedSilpo();
            game.Do(new PlaceBid(1, 10));
            game.Do(new PassAuction(2));

            Assert.Equal("Лідер аукціону не може спасувати.", game.Error(new PassAuction(1)));
            Assert.Equal("Ваша ставка й так найвища.", game.Error(new PlaceBid(1, 50)));
            Assert.Equal("Ви вже спасували.", game.Error(new PlaceBid(2, 50)));
            game.P(0).Balance = 15;
            Assert.Equal("Не вистачає грошей: у вас 15 грн.", game.Error(new PlaceBid(0, 20)));
        }

        [Fact]
        public void FirstBid_AtLeastTen()
        {
            var game = DeclinedSilpo();

            Assert.Equal("Ставка має бути не менше 10 грн.", game.Error(new PlaceBid(1, 5)));
            Assert.Contains(new PlaceBid(1, 10), game.GetAvailableActions(1));
        }

        [Fact]
        public void NoAuction_BidIsRejected()
        {
            var game = CreateFor(3);

            Assert.Equal("Зараз немає аукціону.", game.Error(new PlaceBid(1, 10)));
        }

        [Fact]
        public void AfterAuctionOnDouble_PlayerRollsAgain()
        {
            var game = CreateFor(3, 1, 1);
            game.Do(new RollDice(0));
            game.Do(new DeclinePurchase(0));
            game.Do(new PassAuction(0));
            game.Do(new PassAuction(1));
            game.Do(new PassAuction(2));

            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
        }
    }
}
