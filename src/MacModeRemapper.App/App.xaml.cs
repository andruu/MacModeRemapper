using System.Windows;

namespace MacModeRemapper.App;

public partial class App : System.Windows.Application
{
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent multiple instances
        bool createdNew;
        var mutex = new System.Threading.Mutex(true, "MacModeRemapper_SingleInstance", out createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Mac Mode Remapper is already running.",
                "Mac Mode Remapper",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            _trayIcon = new TrayIcon();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to start Mac Mode Remapper:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
