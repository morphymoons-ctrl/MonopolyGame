using System.Text.Json;
using Monopoly.Core;

namespace Monopoly.Net
{
    public sealed record SavedSeat(string Name, int ColorIndex, bool IsHost, bool IsBot);

    // Сохранённая партия: зерно и все действия — по ним Game.Replay восстанавливает её точно такой же.
    public sealed record SaveFile(
        Guid GameId,
        string Version,
        int Seed,
        IReadOnlyList<SavedSeat> Seats,
        IReadOnlyList<GameAction> Actions,
        DateTime SavedAtUtc,
        bool Finished,
        // Сколько шла партия до сохранения — для часов длительности партии.
        int PlayedSeconds = 0)
    {
        public string? HostName => Seats.FirstOrDefault(s => s.IsHost)?.Name;
    }

    // Сохранения хоста: по файлу на партию. По умолчанию — %AppData%\Monopoly\saves.
    public sealed class SaveStore
    {
        private static readonly JsonSerializerOptions Json = CreateJson();

        public string Directory { get; }

        public SaveStore(string? directory = null)
        {
            Directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Monopoly", "saves");
        }

        // Запись через временный файл: если приложение упадёт посреди записи, старое сохранение останется целым.
        public void Write(SaveFile save)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string path = PathOf(save.GameId);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(save, Json));
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Не сохранилось — партия идёт дальше, сохраним после следующего действия.
            }
        }

        // Незаконченные партии этой версии игры, свежие сначала.
        public IReadOnlyList<SaveFile> ListUnfinished()
        {
            if (!System.IO.Directory.Exists(Directory))
                return Array.Empty<SaveFile>();

            var result = new List<SaveFile>();
            foreach (string file in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
            {
                try
                {
                    var save = JsonSerializer.Deserialize<SaveFile>(File.ReadAllText(file), Json);
                    if (save is { Finished: false } && save.Version == NetDefaults.GameVersion)
                        result.Add(save);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
                {
                    // Повреждённый или чужой файл — пропускаем.
                }
            }
            return result.OrderByDescending(s => s.SavedAtUtc).ToList();
        }

        public void Delete(Guid gameId)
        {
            try
            {
                File.Delete(PathOf(gameId));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        private string PathOf(Guid gameId) => Path.Combine(Directory, $"{gameId:N}.json");

        private static JsonSerializerOptions CreateJson()
        {
            var options = ProtocolJson.CreateOptions();
            options.WriteIndented = true;
            return options;
        }
    }
}
