using System.Windows.Media;
using Monopoly.Core;
using Monopoly.Net;

namespace Monopoly.App
{
    // Цвета фишек: по одному на место в лобби.
    public static class PlayerPalette
    {
        public static readonly Brush[] Brushes =
        {
            Make("#E5484D"),
            Make("#2F7BFF"),
            Make("#30A46C"),
            Make("#F5A524"),
            Make("#8E4EC6"),
        };

        public static readonly string[] Names = { "Красный", "Синий", "Зелёный", "Оранжевый", "Фиолетовый" };

        public static Brush Get(int colorIndex) => Brushes[colorIndex % Lobby.ColorCount];

        internal static SolidColorBrush Make(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }

    // Цвета групп поля — полоса сверху клетки, как на настоящей доске.
    public static class GroupPalette
    {
        private static readonly Brush Supermarket = PlayerPalette.Make("#8D5A3B");
        private static readonly Brush Factory = PlayerPalette.Make("#6B7C93");
        private static readonly Brush Tv = PlayerPalette.Make("#E0569B");
        private static readonly Brush Food = PlayerPalette.Make("#F07A2E");
        private static readonly Brush Online = PlayerPalette.Make("#19B5C9");
        private static readonly Brush Bank = PlayerPalette.Make("#F2C12E");
        private static readonly Brush Network = PlayerPalette.Make("#2E9E5B");
        private static readonly Brush Gas = PlayerPalette.Make("#263447");
        private static readonly Brush Logistics = PlayerPalette.Make("#4B5BD6");
        private static readonly Brush Special = PlayerPalette.Make("#1F3354");

        public static Brush Get(CellType type) => type switch
        {
            CellType.Supermarket => Supermarket,
            CellType.Factory => Factory,
            CellType.TV => Tv,
            CellType.Food => Food,
            CellType.OnlineShop => Online,
            CellType.Bank => Bank,
            CellType.NetworkShop => Network,
            CellType.GasStation => Gas,
            CellType.Logistics => Logistics,
            _ => Special,
        };

        // Значок особых клеток (шрифт Segoe UI Symbol).
        public static string? Icon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "⛓",
            CellType.Casino => "♠♥♣♦",
            CellType.Rest => "☕",
            CellType.Chance => "?",
            CellType.GasStation => "⛽",
            CellType.Logistics => "✉",
            _ => null,
        };
    }
}
