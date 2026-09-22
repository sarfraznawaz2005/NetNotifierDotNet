using System.Windows;
using NetNotifier.Models;
using NetNotifier.Services;
using MessageBox = System.Windows.MessageBox;

namespace NetNotifier;

public partial class SettingsWindow : Window
{
    private readonly NetNotifierApp _app;

    public SettingsWindow(NetNotifierApp app)
    {
        _app = app;
        InitializeComponent();

        TestUrlsBox.Text = string.Join(",", _app.Settings.TestUrls);
        IntervalBox.Text = (_app.Settings.IntervalMs / 1000).ToString();
        TimeoutBox.Text = _app.Settings.HttpTimeoutMs.ToString();
        VoiceAlertsCheckBox.IsChecked = _app.Settings.VoiceAlerts;
        StartWithWindowsCheckBox.IsChecked = StartupService.IsEnabled();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalBox.Text, out var intervalSeconds))
        {
            MessageBox.Show("Interval must be a whole number of seconds.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var intervalMs = intervalSeconds * 1000;
        if (intervalMs < Defaults.MinIntervalMs || intervalMs > Defaults.MaxIntervalMs)
        {
            MessageBox.Show(
                $"Interval must be between {Defaults.MinIntervalMs / 1000} and {Defaults.MaxIntervalMs / 1000} seconds.",
                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TimeoutBox.Text, out var timeoutMs) ||
            timeoutMs < Defaults.HttpTimeoutMinMs || timeoutMs > Defaults.HttpTimeoutMaxMs)
        {
            MessageBox.Show(
                $"HTTP timeout must be between {Defaults.HttpTimeoutMinMs} and {Defaults.HttpTimeoutMaxMs} ms.",
                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var urls = TestUrlsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (urls.Count == 0)
        {
            MessageBox.Show("Test URLs cannot be empty.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var badUrls = urls.Where(u => !Uri.TryCreate(u, UriKind.Absolute, out var parsed) ||
                                       (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)).ToList();
        if (badUrls.Count > 0)
        {
            MessageBox.Show(
                "Each test URL must start with http:// or https://. Invalid: " + string.Join(", ", badUrls),
                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newSettings = new AppSettings
        {
            IntervalMs = intervalMs,
            TestUrls = urls,
            VoiceAlerts = VoiceAlertsCheckBox.IsChecked == true,
            HttpTimeoutMs = timeoutMs,
        };

        _app.ApplySettings(newSettings);

        var startupApplied = StartupService.SetEnabled(StartWithWindowsCheckBox.IsChecked == true);
        if (!startupApplied)
        {
            MessageBox.Show(
                "Can't enable Start with Windows while running from source (dotnet run). Build the app first (build.bat), then set this from the built exe.",
                "Start with Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Close();
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var ok = await _app.TestConnectionAsync();
        MessageBox.Show(ok ? "Internet connection successful!" : "Internet connection failed!",
            "Test Result", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
