using Monopoly.Core;

namespace Monopoly.Tests
{
    // Партии для тестов: порядок ходов и колода не перемешиваются, числа генератора заданы заранее.
    internal static class TestGame
    {
        private static readonly string[] Names = { "Аня", "Богдан", "Вика", "Гриша", "Даша" };

        public static readonly GameOptions Fixed = new() { ShuffleTurnOrder = false, ShuffleChanceDeck = false };

        // Номера клеток из RULES.md, §2.
        public const int Atb = 1, Varus = 2, Silpo = 3, Wog = 4;
        public const int Arcelor = 5, Stasik = 6, Azovstal = 7, Jail = 8;
        public const int Tet = 9, Okko = 12, Casino = 16, Rest = 17;
        public const int Massage = 18, StripClub = 20, Upg = 21, NovaPoshta = 22, Ukrposhta = 23, Chance = 24;
        public const int Ukrnafta = 28;

        // Два игрока: Аня (0) ходит первой, Богдан (1). Числа — значения кубиков и других случайностей по порядку.
        public static Game Create(params int[] randomValues) => CreateFor(2, randomValues);

        public static Game CreateFor(int players, params int[] randomValues) =>
            new(Names.Take(players).ToArray(), new ScriptedRandom(randomValues), Fixed);

        public static Player P(this Game game, int id) => game.State.FindPlayer(id)!;

        public static void Give(this Game game, int playerId, params int[] cells)
        {
            foreach (int cell in cells)
                game.State.Board[cell].OwnerId = playerId;
        }

        // Действие, которое обязано пройти.
        public static ActionResult Do(this Game game, GameAction action)
        {
            var result = game.Execute(action);
            Assert.True(result.Success, result.Error);
            return result;
        }

        public static string? Error(this Game game, GameAction action) => game.Execute(action).Error;

        public static void PutOnTop(this Game game, ChanceCard card)
        {
            game.State.ChanceDeck.Remove(card);
            game.State.ChanceDeck.Insert(0, card);
        }
    }
}
