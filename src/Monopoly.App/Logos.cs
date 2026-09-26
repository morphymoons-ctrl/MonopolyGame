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
            null, null, "masazhka", "bordel", "stripklub", "upg", "nova-poshta", "ukrposhta",
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
                // Картинки большие, а на клетке нужны ~150 точек: уменьшаем при загрузке, чтобы не держать мегабайты в памяти.
                image.DecodePixelWidth = 400;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (IOException)
            {
                // Такого логотипа ещё нет.
                return null;
            }
        }
    }
}
