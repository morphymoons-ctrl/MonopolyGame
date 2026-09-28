using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Monopoly.Core;

namespace Monopoly.App
{
    // Логотипы компаний на клетках: Assets/Logos/<папка доски>/*.png, вшиты в Monopoly.exe.
    // У каждой доски (RULES.md, §15) своя папка. Нет файла — на клетке остаётся название.
    // Номер в списке — номер клетки; у углов и особых клеток логотипа нет (null).
    public static class Logos
    {
        // «Українські бізнеси»: файлы названы по старым названиям клеток.
        private static readonly string?[] BusinessFiles =
        {
            null, "atb", "varus", "silpo", "wog", "arcelormittal", "stasik", "azovstal",
            null, "tet", "novyi-kanal", "intel", "okko", "puzata-hata", "pizza-day", "bulochna-1",
            null, "masazhka", "bordel", "stripclub", null, "upg", "nova-poshta", "ukrposhta",
            null, "pumb", "privatbank", "monobank", "ukrnafta", "allo", "citrus", "foxtrot",
        };

        // «Військова інфраструктура України»: латиницей по названиям клеток.
        private static readonly string?[] MilitaryFiles =
        {
            null, "pikhota", "lehka-bronetekhnika", "tankovi-viiska", "ok-pivnich", "bronetankovyi-zavod", "zavod-boieprypasiv", "raketnyi-zavod",
            null, "viiskovyi-zviazok", "reb", "rozvidka", "ok-skhid", "minometna-bataria", "haubychna-artyleriia", "rszv-himars",
            null, "fpv-drony", "rozviduvalni-bpla", "udarni-bpla", null, "ok-pivden", "viiskova-zaliznytsia", "medychna-sluzhba",
            null, "morska-pikhota", "katery", "morski-drony", "ok-zakhid", "zrk-buk", "iris-t", "patriot",
        };

        // «Уряд України».
        private static readonly string?[] GovernmentFiles =
        {
            null, "patrulna-politsiia", "natsionalna-politsiia", "natsgvardiia", "kpp-yahodyn", "sbu", "hur", "szru",
            null, "nazk", "sap", "nabu", "kpp-chop", "pecherskyi-sud", "verkhovnyi-sud", "konstytutsiinyi-sud",
            null, "podatkova", "mytnytsia", "beb", null, "kpp-krakovets", "naftohaz", "ukrenerho",
            null, "diia", "rezerv-plus", "armiia-plus", "kpp-shehyni", "kabmin", "verkhovna-rada", "ofis-prezydenta",
        };

        // «Криптовалюти».
        private static readonly string?[] CryptoFiles =
        {
            null, "dogecoin", "shiba-inu", "pepe", "ferma-harazh", "binance", "coinbase", "whitebit",
            null, "metamask", "trust-wallet", "ledger", "ferma-tekhas", "solana", "ton", "cardano",
            null, "bored-ape", "cryptopunks", "pudgy-penguins", null, "ferma-islandiia", "usdt", "usdc",
            null, "uniswap", "aave", "pancakeswap", "ferma-kazakhstan", "bnb", "ethereum", "bitcoin",
        };

        // «Відеоігри».
        private static readonly string?[] GamesFiles =
        {
            null, "brawl-stars", "clash-royale", "subway-surfers", "steam", "valorant", "call-of-duty", "cs2",
            null, "stardew-valley", "terraria", "hollow-knight", "epic-games", "kozaky-3", "metro-exodus", "stalker-2",
            null, "roblox", "garrys-mod", "minecraft", null, "playstation", "twitch", "youtube",
            null, "pubg", "apex-legends", "fortnite", "xbox", "witcher-3", "gta-5", "dota-2",
        };

        // «Битва олігархів».
        private static readonly string?[] OligarchsFiles =
        {
            null, "larok", "avtomyika", "lombard", "poltavske-rodovyshche", "khlibozavod", "tsukrovyi-zavod", "ptakhofabryka",
            null, "hazeta", "radiostantsiia", "telekanal", "shebelynka", "koksokhim", "hzk", "metkombinat",
            null, "mini-npz", "merezha-azs", "hazzbut", null, "boryslav", "hazoprovid", "naftoprovid",
            null, "kredytna-spilka", "rehionalnyi-bank", "systemnyi-bank", "shelf", "investfond", "ofshor", "finpromhrupa",
        };

