using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Аукцион (RULES.md, §4). Три игрока: Аня (0) отказывается от «Сілько» (140 000), дальше торгуются все.
    // Первая ставка — от 90% цены: 126 000.
    public class AuctionTests
    {
        private static Game DeclinedSilpo()
        {
            var game = CreateFor(3, 1, 2);
            game.Do(new RollDice(0));
            game.Do(new DeclinePurchase(0));
            return game;
        }

        // Ане не хватает на «Сілько»: она — находчик и получит 30% от продажи.
        private static Game CouldNotAffordSilpo(int balance = 100_000)
        {
            var game = CreateFor(3, 1, 2);
            game.P(0).Balance = balance;
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
            Assert.Contains(new AuctionStarted(Silpo, 126_000, null), result.Events);
            Assert.Equal("Зараз триває аукціон.", game.Error(new EndTurn(0)));
        }

        [Fact]
        public void EveryonePasses_CompanyStaysWithBank()
        {
            var game = CouldNotAffordSilpo();

            game.Do(new PassAuction(1));
            game.Do(new PassAuction(2));
            var last = game.Do(new PassAuction(0));

            Assert.Contains(new AuctionUnsold(Silpo), last.Events);
            Assert.DoesNotContain(last.Events, e => e is FinderPaid);
            Assert.Null(game.State.Board[Silpo].OwnerId);
            Assert.Equal(100_000, game.P(0).Balance);
            Assert.Equal(TurnPhase.Manage, game.State.Phase);
        }

        [Fact]
        public void HighestBidderWins_WhenOthersPass()
        {
            var game = DeclinedSilpo();

            game.Do(new PlaceBid(1, 126_000));
            Assert.Equal($"Ставка має бути не менше {M(136_000)}.", game.Error(new PlaceBid(2, 130_000)));
            game.Do(new PlaceBid(2, 136_000));
            game.Do(new PassAuction(0));
            var last = game.Do(new PassAuction(1));

            Assert.Contains(new AuctionWon(2, Silpo, 136_000), last.Events);
            // Аня отказалась, хотя денег хватало, — 30% ей не положено.
            Assert.DoesNotContain(last.Events, e => e is FinderPaid);
            Assert.Equal(2, game.State.Board[Silpo].OwnerId);
            Assert.Equal(GameRules.StartingBalance - 136_000, game.P(2).Balance);
            Assert.Equal(GameRules.StartingBalance, game.P(0).Balance);
            Assert.Null(game.State.Auction);
        }

        [Fact]
        public void FinderWhoCouldNotAfford_Gets30Percent()
        {
            var game = CouldNotAffordSilpo();
            Assert.Equal(0, game.State.Auction!.FinderId);

            game.Do(new PlaceBid(1, 146_000));
            game.Do(new PassAuction(2));
            var last = game.Do(new PassAuction(0));

            // 30% от 146 000 = 43 800 — Ане; Богдан платит только свою ставку.
            Assert.Contains(new FinderPaid(0, Silpo, 43_800), last.Events);
            Assert.Equal(100_000 + 43_800, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance - 146_000, game.P(1).Balance);
        }

        [Fact]
        public void FinderWhoWinsHimself_GetsNothing()
        {
            var game = CouldNotAffordSilpo(balance: 130_000);

            game.Do(new PlaceBid(0, 126_000));
            game.Do(new PassAuction(1));
            var last = game.Do(new PassAuction(2));

            Assert.Equal(0, game.State.Board[Silpo].OwnerId);
            Assert.DoesNotContain(last.Events, e => e is FinderPaid);
            Assert.Equal(130_000 - 126_000, game.P(0).Balance);
        }

        [Fact]
        public void DeclinerCanBidToo()
        {
            var game = DeclinedSilpo();

            game.Do(new PlaceBid(0, 126_000));
            game.Do(new PassAuction(1));
            game.Do(new PassAuction(2));

            Assert.Equal(0, game.State.Board[Silpo].OwnerId);
            Assert.Equal(GameRules.StartingBalance - 126_000, game.P(0).Balance);
        }

        [Fact]
        public void Rules_ForLeaderAndPassed()
        {
            var game = DeclinedSilpo();
            game.Do(new PlaceBid(1, 126_000));
            game.Do(new PassAuction(2));

            Assert.Equal("Лідер аукціону не може спасувати.", game.Error(new PassAuction(1)));
            Assert.Equal("Ваша ставка й так найвища.", game.Error(new PlaceBid(1, 200_000)));
            Assert.Equal("Ви вже спасували.", game.Error(new PlaceBid(2, 200_000)));
            game.P(0).Balance = 15_000;
            Assert.Equal($"Не вистачає грошей: у вас {M(15_000)}.", game.Error(new PlaceBid(0, 136_000)));
        }

        [Fact]
        public void FirstBid_From90PercentOfPrice()
        {
            var game = DeclinedSilpo();

            Assert.Equal($"Ставка має бути не менше {M(126_000)}.", game.Error(new PlaceBid(1, 10_000)));
            Assert.Contains(new PlaceBid(1, 126_000), game.GetAvailableActions(1));
        }

        [Theory]
        [InlineData(140_000, 126_000)]
        [InlineData(95_500, 86_000)]
        public void StartBid_IsRounded(int price, int startBid)
        {
            Assert.Equal(startBid, GameRules.AuctionStartBid(price));
        }

        [Fact]
        public void NoAuction_BidIsRejected()
        {
            var game = CreateFor(3);

            Assert.Equal("Зараз немає аукціону.", game.Error(new PlaceBid(1, 126_000)));
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
