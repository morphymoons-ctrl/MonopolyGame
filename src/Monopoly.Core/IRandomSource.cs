namespace Monopoly.Core
{
    // Единственный источник случайности в игре: кубики, порядок ходов, позже — колода «Шанса» и казино.
    public interface IRandomSource
    {
        int Next(int minInclusive, int maxExclusive);
    }

    // Генератор с зерном: одно и то же зерно и те же действия игроков дают ту же партию.
    public sealed class SeededRandom : IRandomSource
    {
        private readonly Random random;

        public int Seed { get; }

        public SeededRandom(int seed)
        {
            Seed = seed;
            random = new Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
    }
}
