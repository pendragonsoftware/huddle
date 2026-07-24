using Huddle.Server;
using MetroLog.Maui;

namespace Huddle.Sample.Server;

public partial class MainPage : ContentPage
{
    private static readonly Color HuddleMuted = Color.FromArgb("#6E6E6E");
    private static readonly Color HuddleCoral = Color.FromArgb("#F4513F");
    private static readonly Color HuddleOrange = Color.FromArgb("#E44812");
    private static readonly Color HuddleAmber = Color.FromArgb("#FF9F2F");
    private static readonly Color HuddleError = Color.FromArgb("#C92F12");

    private readonly IMobileServer _server;
    private readonly MessageBus _messageBus;

    public MainPage(IMobileServer server, MessageBus messageBus)
    {
        _server = server;
        _messageBus = messageBus;

        InitializeComponent();

        BindingContext = new LogController()
        {
            IsShakeEnabled = true
        };

        _server.StartedBroadcasting += Server_StartedBroadcasting;
        _server.ClientConnected += Server_ClientConnected;

        _messageBus.ApiMessageReceived += MessageBus_ApiMessageReceived;
        _messageBus.MessageReceived += MessageBus_MessageReceived;
        _messageBus.QueueMessageReceived += MessageBus_QueueMessageReceived;
        _messageBus.QueueMessageError += MessageBus_QueueMessageError;

        labelIpAddress.Text = _server.IpAddress ?? "Unknown IP Address";
        labelDeviceId.Text = _server.DeviceId ?? "Unknown Device ID";

        if (_server.IsBroadcasting && (_server.HttpApi?.IsRunning ?? true))
        {
            buttonStart.IsVisible = false;
            buttonStop.IsVisible = true;
        }
    }

    private void Server_ClientConnected(object? sender, (string DeviceId, string IpAddress) e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite($"Client with device id: {e.DeviceId} connected from IP Address: {e.IpAddress}", HuddleAmber);
        });
    }

    private void Server_StartedBroadcasting(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite($"Broadcasting - {_server.IpAddress} ({_server.DeviceId}):{_server.HttpApi?.Port}/{_server.Queue?.Port}/{_server.MessagingPort}",
                HuddleMuted);

            buttonStart.IsVisible = false;
            buttonStop.IsVisible = true;
        });
    }

    private void MessageBus_ApiMessageReceived(object? sender, string e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite(e, HuddleCoral);
        });
    }

    private void MessageBus_MessageReceived(object? sender, string e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite(e, HuddleAmber);
        });
    }

    private void MessageBus_QueueMessageReceived(object? sender, string e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite(e, HuddleOrange);
        });
    }

    private void MessageBus_QueueMessageError(object? sender, string e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite(e, HuddleError);
        });
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        ConsoleWrite($"Sent {entryText.Text} to clients");
        var result = await _server.SendMessageToClientsAsync(entryText.Text);

        var message = $"Sent to {result.IpAddresses.Length} clients ({string.Join(",", result.IpAddresses)})";
        ConsoleWrite(message, HuddleAmber);

        if (result.UnableToSendTo.Length > 0)
        {
            message = $"Unable to send to {result.UnableToSendTo.Length} clients ({string.Join(",", result.UnableToSendTo)})";
            ConsoleWrite(message, HuddleError);
        }
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await _server.StartAsync();
        ConsoleWrite("Server manually started");

        buttonStart.IsVisible = false;
        buttonStop.IsVisible = true;
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        await _server.StopAsync();
        ConsoleWrite("Server manually stopped");

        buttonStart.IsVisible = true;
        buttonStop.IsVisible = false;
    }

    private void ConsoleWrite(string message, Color? colour = null)
    {
        var label = new Label()
        {
            Text = message,
            TextColor = colour ?? HuddleMuted
        };

        containerMessages.Children.Add(label);
    }
}
