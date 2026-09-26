using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Monopoly.App
{
    // Логотипы компаний на клетках: Assets/Logos/*.png, вшиты в Monopoly.exe.
    // Нет файла — на клетке остаётся название. Файлы названы по старым названиям клеток, номер клетки — индекс в списке.
    public static class Logos
    {
        private static readonly string?[] Files =
        {
            null, "atb", "varus", "silpo", "wog", "arcelormittal", "stasik", "azovstal",
            null, "tet", "novyi-kanal", "intel", "okko", "puzata-hata", "pizza-day", "bulochna-1",
            null, null, "masazhka", "bordel", "stripclub", "upg", "nova-poshta", "ukrposhta",
            null, "pumb", "privatbank", "monobank", "ukrnafta", "allo", "citrus", "foxtrot",
        };

        private static readonly Dictionary<int, ImageSource?> Cache = new();

        public static ImageSource? For(int cellIndex)
        {
            if (Cache.TryGetValue(cellIndex, out var cached))
            {
                return cached;
            }
            var logo = cellIndex < Files.Length && Files[cellIndex] is { } file ? Load(file) : null;
            Cache[cellIndex] = logo;
            return logo;
        }

        private static ImageSource? Load(string file)
        {
            try
            {
                var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Logos/{file}.png"));
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
                return Trim(image);
            }
            catch (IOException)
            {
                // Такого логотипа ещё нет.
                return null;
            }
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
