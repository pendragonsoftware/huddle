using Huddle.Client;
using Huddle.Sample.Client.Services;
using System.ComponentModel;

namespace Huddle.Sample.Client;

public class ServersViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public List<ServerInformation> Servers { get; private set; } = new List<ServerInformation>();
    public bool HasMultipleServers => Servers.Count > 1;

    public void AddServer(ServerInformation serverInformation)
    {
        if (!Servers.Any(x => x.IpAddress == serverInformation.IpAddress))
        {
            Servers.Add(serverInformation);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Servers)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMultipleServers)));
        }
    }
}

public partial class MainPage : ContentPage, IDisposable
{
    private static readonly Color HuddleMuted = Color.FromArgb("#6E6E6E");
    private static readonly Color HuddleCoral = Color.FromArgb("#F4513F");
    private static readonly Color HuddleOrange = Color.FromArgb("#E44812");
    private static readonly Color HuddleAmber = Color.FromArgb("#FF9F2F");
    private static readonly Color HuddleError = Color.FromArgb("#C92F12");

    private readonly IServerDiscoveryService _serverDiscoveryService;
    private readonly ServerApiClient _serverApiClient;
    private readonly ServerQueueClient _serverQueueClient;

    public ServersViewModel ViewModel = new ServersViewModel();

    private bool _serverConnected = false;

    public MainPage(
        IServerDiscoveryService serverDiscoveryService,
        ServerApiClient serverApiClient,
        ServerQueueClient serverQueueClient)
    {
        _serverDiscoveryService = serverDiscoveryService;
        _serverApiClient = serverApiClient;
        _serverQueueClient = serverQueueClient;

        _serverDiscoveryService.ServerDiscovered += ServerDiscoveryService_ServerServiceFound;
        _serverDiscoveryService.ServerConnectionLost += ServerDiscoveryService_ServerServiceLost;
        _serverDiscoveryService.ServerConnectionConfirmed += ServerDiscoveryService_ConnectionToServer;

        InitializeComponent();
        BindingContext = ViewModel;

        labelDeviceId.Text = _serverDiscoveryService.DeviceId ?? "Unknown Device ID";

        labelIpAddress.Text = _serverDiscoveryService.IpAddress ?? "Unknown IP Address";

        UpdateServerSearchingButtons();

        if (_serverDiscoveryService.FoundServers.Any())
        {
            foreach (var foundServer in _serverDiscoveryService.FoundServers)
            {
                ViewModel.AddServer(foundServer);
            }

            _serverConnected = true;

            var server = _serverDiscoveryService.FoundServers.ElementAt(0);
            entryIpAddress.Text = server.IpAddress;
            entryPort.Text = (server.HttpPort.HasValue ? server.HttpPort.Value : 0).ToString();

            ReportServerFound(server);

            _ = Task.Run(async () => await _serverDiscoveryService.ConnectToServerAsync(server));

            _serverConnected = true;
        }
        else
        {
            entryIpAddress.Text = "192.168.0.16";
            entryPort.Text = 11_000.ToString();
        }
    }

    public void Dispose()
    {
        _serverDiscoveryService.ServerDiscovered -= ServerDiscoveryService_ServerServiceFound;
        _serverDiscoveryService.ServerConnectionLost -= ServerDiscoveryService_ServerServiceLost;
        _serverDiscoveryService.ServerConnectionConfirmed -= ServerDiscoveryService_ConnectionToServer;

        GC.SuppressFinalize(this);
    }

    private void ServerDiscoveryService_ServerServiceFound(object? sender, ServerInformation s)
    {
        Dispatcher.Dispatch(async () =>
        {
            ReportServerFound(s);

            ViewModel.AddServer(s);

            entryIpAddress.Text = s.IpAddress;
            entryPort.Text = s.HttpPort.ToString();

            UpdateServerSearchingButtons();

            await _serverDiscoveryService.ConnectToServerAsync(s);

            _serverConnected = true;
        });
    }

    private void ReportServerFound(ServerInformation serverInformation)
    {
        ConsoleWrite($"Found server at: {serverInformation.IpAddress}.", HuddleMuted);
        if (serverInformation.HttpPort.HasValue)
        {
            ConsoleWrite($"HTTP API service available {serverInformation.HttpPort.Value}", HuddleCoral);
        }
        if (serverInformation.QueuePort.HasValue)
        {
            ConsoleWrite($"Queue service available {serverInformation.QueuePort.Value}", HuddleOrange);
        }
        if (serverInformation.MessagingPort.HasValue)
        {
            ConsoleWrite($"Messaging service available {serverInformation.MessagingPort.Value}", HuddleAmber);
        }
    }

