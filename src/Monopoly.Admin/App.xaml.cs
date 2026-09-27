using System;
using System.IO;
using System.Windows;
using Monopoly.Net;

namespace Monopoly.Admin
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Monopoly.Admin.exe --new-key <файл>: создать ключ (если его ещё нет) и записать открытый ключ в файл —
            // его вставляют в AdminAuth.OwnerPublicKey. Существующий ключ не заменяется.
            if (e.Args.Length == 2 && e.Args[0] == "--new-key")
            {
                try
                {
                    using var key = AdminKeyStore.LoadOrCreate();
                    File.WriteAllText(e.Args[1], AdminAuth.ExportPublicKey(key));
                    Shutdown(0);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    File.WriteAllText(e.Args[1], "ERROR: " + ex.Message);
                    Shutdown(1);
                }
                return;
            }

            new MainWindow().Show();
        }
    }
}
