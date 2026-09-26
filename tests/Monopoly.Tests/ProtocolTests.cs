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
            var events = new GameEvent[]
            {
                new TurnStarted(1),
                new DiceRolled(1, 3, 4),
                new PlayerMoved(1, 30, 3),
                new PurchaseOffered(1, 3, 140),
                new PropertyBought(1, 3, 140),
                new PurchaseDeclined(1, 3),
                new RentPaid(1, 0, 4, 16),
                new RestStarted(1),
                new TurnSkipped(1),
            };
            var eventTypes = typeof(GameEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent)));
            Assert.Equal(
                eventTypes.Select(t => t.Name).Except(new[] { nameof(GameStarted) }).Order(),
                events.Select(e => e.GetType().Name).Order());

            Assert.Equal(events, RoundTrip(events));

            var started = RoundTrip<GameEvent>(new GameStarted(42, new[] { 2, 0, 1 }));
            Assert.Equal(new[] { 2, 0, 1 }, Assert.IsType<GameStarted>(started).TurnOrder);
            Assert.Equal(42, ((GameStarted)started).Seed);
        }

        [Fact]
        public void GameUpdate_SurvivesRoundTrip()
        {
            var game = new Game(new[] { "Аня", "Богдан" }, new ScriptedRandom(1, 2), shuffleTurnOrder: false);
            var result = game.Execute(new RollDice(0));
            var update = new GameUpdate(result.Events, game.State.ToSnapshot(), game.GetAvailableActions(0));

            var copy = RoundTrip(update);

            Assert.Equal(update.Events, copy.Events);
            Assert.Equal(update.AvailableActions, copy.AvailableActions);
            Assert.Equal(update.Snapshot.Players, copy.Snapshot.Players);
            Assert.Equal(update.Snapshot.Owners, copy.Snapshot.Owners);
            Assert.Equal(TurnPhase.BuyDecision, copy.Snapshot.Phase);
            Assert.Equal(new DiceRoll(1, 2), copy.Snapshot.LastRoll);
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
