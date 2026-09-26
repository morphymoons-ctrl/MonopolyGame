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
            Screen.Content = new StartScreen(settings, CreateGameAsync, JoinGameAsync, message);
        }

        private async Task<string?> CreateGameAsync(string name)
        {
            try
            {
                host = await GameHost.StartAsync();
            }
            catch (IOException)
            {
                return $"Порт {NetDefaults.Port} занят — похоже, игра уже создана на этом компьютере. " +
                    "Чтобы зайти в неё второй копией, подключитесь к 127.0.0.1.";
            }

            var error = await ConnectAsync("127.0.0.1", name, host.HostToken);
            if (error is not null)
            {
                await StopNetworkAsync();
            }
            return error;
        }

        private async Task<string?> JoinGameAsync(string name, string address)
        {
            var error = await ConnectAsync(address, name, null);
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
                return $"Неверный адрес: {address}.";
            }

            // Сообщения приходят из потоков сети — переносим их в поток окна.
            // Сообщения от старого подключения (после выхода) не показываем.
            client = newClient;
            lastLobby = null;
            newClient.LobbyChanged += s => OnUi(newClient, () => OnLobby(s));
            newClient.GameStarted += info => OnUi(newClient, () => OnGameStarted(info));
            newClient.GameUpdated += update => OnUi(newClient, () => gameScreen?.Apply(update));
            newClient.Notice += text => OnUi(newClient, () => gameScreen?.AddLog(text));
            newClient.ConnectionLost += text => OnUi(newClient, () => OnConnectionLost(text));

            var error = await newClient.ConnectAsync(name, hostToken);
            if (error is not null)
            {
                return error;
            }

            lobbyScreen = new LobbyScreen(newClient, hostToken is not null, LeaveAsync);
            if (lastLobby is not null)
            {
                lobbyScreen.Show(lastLobby);
            }
            Screen.Content = lobbyScreen;
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

        private void OnGameStarted(GameStartInfo info)
        {
            lobbyScreen = null;
            gameScreen = new GameScreen(client!, info, settings, LeaveAsync);
            Screen.Content = gameScreen;
        }

        private async void OnConnectionLost(string text)
        {
            await StopNetworkAsync();
            ShowStart(text);
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
