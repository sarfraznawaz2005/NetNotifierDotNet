using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace NetNotifier;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "NetNotifier-SingleInstance-Mutex";

    private Mutex? _singleInstanceMutex;
    private NetNotifierApp? _netNotifierApp;
    private TrayIconManager? _trayIconManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("NetNotifier is already running.", "NetNotifier", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _netNotifierApp = new NetNotifierApp();
        _trayIconManager = new TrayIconManager(_netNotifierApp);
        _netNotifierApp.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        _netNotifierApp?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
