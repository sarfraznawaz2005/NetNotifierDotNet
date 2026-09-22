using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using NetNotifier.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace NetNotifier;

public class TrayIconManager : IDisposable
{
    private readonly NetNotifierApp _app;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _onlineIcon;
    private readonly Icon _offlineIcon;
    private readonly Icon _noInternetIcon;

    private SettingsWindow? _settingsWindow;

    public TrayIconManager(NetNotifierApp app)
    {
        _app = app;

        _onlineIcon = LoadIcon("online.ico");
        _offlineIcon = LoadIcon("offline.ico");
        _noInternetIcon = LoadIcon("no-internet.ico");

        _notifyIcon = new NotifyIcon
        {
            Icon = _noInternetIcon,
            Visible = true,
            Text = "NetNotifier - starting...",
            ContextMenuStrip = BuildContextMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _app.StatusUpdated += OnStatusUpdated;
    }

    private static Icon LoadIcon(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = $"NetNotifier.Resources.{fileName}";
        using var stream = asm.GetManifestResourceStream(resourceName);
        return stream != null ? new Icon(stream) : SystemIcons.Application;
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Check Now", null, (_, _) => _ = ManualCheckAsync());
        menu.Items.Add("Reset Statistics", null, (_, _) => ResetStatistics());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Restart", null, (_, _) => RestartApp());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        return menu;
    }

    private async Task ManualCheckAsync()
    {
        await Task.Run(() => _app.TestConnectionAsync());
    }

    private void ResetStatistics()
    {
        var result = MessageBox.Show("Reset all statistics?", "Reset Statistics",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
            _app.ResetStats();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_app);
        _settingsWindow.Show();
    }

    private static void RestartApp()
    {
        var exePath = Environment.ProcessPath;
        if (exePath != null)
            Process.Start(exePath);
        Application.Current.Shutdown();
    }

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        Application.Current.Shutdown();
    }

    private void OnStatusUpdated()
    {
        // NetNotifierApp raises this from a background thread/timer.
        Application.Current?.Dispatcher.Invoke(UpdateTrayDisplay);
    }

    private void UpdateTrayDisplay()
    {
        if (_app.LastStatus == ConnectionStatus.Online)
        {
            _notifyIcon.Icon = _onlineIcon;

            var uptime = _app.Uptime;
            var ip = _app.GetPublicIp();
            var availability = _app.AvailabilityPercent.ToString("F1");

            _notifyIcon.Text = Truncate(
                $"IP:\t{ip}\n" +
                $"Uptime:\t{uptime:hh\\:mm\\:ss}\n" +
                $"Drops:\t{_app.DisconnectsToday}\n" +
                $"Up:\t{availability}%", 127);
        }
        else
        {
            _notifyIcon.Icon = _offlineIcon;
            _notifyIcon.Text = "OFFLINE";
        }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];

    public void Dispose()
    {
        _app.StatusUpdated -= OnStatusUpdated;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _onlineIcon.Dispose();
        _offlineIcon.Dispose();
        _noInternetIcon.Dispose();
    }
}
