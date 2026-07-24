using MetroLog.Maui;

namespace Huddle.Sample.Server;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    private void ToolbarItem_Clicked(object sender, EventArgs e)
    {
        var logController = new LogController();
        logController.GoToLogsPageCommand.Execute(null);
    }
}
