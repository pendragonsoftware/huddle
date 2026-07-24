using MetroLog.Maui;

namespace Huddle.Sample.Client;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App()
    {
        InitializeComponent();

        _shell = new AppShell();

        LogController.InitializeNavigation(
            page => _shell.Navigation.PushModalAsync(page),
            () => _shell.Navigation.PopModalAsync());
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_shell);
    }
}
