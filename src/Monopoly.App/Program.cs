using System;
using Velopack;

namespace Monopoly.App
{
    // Точка входа. Velopack первым делом обрабатывает свои запуски (установка, обновление, удаление) и сам завершает их;
    // при обычном запуске он ничего не делает, и открывается игра.
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            VelopackApp.Build().Run();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