        // «Київ».
        private static readonly string?[] KyivFiles =
        {
            null, "troieshchyna", "borshchahivka", "obolon", "tsentralnyi-vokzal", "retroville", "blockbuster-mall", "gulliver",
            null, "lukianivskyi-rynok", "zhytnii-rynok", "bessarabskyi-rynok", "boryspil", "lavina-mall", "ocean-plaza", "respublika-park",
            null, "hidropark", "peizazhna-aleia", "vdnh", null, "darnytsia", "metro", "funikuler",
            null, "hotel-ukraina", "hilton", "fairmont", "zhuliany", "leonardo", "parus", "101-tower",
        };

        private static readonly Dictionary<(BoardTheme, int), ImageSource?> Cache = new();

        // Доски, где по картинке (фото места, эмблема, монета) клетку сразу не узнать: под картинкой пишется название.
        public static bool WithCaption(BoardTheme theme) => theme is BoardTheme.Kyiv or BoardTheme.Military or BoardTheme.Crypto;

        public static ImageSource? For(int cellIndex, BoardTheme theme)
        {
            if (Cache.TryGetValue((theme, cellIndex), out var cached))
            {
                return cached;
            }
            var (folder, files) = theme switch
            {
                BoardTheme.Military => ("military", MilitaryFiles),
                BoardTheme.Government => ("government", GovernmentFiles),
                BoardTheme.Crypto => ("crypto", CryptoFiles),
                BoardTheme.Games => ("games", GamesFiles),
                BoardTheme.Oligarchs => ("oligarchs", OligarchsFiles),
                BoardTheme.Kyiv => ("kyiv", KyivFiles),
                _ => ("business", BusinessFiles),
            };
            var logo = cellIndex < files.Length && files[cellIndex] is { } file ? Load(folder, file) : null;
            Cache[(theme, cellIndex)] = logo;
            return logo;
        }

        // Логотип — PNG (прозрачный фон). Фото — JPEG: в разы легче, а скруглённые углы игра дорисует сама.
        private static ImageSource? Load(string folder, string file) =>
            Read(folder, file + ".png") is { } png ? Trim(png)
            : Read(folder, file + ".jpg") is { } jpg ? RoundCorners(jpg)
            : null;

        private static BitmapSource? Read(string folder, string name)
        {
            try
            {
                var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Logos/{folder}/{name}"));
                if (resource is null)
                {
                    return null;
                }
                using var stream = resource.Stream;
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (IOException)
            {
                // Такого файла нет.
                return null;
            }
        }

        // Фото со скруглёнными углами — как у картинок-PNG (радиус — 1/8 стороны).
        private static BitmapSource RoundCorners(BitmapSource source)
        {
            int width = source.PixelWidth, height = source.PixelHeight;
            double radius = Math.Min(width, height) / 8.0;
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.PushClip(new RectangleGeometry(new Rect(0, 0, width, height), radius, radius));
                context.DrawImage(source, new Rect(0, 0, width, height));
            }
            var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            result.Render(visual);
            result.Freeze();
            return result;
        }

        // Картинки квадратные 512×512, а сам логотип лежит в квадрате как придётся: широкий — полосой, иногда выше середины.
        // Срезаем прозрачные поля — дальше логотип центрируется и масштабируется по своим настоящим границам.
        private static BitmapSource Trim(BitmapSource source)
        {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth, height = bgra.PixelHeight, stride = width * 4;
            var pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);

            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Почти прозрачные точки (тень, сглаживание края) границу не двигают.
                    if (pixels[y * stride + x * 4 + 3] > 16)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }
            if (right < 0)
            {
                return source;
            }
            var cropped = new CroppedBitmap(source, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
            cropped.Freeze();
            return cropped;
        }
    }
}
