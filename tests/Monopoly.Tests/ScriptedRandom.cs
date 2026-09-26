using Monopoly.Core;

namespace Monopoly.Tests
{
    // Генератор с заранее заданными числами: тест сам решает, что выпадет на кубиках.
    internal sealed class ScriptedRandom : IRandomSource
    {
        private readonly Queue<int> values;

        public ScriptedRandom(params int[] values)
        {
            this.values = new Queue<int>(values);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (values.Count == 0)
                throw new InvalidOperationException("Заготовленные числа закончились.");
            int value = values.Dequeue();
            if (value < minInclusive || value >= maxExclusive)
                throw new InvalidOperationException($"Число {value} вне диапазона [{minInclusive}, {maxExclusive}).");
            return value;
        }
    }
}
