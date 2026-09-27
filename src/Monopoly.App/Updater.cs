using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Monopoly.App
{
    // Автообновление (этап 6): новые версии лежат в GitHub Releases репозитория игры.
    // При запуске игра проверяет, есть ли версия новее, и скачивает её в фоне. Дальше — либо кнопка
    // «Оновити» на стартовом экране (перезапуск сразу), либо обновление ставится само при выходе из игры.
    // У запуска из Visual Studio / dotnet run установки нет — там проверка не делается.
    public sealed class Updater
    {
        public const string RepoUrl = "https://github.com/morphymoons-ctrl/MonopolyGame";

        private readonly UpdateManager manager = new(new GithubSource(RepoUrl, null, false));
        private VelopackAsset? downloaded;
        private Task? running;

        // Найденная новая версия; null — обновлений нет (или ещё не проверили).
        public string? FoundVersion { get; private set; }
        // Сколько скачано, 0–100.
        public int Progress { get; private set; }
        // Версия, которая скачана и ждёт перезапуска; null — ещё не скачана.
        public string? ReadyVersion { get; private set; }

        // Нашлась новая версия, скачивание продвинулось или закончилось — стартовый экран обновляет плашку.
        public event Action? Changed;

        // Один раз за запуск игры; повторные вызовы ждут ту же проверку.
        public Task CheckAsync() => running ??= CheckCoreAsync();

        private async Task CheckCoreAsync()
        {
            if (!manager.IsInstalled)
            {
                return;
            }
            try
            {
                var update = await manager.CheckForUpdatesAsync();
                if (update is null)
                {
                    return;
                }
                // Плашку показываем сразу, ещё до скачивания, — иначе игрок не знает, что идёт обновление.
                FoundVersion = update.TargetFullRelease.Version.ToString();
                Changed?.Invoke();
                await manager.DownloadUpdatesAsync(update, percent =>
                {
                    Progress = percent;
                    Changed?.Invoke();
                });
                downloaded = update.TargetFullRelease;
                // Если игрок не нажмёт «Оновити», новая версия встанет сама, когда он закроет игру.
                manager.WaitExitThenApplyUpdates(downloaded, silent: true, restart: false);
                ReadyVersion = FoundVersion;
                Changed?.Invoke();
            }
            catch (Exception)
            {
                // Нет интернета, GitHub недоступен, диск занят — игра работает на текущей версии, проверим в следующий раз.
                // Ошибка обновления не должна мешать играть, поэтому ловим всё.
            }
        }

        // Закрывает игру, ставит скачанную версию и запускает её снова.
        public void ApplyAndRestart()
        {
            if (downloaded is not null)
            {
                manager.ApplyUpdatesAndRestart(downloaded);
            }
        }
    }
}
