using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Monopoly.Net;

namespace Monopoly.App
{
    // Переключает экраны и держит подключение: у хоста — ещё и сервер игры.
    public partial class MainWindow : Window
    {
        private readonly UserSettings settings = UserSettings.Load();
        private readonly SaveStore saves = new();
        private GameHost? host;
        private GameClient? client;
        private LobbyState? lastLobby;
        private LobbyScreen? lobbyScreen;
        private GameScreen? gameScreen;

        public MainWindow()
        {
            InitializeComponent();
            Closing += (_, _) => StopNetworkOnClose();
            ShowStart(null);
        }

        private void ShowStart(string? message)
        {
            lobbyScreen = null;
            gameScreen = null;
            Screen.Content = new StartScreen(settings, saves, CreateGameAsync, JoinGameAsync, ResumeGameAsync, message);
        }

        private GameHostOptions HostOptions => new() { Saves = saves };

        private async Task<string?> CreateGameAsync(string name)
        {
            try
            {
                host = await GameHost.StartAsync(HostOptions);
            }
            catch (IOException)
            {
                return PortBusyMessage;
            }
            return await ConnectOrStopAsync("127.0.0.1", name, host.HostToken);
        }

        // Продолжение сохранённой партии: хост заходит на своё место, остальные — под своими именами.
        private async Task<string?> ResumeGameAsync(SaveFile save)
        {
            try
            {
                host = await GameHost.ResumeAsync(save, HostOptions);
            }
            catch (IOException)
            {
                return PortBusyMessage;
            }
            catch (InvalidDataException ex)
            {
                return $"Не вдалося відкрити збереження: {ex.Message}";
            }
            return await ConnectOrStopAsync("127.0.0.1", host.HostName ?? settings.Name, host.HostToken);
        }

        private Task<string?> JoinGameAsync(string name, string address) => ConnectOrStopAsync(address, name, null);

        private static string PortBusyMessage =>
            $"Порт {NetDefaults.Port} зайнятий — схоже, гру вже створено на цьому комп'ютері. " +
            "Щоб зайти в неї другою копією, підключіться до 127.0.0.1.";

        private async Task<string?> ConnectOrStopAsync(string address, string name, string? hostToken)
        {
            var error = await ConnectAsync(address, name, hostToken);
            if (error is not null)
            {
                await StopNetworkAsync();
            }
            return error;
        }

        private async Task<string?> ConnectAsync(string address, string name, string? hostToken)
        {
            GameClient newClient;
            try
            {
                newClient = new GameClient(address);
            }
            catch (UriFormatException)
            {
                return $"Неправильна адреса: {address}.";
            }

            // Сообщения приходят из потоков сети — переносим их в поток окна.
            // Сообщения от старого подключения (после выхода) не показываем.
            client = newClient;
            lastLobby = null;
            newClient.LobbyChanged += s => OnUi(newClient, () => OnLobby(s));
            newClient.GameStarted += info => OnUi(newClient, () => OnGameStarted(info));
            newClient.GameUpdated += update => OnUi(newClient, () => gameScreen?.Apply(update));
            newClient.Notice += text => OnUi(newClient, () => gameScreen?.AddLog(text));
            newClient.Reconnecting += () => OnUi(newClient, () => gameScreen?.ShowReconnecting(true));
            newClient.Reconnected += () => OnUi(newClient, () => gameScreen?.ShowReconnecting(false));
            newClient.ConnectionLost += text => OnUi(newClient, () => OnConnectionLost(text));

            var error = await newClient.ConnectAsync(name, hostToken);
            if (error is not null)
            {
                return error;
            }

            // В идущую партию (возвращение, продолжение) лобби не нужно — хост сразу пришлёт начало игры.
            if (gameScreen is null)
            {
                lobbyScreen = new LobbyScreen(newClient, hostToken is not null, LeaveAsync);
                if (lastLobby is not null)
                {
                    lobbyScreen.Show(lastLobby);
                }
                Screen.Content = lobbyScreen;
            }
            return null;
        }

        private void OnUi(GameClient source, Action action)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (source == client)
                {
                    action();
                }
            });
        }

        private void OnLobby(LobbyState state)
        {
            lastLobby = state;
            lobbyScreen?.Show(state);
        }

        // Начало партии или возвращение в неё — экран строится заново по истории от хоста.
        private void OnGameStarted(GameStartInfo info)
        {
            lobbyScreen = null;
            gameScreen = new GameScreen(client!, info, settings, LeaveAsync);
            Screen.Content = gameScreen;
        }

        private async void OnConnectionLost(string text)
        {
            bool inGame = gameScreen is not null;
            await StopNetworkAsync();
            ShowStart(inGame ? $"{text} Щоб повернутися до партії, підключіться до того самого хоста під тим самим ім'ям." : text);
        }

        private async Task LeaveAsync()
        {
            await StopNetworkAsync();
            ShowStart(null);
        }

        private async Task StopNetworkAsync()
        {
            var (oldClient, oldHost) = (client, host);
            client = null;
            host = null;
            if (oldClient is not null)
            {
                await oldClient.DisposeAsync();
            }
            if (oldHost is not null)
            {
                await oldHost.DisposeAsync();
            }
        }

        // При закрытии окна ждать некогда: закрываем сеть в фоне, но не дольше пары секунд.
        private void StopNetworkOnClose()
        {
            Task.Run(StopNetworkAsync).Wait(TimeSpan.FromSeconds(2));
        }
    }
}
