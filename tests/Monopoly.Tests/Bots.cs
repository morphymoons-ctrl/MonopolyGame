using Monopoly.Core;

namespace Monopoly.Tests
{
    // Боты для тестов: делают случайные допустимые действия. Генератор бота — свой, движок о нём не знает.
    internal static class Bots
    {
        // Играет, пока партия не кончится или не наберётся maxActions действий. check — проверка после каждого действия.
        public static void Play(Game game, Random rng, int maxActions, Action<Game>? check = null)
        {
            for (int i = 0; i < maxActions && game.State.Phase != TurnPhase.GameOver; i++)
            {
                var options = game.State.ActivePlayers
                    .SelectMany(p => game.GetAvailableActions(p.Id))
                    .ToList();
                // Если никому нечего делать — игра зависла.
                Assert.NotEmpty(options);

                var action = Choose(game, options, rng);
                var result = game.Execute(action);
                if (action is ProposeTrade)
                    continue; // случайный обмен может быть недопустимым — это нормально
                Assert.True(result.Success, $"{action}: {result.Error}");
                check?.Invoke(game);
            }
        }

        // Боты покупают и строят охотно, а продают и закладывают в основном в долгах — так партии доходят до банкротств.
        private static GameAction Choose(Game game, List<GameAction> options, Random rng)
        {
            T Pick<T>(IReadOnlyList<T> list) => list[rng.Next(list.Count)];

            if (game.State.Phase == TurnPhase.Debt)
            {
                var raise = options.Where(a => a is SellBranch or MortgageCompany).ToList();
                return raise.Count > 0 && rng.Next(10) > 0 ? Pick(raise) : Pick(options);
            }

            var buy = options.OfType<BuyProperty>().ToList();
            if (buy.Count > 0 && rng.Next(10) < 8)
                return buy[0];
            var build = options.OfType<BuildBranch>().ToList();
            if (build.Count > 0 && rng.Next(10) < 6)
                return Pick(build);
            var trade = options.OfType<ProposeTrade>().FirstOrDefault();
            if (trade is not null && rng.Next(20) == 0)
                return RandomTrade(game, trade.PlayerId, rng);

            var main = options.Where(a => a is not (BuildBranch or SellBranch or MortgageCompany or RedeemCompany or ProposeTrade)).ToList();
            var property = options.Where(a => a is SellBranch or MortgageCompany or RedeemCompany).ToList();
            if (main.Count > 0 && (property.Count == 0 || rng.Next(20) > 0))
                return Pick(main);
            return property.Count > 0 ? Pick(property) : Pick(options.Where(a => a is not ProposeTrade).ToList());
        }

        // Отдать случайную свою компанию случайному игроку за 10 грн.
        private static GameAction RandomTrade(Game game, int playerId, Random rng)
        {
            var mine = Enumerable.Range(0, game.State.Board.Count).Where(i => game.State.Board[i].OwnerId == playerId).ToList();
            var others = game.State.ActivePlayers.Where(p => p.Id != playerId).ToList();
            var target = others[rng.Next(others.Count)];
            var give = mine.Count > 0 ? new TradeTerms(new[] { mine[rng.Next(mine.Count)] }, 0, 0) : TradeTerms.Empty;
            return new ProposeTrade(playerId, target.Id, give, new TradeTerms(Array.Empty<int>(), Math.Min(10, target.Balance), 0));
        }
    }
}
