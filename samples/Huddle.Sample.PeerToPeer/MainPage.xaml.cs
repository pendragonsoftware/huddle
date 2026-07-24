using Huddle.PeerToPeer;
using System.Collections.ObjectModel;

namespace Huddle.Sample.PeerToPeer;

public record Peer(string DisplayName);

public partial class MainPage : ContentPage
{
    private static readonly Color HuddleMuted = Color.FromArgb("#6E6E6E");
    private static readonly Color HuddleCoral = Color.FromArgb("#F4513F");
    private static readonly Color HuddleOrange = Color.FromArgb("#E44812");
    private static readonly Color HuddleAmber = Color.FromArgb("#FF9F2F");
    private static readonly Color HuddleError = Color.FromArgb("#C92F12");

    private readonly IMessagingService _messagingService;
    private readonly MessageBus _messageBus;

    public ObservableCollection<Peer> Peers = new();

    public MainPage(IMessagingService messagingService, MessageBus messageBus)
    {
        _messagingService = messagingService;
        _messageBus = messageBus;

        InitializeComponent();

        collectionView.ItemsSource = Peers;

        _messagingService.IsBroadcastingChanged += MessagingService_IsBroadcastingChanged;
        _messagingService.PeerDiscovered += MessagingService_PeerDiscovered;
        _messagingService.PeerLost += MessagingService_PeerLost;

        _messageBus.MessageReceived += MessageBus_MessageReceived;

        labelDisplayName.Text = _messagingService.DisplayName ?? "Unknown Display Name";

        if (_messagingService.IsBroadcasting)
        {
            buttonStart.IsVisible = false;
            buttonStop.IsVisible = true;
        }
    }

    private void MessagingService_IsBroadcastingChanged(object? sender, bool e)
    {
        Dispatcher.Dispatch(() =>
        {
            buttonStart.IsVisible = !e;
            buttonStop.IsVisible = e;
        });
    }

    private void MessagingService_PeerDiscovered(object? sender, string e)
    {
        Dispatcher.Dispatch(() =>
        {
            Peers.Add(new Peer(e));
            ConsoleWrite($"Added peer with display name: {e}", HuddleOrange);
        });
    }

    private void MessagingService_PeerLost(object? sender, string e)
    {
        Dispatcher.Dispatch(() =>
        {
            var index = Peers.ToList().FindIndex(x => x.DisplayName == e);
            if (index >= 0)
            {
                Peers.RemoveAt(index);
            }

            ConsoleWrite($"Lost peer with display name: {e}", HuddleAmber);
        });
    }

    private void MessageBus_MessageReceived(object? sender, (string Message, string DisplayName) e)
    {
        Dispatcher.Dispatch(() =>
        {
            ConsoleWrite($"{e.Message} from {e.DisplayName}", HuddleCoral);
        });
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (collectionView.SelectedItem == null)
        {
            await DisplayAlertAsync("No Selected Peer", "Please select a peer in the left panel to send a message", "OK");
            return;
        }

        var selectedItem = (Peer)collectionView.SelectedItem;
        var displayName = selectedItem.DisplayName;

        ConsoleWrite($"Sending {entryText.Text} to {displayName}");
        var result = await _messagingService.SendAsync(entryText.Text, displayName);

        if (result)
        {
            ConsoleWrite($"Sent {entryText.Text} to {displayName}", HuddleCoral);
        }
        else
        {
            ConsoleWrite($"Failed to send {entryText.Text} to {displayName}", HuddleError);
        }
    }

    private async void OnSendAllClicked(object sender, EventArgs e)
    {
        ConsoleWrite($"Sending to {entryText.Text} to clients");
        var result = await _messagingService.SendToAllAsync(entryText.Text);

        var message = $"Sent to {result.IpAddresses.Length} peers ({string.Join(",", result.IpAddresses)})";
        ConsoleWrite(message, HuddleAmber);

        if (result.UnableToSendTo.Length > 0)
        {
            message = $"Unable to send to {result.UnableToSendTo.Length} peers ({string.Join(",", result.UnableToSendTo)})";
            ConsoleWrite(message, HuddleError);
        }
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await _messagingService.StartAsync();
        ConsoleWrite("Messaging service manually started");

        buttonStart.IsVisible = false;
        buttonStop.IsVisible = true;
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        await _messagingService.StopAsync();
        ConsoleWrite("Messaging service manually stopped");

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
