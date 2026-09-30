using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // События партии (RULES.md, §17). Аня (0) и Богдан (1); событие запускается напрямую, без генератора.
    public class EventsTests
    {
        private static List<GameEvent> Start(Game game, WorldEventKind kind, CellType? group = null)
        {
            var events = new List<GameEvent>();
            game.StartEvent(kind, events, group);
            return events;
        }

        private static int RentOf(Game game, int cell) =>
            GameRules.Rent(game.State.Board, cell, 0, game.State.RentContext);

        [Fact]
        public void NewRound_WhenTurnReturnsToFirstPlayer()
        {
            // Аня и Богдан: 13 → 16 «Казино», оба просто заканчивают ход.
            var game = Create(1, 2, 1, 2);
            game.P(0).Position = 13;
            game.P(1).Position = 13;
            game.Do(new RollDice(0));
            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));

            var result = game.Do(new EndTurn(1));

            Assert.Contains(new RoundStarted(2), result.Events);
            Assert.Equal(2, game.State.Round);
        }

        [Fact]
        public void LastingEvent_EndsAfterItsRounds()
        {
            var game = Create();
            var started = Start(game, WorldEventKind.Crisis);
            Assert.Contains(new WorldEventStarted(WorldEventKind.Crisis, 2, null), started);

            var second = new List<GameEvent>();
            game.StartRound(second);
            Assert.Single(game.State.ActiveEvents);
            Assert.Equal(1, game.State.ActiveEvents[0].RoundsLeft(game.State.Round));

            var third = new List<GameEvent>();
            game.StartRound(third);
            Assert.Contains(new WorldEventEnded(WorldEventKind.Crisis), third);
            Assert.Empty(game.State.ActiveEvents);
        }

        [Fact]
        public void Inflation_RaisesPrices_BranchCosts_StartBonus_AndStationRent()
        {
            var game = Create();
            game.Give(1, Wog);

            Start(game, WorldEventKind.Inflation);

            Assert.Equal(154_000, game.State.Board[Silpo].Price);
            Assert.Equal(110_000, game.State.Board[Silpo].BranchCost);
            Assert.Equal(220_000, game.State.StartBonus);
            Assert.Equal(55_000, RentOf(game, Wog));
            Assert.Empty(game.State.ActiveEvents);

            Start(game, WorldEventKind.Inflation);

            Assert.Equal(169_400, game.State.Board[Silpo].Price);
            Assert.Equal(121_000, game.State.Board[Silpo].BranchCost);
            Assert.Equal(242_000, game.State.StartBonus);
        }

        [Fact]
        public void Crisis_HalvesRent_Boom_AddsHalf()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);

            Start(game, WorldEventKind.Crisis);
            Assert.Equal(8_400, RentOf(game, Silpo));
            var result = game.Do(new RollDice(0));
            Assert.Contains(new RentPaid(0, 1, Silpo, 8_400), result.Events);

            game.State.Events.Clear();
            Start(game, WorldEventKind.Boom);
            Assert.Equal(25_200, RentOf(game, Silpo));
        }

        [Fact]
        public void LuxuryTax_TenPercentOfCash()
        {
            var game = Create();
            game.P(0).Balance = 1_000_000;

            var events = Start(game, WorldEventKind.LuxuryTax);

            Assert.Contains(new PaidToBank(0, 100_000), events);
            Assert.Contains(new PaidToBank(1, 150_000), events);
            Assert.Equal(900_000, game.P(0).Balance);
        }

        [Fact]
        public void StateAid_PoorestGetsDouble()
        {
            var game = Create();
            game.P(0).Balance = 100_000;

            Start(game, WorldEventKind.StateAid);

            Assert.Equal(300_000, game.P(0).Balance);
            Assert.Equal(GameRules.StartingBalance + 100_000, game.P(1).Balance);
        }

        [Fact]
        public void Solidarity_RichestPaysPoorest()
        {
            var game = Create();
            game.P(0).Balance = 100_000;
            game.P(1).Balance = 2_000_000;

            var events = Start(game, WorldEventKind.Solidarity);

            Assert.Contains(new PaidToPlayer(1, 0, 200_000), events);
            Assert.Equal(300_000, game.P(0).Balance);
            Assert.Equal(1_800_000, game.P(1).Balance);
        }

        [Fact]
        public void BuildingBan_NoBuilding()
        {
            var game = Create();
            game.Give(0, Atb, Varus);

            Start(game, WorldEventKind.BuildingBan);

            Assert.Equal("Мораторій на будівництво: будувати не можна ще 2 кола.", game.Error(new BuildBranch(0, Atb)));
        }

        [Fact]
        public void Privatization_FreeCompaniesGoToAuction_FromHalfPrice()
        {
            var game = Create();
            int atbHalf = game.State.Board[Atb].Price / 2;

            var events = Start(game, WorldEventKind.Privatization);

            Assert.Contains(new AuctionStarted(Atb, atbHalf, null), events);
            Assert.Equal(TurnPhase.Auction, game.State.Phase);

            game.Do(new PassAuction(0));
            var next = game.Do(new PassAuction(1));

            Assert.Contains(new AuctionUnsold(Atb), next.Events);
            Assert.Equal(Varus, game.State.Auction!.CellIndex);
            Assert.Equal(game.State.Board[Varus].Price / 2, game.State.Auction.MinBid);
        }

        [Fact]
        public void Privatization_ReturnsToTurn_AfterLastAuction()
        {
            var game = Create();
            var free = Enumerable.Range(0, game.State.Board.Count).Where(i => game.State.Board[i].IsPurchasable).ToList();
            game.Give(1, free.Skip(1).ToArray());

            Start(game, WorldEventKind.Privatization);
            game.Do(new PlaceBid(0, game.State.Auction!.MinBid));
            game.Do(new PassAuction(1));

            Assert.Equal(0, game.State.Board[free[0]].OwnerId);
            Assert.Equal(TurnPhase.AwaitingRoll, game.State.Phase);
        }

        [Fact]
        public void DemandSeason_DoublesGroupRent()
        {
            var game = Create();
            game.Give(1, Silpo);

            Start(game, WorldEventKind.DemandSeason, CellType.Supermarket);

            Assert.Equal(33_600, RentOf(game, Silpo));
        }

        [Fact]
        public void Sanctions_NoRentForGroup()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);
            Start(game, WorldEventKind.Sanctions, CellType.Supermarket);

            var result = game.Do(new RollDice(0));

            Assert.Contains(new RentWaived(0, Silpo, WorldEventKind.Sanctions), result.Events);
            Assert.Equal(GameRules.StartingBalance, game.P(0).Balance);
        }

        [Fact]
        public void Blackout_NoRentForStationsAndLogistics()
        {
            var game = Create(1, 3);
            game.Give(1, Wog);
            Start(game, WorldEventKind.Blackout);

            var result = game.Do(new RollDice(0));

            Assert.Contains(new RentWaived(0, Wog, WorldEventKind.Blackout), result.Events);
        }

        [Fact]
        public void Quarantine_NobodyPaysRent()
        {
            var game = Create(1, 2);
            game.Give(1, Silpo);
            Start(game, WorldEventKind.Quarantine);

            var result = game.Do(new RollDice(0));

            Assert.Contains(new RentWaived(0, Silpo, WorldEventKind.Quarantine), result.Events);
        }

        [Fact]
        public void Raid_JailSkipsTwoTurns()
        {
            // Аня: 3 + 5 → пєтушатня во время «Облави». Богдан: 13 → 16 «Казино».
            var game = Create(3, 5, 1, 2);
            game.P(1).Position = 13;
            Start(game, WorldEventKind.Raid);

            game.Do(new RollDice(0));
            Assert.Equal(2, game.P(0).JailSkips);
            game.Do(new EndTurn(0));
            game.Do(new RollDice(1));
            var skip = game.Do(new EndTurn(1));

            Assert.Contains(new TurnSkipped(0, SkipReason.Jail), skip.Events);
            Assert.True(game.P(0).IsInJail);
            Assert.Equal(1, game.P(0).JailSkips);
        }

        [Theory]
        [InlineData(45, 1)]
        [InlineData(90, 3)]
        public void Jackpot_TriplesMoreOften(int roll, int multiplier)
        {
            var game = Create(1, 3, roll);
            game.P(0).Position = Okko;
            Start(game, WorldEventKind.Jackpot);
            game.Do(new RollDice(0));

            var result = game.Do(new PlayCasino(0, 100_000));

            Assert.Contains(new CasinoPlayed(0, 100_000, multiplier), result.Events);
        }

        [Fact]
        public void CanStart_Limits()
        {
            var game = Create();
            Start(game, WorldEventKind.Crisis);
            Assert.False(game.CanStart(WorldEventKind.Boom));
            Assert.False(game.CanStart(WorldEventKind.Crisis));

            Start(game, WorldEventKind.Raid);
            Assert.False(game.CanStart(WorldEventKind.Jackpot));
            Assert.True(game.CanStart(WorldEventKind.LuxuryTax));
            Assert.True(game.CanStart(WorldEventKind.Inflation));

            game.Give(0, Enumerable.Range(0, game.State.Board.Count).Where(i => game.State.Board[i].IsPurchasable).ToArray());
            Assert.False(game.CanStart(WorldEventKind.Privatization));
        }

        [Fact]
        public void Seeded_Game_HasEvents_FromRound3_NoMoreThanTwoLasting_AndReplays()
        {
            var names = new[] { "Аня", "Богдан", "Віка" };
            var game = Game.Start(names, seed: 5, BoardTheme.Business, EventFrequency.Often);
            var started = new List<int>();
            for (int step = 0; step < 3000 && game.State.Phase != TurnPhase.GameOver && game.State.Round < 15; step++)
            {
                int player = game.AwaitedPlayers().First();
                var result = game.Execute(Bot.Choose(game, player)!);
                Assert.True(result.Success, result.Error);
                started.AddRange(result.Events.OfType<WorldEventStarted>().Select(_ => game.State.Round));
                Assert.True(game.State.ActiveEvents.Count <= WorldEvents.MaxLasting);
            }

            Assert.NotEmpty(started);
            Assert.True(started.Min() >= WorldEvents.FirstRound);

            var copy = Game.Replay(names, 5, game.Actions, BoardTheme.Business, EventFrequency.Often);
            Assert.Equal(game.State.ActiveEvents, copy.State.ActiveEvents);
            Assert.Equal(game.State.PriceIndex, copy.State.PriceIndex);
            Assert.Equal(game.State.Players.Select(p => p.Balance), copy.State.Players.Select(p => p.Balance));
        }

        [Theory]
        [InlineData(1, "1 коло")]
        [InlineData(2, "2 кола")]
        [InlineData(5, "5 кіл")]
        [InlineData(11, "11 кіл")]
        [InlineData(21, "21 коло")]
        public void Rounds_UkrainianForms(int n, string text)
        {
            Assert.Equal(text, WorldEvents.Rounds(n));
        }

        [Fact]
        public void EventsOff_NoEvents()
        {
            var names = new[] { "Аня", "Богдан" };
            var game = Game.Start(names, seed: 5);
            for (int step = 0; step < 1500 && game.State.Phase != TurnPhase.GameOver && game.State.Round < 10; step++)
            {
                var result = game.Execute(Bot.Choose(game, game.AwaitedPlayers().First())!);
                Assert.DoesNotContain(result.Events, e => e is WorldEventStarted);
            }
        }
    }
}
