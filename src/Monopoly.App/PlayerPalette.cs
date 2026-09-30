using System;
using System.Linq;
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

        public static readonly string[] Names = { "Червоний", "Синій", "Зелений", "Помаранчевий", "Фіолетовий" };

        public static Brush Get(int colorIndex) => Brushes[colorIndex % Lobby.ColorCount];

        // Те же цвета, осветлённые на треть, — для текста на тёмном фоне (имена в журнале).
        private static readonly Brush[] LightBrushes = Brushes.Select(b => Lighten(((SolidColorBrush)b).Color, 0.35)).ToArray();

        public static Brush GetLight(int colorIndex) => LightBrushes[colorIndex % Lobby.ColorCount];

        private static Brush Lighten(Color color, double amount)
        {
            byte Mix(byte channel) => (byte)(channel + (255 - channel) * amount);
            var brush = new SolidColorBrush(Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B)));
            brush.Freeze();
            return brush;
        }

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
        private static readonly Brush Nightlife = PlayerPalette.Make("#19B5C9");
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
            CellType.Nightlife => Nightlife,
            CellType.Bank => Bank,
            CellType.NetworkShop => Network,
            CellType.GasStation => Gas,
            CellType.Logistics => Logistics,
            _ => Special,
        };

        // Значок клетки: у особых — в центре, у компаний — в полосе группы. У военной доски (§15) — свои.
        public static string? Icon(CellType type, BoardTheme theme = BoardTheme.Business) => theme switch
        {
            BoardTheme.Military => MilitaryIcon(type),
            BoardTheme.Government => GovernmentIcon(type),
            BoardTheme.Crypto => CryptoIcon(type),
            BoardTheme.Games => GamesIcon(type),
            BoardTheme.Oligarchs => OligarchsIcon(type),
            BoardTheme.Kyiv => KyivIcon(type),
            _ => BusinessIcon(type),
        };

        private static string? KyivIcon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "🚧",
            CellType.Casino => "🎲",
            CellType.Rest => "🏖",
            CellType.Chance => "📱",
            CellType.GasStation => "✈",
            CellType.Logistics => "🚇",
            CellType.Supermarket => "🏘",
            CellType.Factory => "🛍",
            CellType.TV => "🍎",
            CellType.Food => "🏬",
            CellType.Nightlife => "🎡",
            CellType.Bank => "🏨",
            CellType.NetworkShop => "🏙",
            _ => null,
        };

        private static string? OligarchsIcon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "⛔",
            CellType.Casino => "🦈",
            CellType.Rest => "🛥",
            CellType.Chance => "💼",
            CellType.GasStation => "🛢",
            CellType.Logistics => "🔧",
            CellType.Supermarket => "💵",
            CellType.Factory => "🏭",
            CellType.TV => "📡",
            CellType.Food => "⚒",
            CellType.Nightlife => "⛽",
            CellType.Bank => "🏦",
            CellType.NetworkShop => "💰",
            _ => null,
        };

        private static string? GamesIcon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "🚫",
            CellType.Casino => "🎰",
            CellType.Rest => "💤",
            CellType.Chance => "📦",
            CellType.GasStation => "🕹",
            CellType.Logistics => "📺",
            CellType.Supermarket => "📱",
            CellType.Factory => "🎯",
            CellType.TV => "🎨",
            CellType.Food => "☢",
            CellType.Nightlife => "⛏",
            CellType.Bank => "🪂",
            CellType.NetworkShop => "🏆",
            _ => null,
        };

        private static string? CryptoIcon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "🔒",
            CellType.Casino => "📈",
            CellType.Rest => "💎",
            CellType.Chance => "🐦",
            CellType.GasStation => "⛏",
            CellType.Logistics => "$",
            CellType.Supermarket => "🐕",
            CellType.Factory => "💱",
            CellType.TV => "🔑",
            CellType.Food => "🔗",
            CellType.Nightlife => "🖼",
            CellType.Bank => "🦄",
            CellType.NetworkShop => "👑",
            _ => null,
        };

        private static string? GovernmentIcon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "⛓",
            CellType.Casino => "✋",
            CellType.Rest => "✈",
            CellType.Chance => "📜",
            CellType.GasStation => "⛔",
            CellType.Logistics => "⚡",
            CellType.Supermarket => "🚓",
            CellType.Factory => "🕵",
            CellType.TV => "🔍",
            CellType.Food => "⚖",
            CellType.Nightlife => "💰",
            CellType.Bank => "📱",
            CellType.NetworkShop => "🏛",
            _ => null,
        };

        private static string? MilitaryIcon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "⛓",
            CellType.Casino => "♠♥♣♦",
            CellType.Rest => "☕",
            CellType.Chance => "!",
            CellType.GasStation => "★",
            CellType.Logistics => "✚",
            CellType.Supermarket => "⚔",
            CellType.Factory => "🏭",
            CellType.TV => "📡",
            CellType.Food => "💥",
            CellType.Nightlife => "✈",
            CellType.Bank => "⚓",
            CellType.NetworkShop => "🛡",
            _ => null,
        };

        private static string? BusinessIcon(CellType type) => type switch
        {
            CellType.Start => "➜",
            CellType.Jail => "⛓",
            CellType.Casino => "♠♥♣♦",
            CellType.Rest => "☕",
            CellType.Chance => "?",
            CellType.GasStation => "⛽",
            CellType.Logistics => "✉",
            CellType.Supermarket => "🛒",
            CellType.Factory => "🏭",
            CellType.TV => "📺",
            CellType.Food => "🍴",
            CellType.Nightlife => "🍸",
            CellType.Bank => "🏦",
            CellType.NetworkShop => "📱",
            _ => null,
        };

        // Шрифт значков: Segoe UI Symbol, а чего в нём нет (тележка) — из Segoe UI Emoji. WPF рисует их одним цветом.
        public const string IconFont = "Segoe UI Symbol, Segoe UI Emoji";

        // Значок в полосе — белый; на светлой жёлтой полосе банков — тёмный, иначе не видно.
        private static readonly Brush DarkIcon = PlayerPalette.Make("#1A2233");

        public static Brush IconColor(CellType type) => type == CellType.Bank ? DarkIcon : System.Windows.Media.Brushes.White;
    }

    // Шрифты, которые задаются в коде.
    public static class GameFonts
    {
        // Суммы — цены на клетках и балансы игроков: Arial, обычное начертание. Есть в любой Windows, кириллица есть.
        public static readonly FontFamily Money = new("Arial");

        // Значки клеток.
        public static readonly FontFamily Icons = new(GroupPalette.IconFont);
    }
}

