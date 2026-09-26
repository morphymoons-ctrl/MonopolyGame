using Monopoly.Core;
using static Monopoly.Tests.TestGame;

namespace Monopoly.Tests
{
    // Кого ждёт игра, автодействия по таймеру, восстановление партии и бот (RULES.md, §13).
    public class WaitingTests
    {
        [Fact]
        public void AwaitedAndTimeout_FollowPhases()
        {
            var game = CreateFor(3, 1, 2);

            Assert.Equal(new[] { 0 }, game.AwaitedPlayers());
            Assert.Equal(new RollDice(0), game.TimeoutAction(0));
            Assert.Null(game.TimeoutAction(1));

            game.Do(new RollDice(0));
            Assert.Equal(new DeclinePurchase(0), game.TimeoutAction(0));

            game.Do(new DeclinePurchase(0));
            Assert.Equal(new[] { 0, 1, 2 }, game.AwaitedPlayers());
            game.Do(new PlaceBid(1, 10));
            Assert.Equal(new[] { 0, 2 }, game.AwaitedPlayers());
            Assert.Equal(new PassAuction(2), game.TimeoutAction(2));
            Assert.Null(game.TimeoutAction(1));

            game.Do(game.TimeoutAction(0)!);
            game.Do(game.TimeoutAction(2)!);
            Assert.Equal(new EndTurn(0), game.TimeoutAction(0));
        }

        [Fact]
        public void Timeout_OnTrade_Rejects()
        {
            var game = Create();
            game.Give(0, Atb);
            game.Do(new ProposeTrade(0, 1, new TradeTerms(new[] { Atb }, 0, 0), TradeTerms.Empty));

            Assert.Equal(new[] { 1 }, game.AwaitedPlayers());
            Assert.Equal(new RejectTrade(1), game.TimeoutAction(1));
        }

        [Fact]
        public void Timeout_InDebt_SellsBranchesFirst_ThenMortgagesCheapest()
        {
            // Аня встаёт на «Сільпо» Богдана с головным офисом (980). Наличных 600,
            // остальное соберёт, продав филиалы ТВ и заложив компании.
            var game = CreateFor(3, 1, 2);
            game.Give(1, Atb, Varus, Silpo);
            game.State.Board[Silpo].Level = 5;
            game.P(0).Balance = 600;
            game.Give(0, Tet, 10, 11, Arcelor);
            game.State.Board[Tet].Level = 2;
            game.State.Board[10].Level = 2;
            game.State.Board[11].Level = 3;
            game.Do(new RollDice(0));
            Assert.Equal(TurnPhase.Debt, game.State.Phase);

            Assert.Equal(new SellBranch(0, 11), game.TimeoutAction(0));

            int steps = 0;
            while (game.State.Phase == TurnPhase.Debt && steps++ < 50)
                game.Do(game.TimeoutAction(0)!);

            Assert.False(game.P(0).IsBankrupt);
            Assert.NotEqual(TurnPhase.Debt, game.State.Phase);
            Assert.Equal(GameRules.StartingBalance + 980, game.P(1).Balance);
            Assert.All(game.State.Board, c => Assert.True(c.Level >= 0));
        }

        [Fact]
        public void LiquidationStep_MortgagesCheapestWhenNoBranches()
        {
            var game = Create();
            game.Give(0, Arcelor, Atb);

            Assert.Equal(new MortgageCompany(0, Atb), game.LiquidationStep(0));
        }

        [Fact]
        public void Replay_RestoresSameGame()
        {
            var names = new[] { "А", "Б", "В" };
            var original = Game.Start(names, seed: 99);
            Bots.Play(original, new Random(5), maxActions: 400);

            var copy = Game.Replay(names, original.Seed!.Value, original.Actions);

            Assert.Equal(original.History, copy.History, new GameTests.EventComparer());
            Assert.Equal(original.State.ToSnapshot().Players, copy.State.ToSnapshot().Players);
            Assert.Equal(original.State.ToSnapshot().Cells, copy.State.ToSnapshot().Cells);
            Assert.Equal(original.Actions.Count, copy.Actions.Count);
        }

        [Fact]
        public void Replay_RejectsForeignActions()
        {
            var ex = Assert.Throws<InvalidDataException>(() =>
                Game.Replay(new[] { "А", "Б" }, 1, new GameAction[] { new EndTurn(0) }));

            Assert.Contains("Збереження не підходить", ex.Message);
        }

        [Fact]
        public void Bot_BuysWithReserve_AndDeclinesWhenPoor()
        {
            var game = Create(1, 2);
            game.Do(new RollDice(0));
            Assert.Equal(new BuyProperty(0), Bot.Choose(game, 0));

            var poor = Create(1, 2);
            poor.P(0).Balance = 400;
            poor.Do(new RollDice(0));
            Assert.Equal(new DeclinePurchase(0), Bot.Choose(poor, 0));
        }

        [Fact]
        public void Bot_BuildsWithMonopoly_BeforeRolling()
        {
            var game = Create();
            game.Give(0, Atb, Varus, Silpo);

            Assert.Equal(new BuildBranch(0, Silpo), Bot.Choose(game, 0));
        }

        [Fact]
        public void Bot_RejectsTrades_AndDoesNothingOutOfTurn()
        {
            var game = Create();
            game.Give(0, Atb);

            Assert.Null(Bot.Choose(game, 1));
            game.Do(new ProposeTrade(0, 1, new TradeTerms(new[] { Atb }, 0, 0), TradeTerms.Empty));
            Assert.Equal(new RejectTrade(1), Bot.Choose(game, 1));
        }

        // Только боты: всегда есть ход, ни одного недопустимого действия.
        // Боты не меняются, поэтому при 4–5 игроках монополий почти нет и партия может идти бесконечно.
        [Theory]
        [InlineData(1, 2, true)]
        [InlineData(2, 3, true)]
        [InlineData(3, 4, false)]
        [InlineData(4, 5, false)]
        public void BotsOnly_KeepPlaying(int seed, int players, bool mustFinish)
        {
            var names = Enumerable.Range(1, players).Select(i => $"Бот {i}").ToArray();
            var game = Game.Start(names, seed);

            for (int i = 0; i < 20000 && game.State.Phase != TurnPhase.GameOver; i++)
            {
                var action = game.State.ActivePlayers.Select(p => Bot.Choose(game, p.Id)).FirstOrDefault(a => a is not null);
                Assert.NotNull(action);
                var result = game.Execute(action!);
                Assert.True(result.Success, $"{action}: {result.Error}");
            }

            if (mustFinish)
                Assert.Equal(TurnPhase.GameOver, game.State.Phase);
        }
    }
}
