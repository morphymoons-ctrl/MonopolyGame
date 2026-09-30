using System.Net;
using System.Text.Json;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.Tests
{
    public class ProtocolTests
    {
        private static readonly JsonSerializerOptions Options = ProtocolJson.CreateOptions();

        private static T RoundTrip<T>(T value) =>
            JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;

        [Fact]
        public void Actions_KeepTheirType()
        {
            GameAction action = new BuyProperty(3);

            Assert.Equal(action, RoundTrip(action));
        }

        [Fact]
        public void AllEventsOfCore_SurviveRoundTrip()
        {
            // Каждый новый тип события должен проходить по сети — тест напомнит, если нет.
            var offer = new TradeOffer(0, 1, new TradeTerms(new[] { 1, 2 }, 100, 1), TradeTerms.Empty);
            var events = new GameEvent[]
            {
                new TurnStarted(1),
                new TurnSkipped(1, SkipReason.Jail),
                new GameOver(2),
                new DiceRolled(1, 3, 4),
                new RollAgain(1),
                new PlayerMoved(1, 30, 3),
                new PassedStart(1, 200),
                new LandedOnStart(1, 200),
                new RestStarted(1),
                new SentToJail(1, JailReason.Landed),
                new JailCardUsed(1),
                new MortgageExpired(1, 3),
                new PurchaseOffered(1, 3, 140),
                new PropertyBought(1, 3, 140),
                new PurchaseDeclined(1, 3),
                new AuctionStarted(3, 126, 0),
                new BidPlaced(2, 30),
                new AuctionPassed(0),
                new AuctionWon(2, 3, 30),
                new FinderPaid(0, 3, 9),
                new AuctionUnsold(3),
                new RentPaid(1, 0, 4, 16),
                new RentSkipped(1, 4),
                new PaidToBank(1, 50),
                new ReceivedFromBank(1, 150),
                new PaidToPlayer(1, 2, 25),
                new DebtIncurred(1, null, 100),
                new DebtPaid(1, 2, 100),
                new PlayerBankrupt(1, null),
                new CasinoOffered(1),
                new CasinoPlayed(1, 100, 3),
                new ChanceCardDrawn(1, ChanceCard.Taxi),
                new BranchBuilt(1, 3, 5, 70),
                new BranchSold(1, 3, 4, 35),
                new CompanyMortgaged(1, 3, 70),
                new CompanyRedeemed(1, 3, 77),
                new TradeRejected(0, 1),
                new TradeCancelled(0, 1),
                new RoundStarted(3),
                new WorldEventStarted(WorldEventKind.DemandSeason, 2, CellType.Food),
                new WorldEventEnded(WorldEventKind.Crisis),
                new RentWaived(1, 4, WorldEventKind.Quarantine),
            };
            var withLists = new[] { nameof(GameStarted), nameof(TradeProposed), nameof(TradeAccepted) };
            var eventTypes = typeof(GameEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent)));
            Assert.Equal(
                eventTypes.Select(t => t.Name).Except(withLists).Order(),
                events.Select(e => e.GetType().Name).Order());

            Assert.Equal(events, RoundTrip(events));

            // У событий со списками равенство записей сравнивает списки по ссылке — проверяем поля.
            var started = Assert.IsType<GameStarted>(RoundTrip<GameEvent>(new GameStarted(42, new[] { 2, 0, 1 })));
            Assert.Equal(new[] { 2, 0, 1 }, started.TurnOrder);
            Assert.Equal(42, started.Seed);

            var proposed = Assert.IsType<TradeProposed>(RoundTrip<GameEvent>(new TradeProposed(offer)));
            Assert.Equal(new[] { 1, 2 }, proposed.Offer.Give.Cells);
            Assert.Equal(100, proposed.Offer.Give.Money);
            Assert.Equal(1, proposed.Offer.Give.JailCards);
            Assert.Empty(proposed.Offer.Take.Cells);
            Assert.IsType<TradeAccepted>(RoundTrip<GameEvent>(new TradeAccepted(offer)));
        }

        [Fact]
        public void TradeProposal_SurvivesRoundTrip()
        {
            GameAction action = new ProposeTrade(0, 2, new TradeTerms(new[] { 5 }, 0, 0), new TradeTerms(new int[0], 300, 0));

            var copy = Assert.IsType<ProposeTrade>(RoundTrip(action));

            Assert.Equal(2, copy.TargetId);
            Assert.Equal(new[] { 5 }, copy.Give.Cells);
            Assert.Equal(300, copy.Take.Money);
        }

        [Fact]
        public void GameUpdate_SurvivesRoundTrip()
        {
            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(1, 2), TestGame.Fixed);
            var result = game.Execute(new RollDice(0));
            game.Execute(new DeclinePurchase(0));
            game.Execute(new PlaceBid(1, 126_000));
            var update = new GameUpdate(result.Events, game.State.ToSnapshot(), game.GetAvailableActions(0));

            var copy = RoundTrip(update);

            Assert.Equal(update.Events, copy.Events);
            Assert.Equal(update.AvailableActions, copy.AvailableActions);
            Assert.Equal(update.Snapshot.Players, copy.Snapshot.Players);
            Assert.Equal(update.Snapshot.Cells, copy.Snapshot.Cells);
            Assert.Equal(TurnPhase.Auction, copy.Snapshot.Phase);
            Assert.Equal(new DiceRoll(1, 2), copy.Snapshot.LastRoll);
            Assert.Equal(1, copy.Snapshot.Auction!.LeaderId);
            Assert.Equal(136_000, copy.Snapshot.Auction.MinBid);
        }

        [Theory]
        [InlineData("26.10.20.30", "Radmin", "Famatech Radmin VPN Ethernet Adapter", LocalAddresses.Radmin)]
        [InlineData("26.10.20.30", "Ethernet 3", "", LocalAddresses.Radmin)]
        [InlineData("10.147.17.5", "ZeroTier One [abc]", "ZeroTier Virtual Port", LocalAddresses.ZeroTier)]
        [InlineData("100.101.102.103", "Tailscale", "Tailscale Tunnel", LocalAddresses.Tailscale)]
        [InlineData("100.64.0.1", "Ethernet", "", LocalAddresses.Tailscale)]
        [InlineData("100.200.0.1", "Ethernet", "", LocalAddresses.Other)]
        [InlineData("192.168.1.10", "Wi-Fi", "Intel Wireless", LocalAddresses.Lan)]
        [InlineData("172.20.0.3", "Ethernet", "", LocalAddresses.Lan)]
        [InlineData("10.0.0.2", "Ethernet", "", LocalAddresses.Lan)]
        public void Classify_RecognizesNetworks(string ip, string name, string description, string expected)
        {
            Assert.Equal(expected, LocalAddresses.Classify(IPAddress.Parse(ip), name, description));
        }

        [Theory]
        [InlineData("26.1.2.3", "26.1.2.3", 7777)]
        [InlineData("26.1.2.3:8000", "26.1.2.3", 8000)]
        [InlineData("my-pc", "my-pc", 7777)]
        public void ParseAddress_DefaultsToPort7777(string input, string host, int port)
        {
            Assert.Equal((host, port), GameClient.ParseAddress(input));
        }
    }
}
