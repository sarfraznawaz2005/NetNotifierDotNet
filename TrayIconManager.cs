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
    private bool _checkInProgress;

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

        // Right-click already opens the ContextMenuStrip by default. Left-click instead
        // runs a fresh check and pops a balloon with the result.
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _ = CheckAndNotifyAsync();
        };

        _app.StatusUpdated += OnStatusUpdated;
        _app.StatusChanged += OnStatusChanged;
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

        var checkNowItem = new ToolStripMenuItem("Check Now", null, (_, _) => _ = CheckAndNotifyAsync())
        {
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold),
        };
        menu.Items.Add(checkNowItem);

        menu.Items.Add("Copy IP", null, (_, _) => CopyIp());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Reset Statistics", null, (_, _) => ResetStatistics());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        return menu;
    }

    private async Task CheckAndNotifyAsync()
    {
        // Ignore rapid repeat clicks instead of piling up duplicate balloons that would
        // just repeat whatever the first, still-running check eventually finds.
        if (_checkInProgress) return;
        _checkInProgress = true;
        try
        {
            var status = await _app.CheckNowAsync();
            if (status == ConnectionStatus.Online)
                _notifyIcon.ShowBalloonTip(3000, "NetNotifier", "You are online.", ToolTipIcon.Info);
            else
                _notifyIcon.ShowBalloonTip(3000, "NetNotifier", "You are offline.", ToolTipIcon.Warning);
        }
        finally
        {
            _checkInProgress = false;
        }
    }

    private void CopyIp()
    {
        var ip = _app.GetPublicIp();
        if (ip != "N/A")
            System.Windows.Clipboard.SetText(ip);
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

    private void OnStatusChanged(ConnectionStatus status)
    {
        // NetNotifierApp raises this from a background thread/timer.
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (status == ConnectionStatus.Online)
                _notifyIcon.ShowBalloonTip(3000, "NetNotifier", "Connection restored.", ToolTipIcon.Info);
            else
                _notifyIcon.ShowBalloonTip(3000, "NetNotifier", "Connection lost.", ToolTipIcon.Warning);
        });
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
        _app.StatusChanged -= OnStatusChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _onlineIcon.Dispose();
        _offlineIcon.Dispose();
        _noInternetIcon.Dispose();
    }
}
