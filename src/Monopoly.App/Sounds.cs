using System;
using System.IO;
using System.Media;

namespace Monopoly.App
{
    // Короткие звуки, синтезированные в памяти: без файлов и без лицензий.
    public sealed class Sounds
    {
        private const int Rate = 22050;

        private readonly SoundPlayer dice = Create(DiceSamples());
        private readonly SoundPlayer coin = Create(Tones((1568, 0.0, 0.09), (2093, 0.08, 0.28)));
        private readonly SoundPlayer turn = Create(Tones((523, 0.0, 0.45), (784, 0.05, 0.5)));
        private readonly SoundPlayer win = Create(Tones((523, 0.0, 0.3), (659, 0.15, 0.45), (784, 0.3, 0.6), (1047, 0.45, 0.9)));

        public bool Enabled { get; set; } = true;

        public void Dice() => Play(dice);
        public void Coin() => Play(coin);
        public void Turn() => Play(turn);
        public void Win() => Play(win);

        private void Play(SoundPlayer player)
        {
            if (!Enabled)
            {
                return;
            }
            try
            {
                player.Play();
            }
            catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
            {
                // Нет звуковой карты — играем молча.
            }
        }

        // Стук кубиков: несколько коротких щелчков шума. Шум — детерминированный, это не игровая случайность.
        private static float[] DiceSamples()
        {
            var samples = new float[(int)(Rate * 0.32)];
            uint state = 2463534242;
            foreach (double start in new[] { 0.0, 0.06, 0.11, 0.17, 0.22 })
            {
                int from = (int)(start * Rate), length = (int)(0.025 * Rate);
                for (int i = 0; i < length && from + i < samples.Length; i++)
                {
                    state ^= state << 13;
                    state ^= state >> 17;
                    state ^= state << 5;
                    float noise = (state / (float)uint.MaxValue) * 2 - 1;
                    samples[from + i] += noise * 0.35f * (float)Math.Exp(-i / (0.004 * Rate));
                }
            }
            return samples;
        }

        // Звенящие тоны: (частота, начало, конец) в секундах.
        private static float[] Tones(params (double Freq, double Start, double End)[] tones)
        {
            double total = 0;
            foreach (var t in tones)
            {
                total = Math.Max(total, t.End);
            }
            var samples = new float[(int)(Rate * total)];
            foreach (var (freq, start, end) in tones)
            {
                int from = (int)(start * Rate), length = (int)((end - start) * Rate);
                for (int i = 0; i < length && from + i < samples.Length; i++)
                {
                    double t = i / (double)Rate;
                    double envelope = Math.Min(1, t / 0.01) * Math.Exp(-t / ((end - start) / 3));
                    samples[from + i] += (float)(Math.Sin(2 * Math.PI * freq * t) * 0.18 * envelope);
                }
            }
            return samples;
        }

        private static SoundPlayer Create(float[] samples)
        {
            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                int dataBytes = samples.Length * 2;
                writer.Write("RIFF"u8.ToArray());
                writer.Write(36 + dataBytes);
                writer.Write("WAVEfmt "u8.ToArray());
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(Rate);
                writer.Write(Rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write("data"u8.ToArray());
                writer.Write(dataBytes);
                foreach (float s in samples)
                {
                    writer.Write((short)(Math.Clamp(s, -1f, 1f) * short.MaxValue));
                }
            }
            stream.Position = 0;
            var player = new SoundPlayer(stream);
            player.Load();
            return player;
        }
    }
}
