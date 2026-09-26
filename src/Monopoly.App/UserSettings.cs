using System;
using System.IO;
using System.Text.Json;

namespace Monopoly.App
{
    // Что запомнить между запусками: имя и последний адрес хоста. Файл в %AppData%\Monopoly.
    public sealed class UserSettings
    {
        public string Name { get; set; } = "";
        public string LastAddress { get; set; } = "";

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Monopoly", "settings.json");

        public static UserSettings Load()
        {
            try
            {
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath)) ?? new UserSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new UserSettings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Не сохранилось — не страшно, в следующий раз имя введут заново.
            }
        }
    }
}
