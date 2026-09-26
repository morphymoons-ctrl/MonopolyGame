using System.Windows.Media;
using Monopoly.Net;

namespace Monopoly.App
{
    // Цвета фишек: по одному на место в лобби.
    public static class PlayerPalette
    {
        public static readonly Brush[] Brushes =
        {
            System.Windows.Media.Brushes.Red,
            System.Windows.Media.Brushes.Blue,
            System.Windows.Media.Brushes.Green,
            System.Windows.Media.Brushes.Orange,
            System.Windows.Media.Brushes.Purple,
        };

        public static readonly string[] Names = { "Красный", "Синий", "Зелёный", "Оранжевый", "Фиолетовый" };

        public static Brush Get(int colorIndex) => Brushes[colorIndex % Lobby.ColorCount];
    }
}