    private void ServerDiscoveryService_ConnectionToServer(object? sender, IConnectedServer e)
    {
        e.MessageReceived += ConnectedServer_MessageReceived;

        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite($"Connected to server with deviceId {e.DeviceId} and ip address {e.IpAddress}", HuddleAmber);
        });
    }

    private void ServerDiscoveryService_ServerServiceLost(object? sender, IConnectedServer e)
    {
        e.MessageReceived -= ConnectedServer_MessageReceived;

        Dispatcher.Dispatch(() =>
        {
            _serverConnected = false;
            ConsoleWrite("Server lost", HuddleError);

            UpdateServerSearchingButtons();
        });
    }

    private void ConnectedServer_MessageReceived(object? sender, string e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite($"Server sent message {e}", HuddleAmber);
        });
    }

    private async void OnCheckAvailableClicked(object sender, EventArgs e)
    {
        if (await DisplayAlertIfServerNotConnected())
        {
            return;
        }

        var isAvailable = await _serverApiClient.IsAvailableAsync();
        if (isAvailable)
        {
            await DisplayAlertAsync("Server Available", $"{entryIpAddress.Text}:{entryPort.Text} is available", "OK");
        }
        else
        {
            await DisplayAlertAsync("Server Not Available", $"{entryIpAddress.Text}:{entryPort.Text} is NOT available", "OK");
        }
    }

    private async void OnSendApiClicked(object sender, EventArgs e)
    {
        if (await DisplayAlertIfServerNotConnected())
        {
            return;
        }

        var sent = await _serverApiClient.SendMessageAsync(entryText.Text);
        if (sent)
        {
            ConsoleWrite($"Sent {entryText.Text}", HuddleCoral);
        }
        else
        {
            ConsoleWrite($"Failed to send {entryText.Text}", HuddleError);
        }
    }

    private async void OnSendQueueClicked(object sender, EventArgs e)
    {
        if (await DisplayAlertIfServerNotConnected())
        {
            return;
        }

        var sent = await _serverQueueClient.SendMessageAsync("scanpost", entryText.Text);
        if (sent)
        {
            ConsoleWrite($"Sent {entryText.Text}", HuddleOrange);
        }
        else
        {
            ConsoleWrite($"Failed to send {entryText.Text}", HuddleError);
        }
    }

    private async void OnSendMDNSClicked(object sender, EventArgs e)
    {
        if (await DisplayAlertIfServerNotConnected())
        {
            return;
        }

        if (_serverDiscoveryService.ConnectedServer == null)
        {
            await DisplayAlertAsync("Cannot Send Message", "Server Found But Not Connected To", "OK");
            return;
        }

        var sent = await _serverDiscoveryService.ConnectedServer.SendMessageAsync(entryText.Text);
        if (sent)
        {
            ConsoleWrite($"Sent {entryText.Text}", HuddleAmber);
        }
        else
        {
            ConsoleWrite($"Failed to send {entryText.Text}", HuddleError);
        }
    }

    private void OnSearchClicked(object sender, EventArgs e)
    {
        if (!_serverDiscoveryService.IsSearching)
        {
            const int timeoutSeconds = 5;
            ConsoleWrite($"Started searching for server, timeout in {timeoutSeconds} seconds");
            _ = Task.Run(() => _serverDiscoveryService.SearchAsync(TimeSpan.FromSeconds(timeoutSeconds)));

            UpdateServerSearchingButtons();
        }
        else
        {
            ConsoleWrite($"Already searching for server");
        }
    }

    private void OnSearchContinuouslyClicked(object sender, EventArgs e)
    {
        if (!_serverDiscoveryService.IsSearching)
        {
            ConsoleWrite($"Started searching for server (continuously)");
            _serverDiscoveryService.SearchContinuously();

            UpdateServerSearchingButtons();
        }
        else
        {
            ConsoleWrite($"Already searching for server");
        }
    }

    private async void OnStopSearchClicked(object sender, EventArgs e)
    {
        await _serverDiscoveryService.StopSearchingAsync();
        UpdateServerSearchingButtons();
        ConsoleWrite($"Stopped searching for server");
    }

    private void UpdateServerSearchingButtons()
    {
        buttonSearchForServer.IsVisible = !_serverDiscoveryService.IsSearching;
        buttonSearchContinuouslyForServer.IsVisible = !_serverDiscoveryService.IsSearching;
        buttonStopSearchForServer.IsVisible = _serverDiscoveryService.IsSearching;
    }

    private void OnUpdateIpAndPortClicked(object sender, EventArgs e)
    {
        _serverApiClient.PointAtServer(entryIpAddress.Text, int.Parse(entryPort.Text));
    }

    private async void OnLoadTestClicked(object sender, EventArgs e)
    {
        if (await DisplayAlertIfServerNotConnected())
        {
            return;
        }

        var toSend = int.Parse(entryLoadTest.Text);

        await _serverApiClient.StartLoadTestAsync(toSend);

        ConsoleWrite($"Started load test with {toSend}");

        _ = PerformLoadTest(toSend);
    }

    private async Task PerformLoadTest(int toSend)
    {
        //var waitInSeconds = 1 * 1000 / toSend;

        for (var i = 1; i <= toSend; i++)
        {
            await _serverApiClient.ContinueLoadTestAsync(i);

            //Thread.Sleep(waitInSeconds);
        }

        var response = await _serverApiClient.EndLoadTestAsync();

        ConsoleWrite($"Ended load test: {response}");
    }

    private async Task<bool> DisplayAlertIfServerNotConnected()
    {
        if (!_serverConnected)
        {
            await DisplayAlertAsync("Server Not Available", $"{entryIpAddress.Text}:{entryPort.Text} is NOT available", "OK");
            return true;
        }
        return false;
    }

    public void ConsoleWrite(string message, Color? colour = null)
    {
        var label = new Label()
        {
            Text = message,
            TextColor = colour ?? HuddleMuted
        };

        containerMessages.Children.Add(label);
    }

    private async void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ServerInformation server)
        {
            await _serverDiscoveryService.ConnectToServerAsync(server);
        }
    }
}