namespace Monopoly.App
{
    // Юнит игрока — чем он ходит по полю (лобби: «Обрати юніта»). 0 — кружок, остальное — эмодзи из Lobby.Units,
    // окрашенные в цвет игрока, как значки на клетках. Размер у всех одинаковый.
    public static class UnitVisual
    {
        public static readonly string[] Names =
        {
            "Кружечок", "Мавпа", "Папуга", "Горила", "Орангутан", "Пес", "Кіт", "Тигр", "Кінь",
            "Коза", "Вівця", "Свиня", "Слон", "Півень", "Черепаха", "Крокодил",
        };

        public static bool IsEmoji(int unit) => unit > 0 && unit < Lobby.Units.Count;

        public static string Name(int unit) => unit >= 0 && unit < Names.Length ? Names[unit] : Names[0];

        // Контуры эмодзи — один раз на юнит.
        private static readonly System.Collections.Generic.Dictionary<int, Geometry> Outlines = new();

        // Юнит — фигура с белой обводкой stroke, как кружок: и кружок, и эмодзи читаются на любой клетке, даже своего цвета.
        // Эмодзи — контур значка (заливка цветом игрока), вписанный в тот же квадрат size, что и кружок.
        public static System.Windows.Shapes.Shape Create(int unit, Brush color, double size, double stroke)
        {
            if (!IsEmoji(unit))
            {
                return new System.Windows.Shapes.Ellipse { Width = size, Height = size, Fill = color, Stroke = System.Windows.Media.Brushes.White, StrokeThickness = stroke };
            }
            // У эмодзи обводка тонкая и тёмная: многие значки нарисованы тонкими линиями, белая обводка их бы забила.
            return new System.Windows.Shapes.Path
            {
                Data = Outline(unit),
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                Fill = color,
                Stroke = EmojiEdge,
                StrokeThickness = stroke / 2,
                StrokeLineJoin = PenLineJoin.Round,
            };
        }

        private static readonly Brush EmojiEdge = PlayerPalette.Make("#1A1C20");

        // Юнит того, кто ходит, — заметнее: у кружка обводка толще, у эмодзи — золотое свечение вокруг (обводка та же,
        // иначе тонкие линии значка потеряются). Остальные — с обычной тенью.
        public static void Highlight(System.Windows.Shapes.Shape unit, bool current, double size)
        {
            if (unit is System.Windows.Shapes.Ellipse)
            {
                unit.StrokeThickness = current ? 5 : 3;
                return;
            }
            unit.Effect = current
                ? new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0xFF, 0xD2, 0x4A), ShadowDepth = 0, BlurRadius = size * 0.45, Opacity = 1 }
                : new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.6 };
        }

        private static Geometry Outline(int unit)
        {
            if (!Outlines.TryGetValue(unit, out var geometry))
            {
                var text = new FormattedText(Lobby.Units[unit], System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight, new Typeface(GameFonts.Icons, System.Windows.FontStyles.Normal,
                        System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal), 100, System.Windows.Media.Brushes.Black, 1.0);
                geometry = text.BuildGeometry(new System.Windows.Point(0, 0));
                geometry.Freeze();
                Outlines[unit] = geometry;
            }
            return geometry;
        }
    }
}
