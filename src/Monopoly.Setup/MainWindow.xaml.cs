using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Monopoly.Setup
{
    // Выбор папки и запуск встроенного установщика Velopack с --installto. Дальше всё делает Velopack:
    // копирует игру, создаёт ярлыки, запись в «Программах» и запускает игру; обновления потом идут в ту же папку.
    public partial class MainWindow : Window
    {
        private const string PackId = "MonopolyGame";
        private const string PayloadName = "payload.exe";

        // Где игра уже установлена (Velopack пишет это в «Программы»); null — не установлена.
        private readonly string? installed = FindInstalled();

        public MainWindow()
        {
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Встановлення гри · версія {version.Major}.{version.Minor}.{version.Build}";
            PathBox.Text = installed ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PackId);
            if (installed is not null)
            {
                HintText.Text = "Гру вже встановлено в цій папці — її буде перевстановлено. Оберете іншу — гру буде перенесено туди. "
                    + "Збережені партії й налаштування залишаться.";
            }
            if (Assembly.GetExecutingAssembly().GetManifestResourceInfo(PayloadName) is null)
            {
                ShowError("У цьому файлі немає самої гри — його зібрано без установника. Завантажте установник ще раз.");
                InstallButton.IsEnabled = false;
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            string current = PathBox.Text.Trim();
            string? start = Directory.Exists(current) ? current : SafeParent(current);
            string? folder = FolderPicker.Pick(new WindowInteropHelper(this).Handle, "Оберіть папку для гри", start);
            if (folder is null)
                return;
            // Выбрали общую папку (например, D:\Ігри) — игра ляжет в свою подпапку.
            PathBox.Text = string.Equals(Path.GetFileName(folder), PackId, StringComparison.OrdinalIgnoreCase)
                ? folder
                : Path.Combine(folder, PackId);
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            string? target = Validate(PathBox.Text);
            if (target is null)
                return;
            if (Process.GetProcessesByName("Monopoly").Length > 0)
            {
                ShowError("Гра зараз відкрита. Закрийте «Монополію» й натисніть «Встановити» ще раз.");
                return;
            }

            SetBusy(true);
            ErrorText.Visibility = Visibility.Collapsed;
            string payload = Path.Combine(Path.GetTempPath(), $"{PackId}-setup-{Guid.NewGuid():N}.exe");
            try
            {
                // Перенос в другую папку: сначала тихо удаляем старую копию (партии и настройки — в %AppData%, их это не касается).
                if (installed is not null && !SamePath(installed, target))
                {
                    StatusText.Text = "Видаляємо стару копію…";
                    int removed = await RunAsync(Path.Combine(installed, "Update.exe"), "uninstall --silent");
                    if (removed != 0 || FindInstalled() is not null)
                    {
                        ShowError($"Не вдалося прибрати стару копію з «{installed}». Видаліть гру через «Параметри → Програми» і спробуйте ще раз.");
                        return;
                    }
                }

                StatusText.Text = "Встановлюємо…";
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadName)!)
                using (var file = File.Create(payload))
                {
                    await resource.CopyToAsync(file);
                }

                // Velopack показывает своё окошко с прогрессом, а в конце запускает игру.
                int exitCode = await RunAsync(payload, $"--installto \"{target}\"");
                if (exitCode == 0)
                {
                    Close();
                    return;
                }
                ShowError($"Встановлення не вдалося (код {exitCode}). Спробуйте іншу папку або запустіть установник ще раз.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                ShowError($"Встановлення не вдалося: {ex.Message}");
            }
            finally
            {
                TryDelete(payload);
                StatusText.Text = "";
                SetBusy(false);
            }
        }

        // Полный путь к папке установки или null (ошибка уже показана).
        private string? Validate(string text)
        {
            string path;
            try
            {
                path = Path.GetFullPath(text.Trim()).TrimEnd('\\');
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                ShowError("Це не схоже на шлях до папки. Приклад: D:\\Ігри\\MonopolyGame.");
                return null;
            }
            if (!Path.IsPathRooted(text.Trim()) || Path.GetPathRoot(path)!.TrimEnd('\\') == path)
            {
                ShowError("Оберіть папку, а не цілий диск. Приклад: D:\\Ігри\\MonopolyGame.");
                return null;
            }
            // Папка текущей установки не пустая — это нормально: игра переустановится поверх.
            if (installed is not null && SamePath(installed, path))
                return path;
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
            {
                ShowError("Ця папка не порожня. Оберіть порожню або нову — гра створить її сама.");
                return null;
            }

            // Установка идёт без прав администратора: в Program Files и подобные папки записать не выйдет.
            bool created = !Directory.Exists(path);
            try
            {
                Directory.CreateDirectory(path);
                string probe = Path.Combine(path, ".write-test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowError("Сюди не можна записувати без прав адміністратора. Оберіть іншу папку, наприклад D:\\Ігри.");
                return null;
            }
            finally
            {
                if (created)
                    TryDeleteDirectory(path);
            }
            return path;
        }

        private static Task<int> RunAsync(string exe, string arguments) => Task.Run(() =>
        {
            using var process = Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false })!;
            process.WaitForExit();
            return process.ExitCode;
        });

        private static bool SamePath(string a, string b) =>
            string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        private static string? FindInstalled()
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{PackId}");
            return key?.GetValue("InstallLocation") is string location && Directory.Exists(location) ? location.TrimEnd('\\') : null;
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void SetBusy(bool busy)
        {
            InstallButton.IsEnabled = !busy;
            BrowseButton.IsEnabled = !busy;
            PathBox.IsEnabled = !busy;
            CancelButton.IsEnabled = !busy;
        }

        private static string? SafeParent(string path)
        {
            try
            {
                return Path.GetDirectoryName(path);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void TryDelete(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Временный файл — не страшно, Windows уберёт.
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
