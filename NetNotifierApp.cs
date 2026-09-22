using NetNotifier.Models;
using NetNotifier.Services;

namespace NetNotifier;

public enum ConnectionStatus { Unknown, Online, Offline }

public class NetNotifierApp : IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly ConnectivityChecker _connectivityChecker = new();
    private readonly PublicIpService _publicIpService = new();
    private readonly SpeechService _speechService = new();

    private System.Threading.Timer? _timer;
    private bool _isChecking;
    private bool _firstRun = true;
    private int _statusChangeCount;
    private DateTime _lastStatusChangeUtc = DateTime.MinValue;

    public AppSettings Settings { get; private set; }
    public ConnectionStatus LastStatus { get; private set; } = ConnectionStatus.Unknown;
    public DateTime OnlineSinceUtc { get; private set; } = DateTime.UtcNow;
    public int DisconnectsToday { get; private set; }
    public int TotalChecks { get; private set; }
    public int SuccessfulChecks { get; private set; }

    /// <summary>Raised on the check thread after every check. Subscribers must marshal to the UI thread themselves.</summary>
    public event Action? StatusUpdated;

    public NetNotifierApp()
    {
        Settings = _settingsService.Load();
    }

    public void Start()
    {
        _ = CheckConnectionAsync();
        _timer = new System.Threading.Timer(_ => _ = CheckConnectionAsync(), null, Settings.IntervalMs, Settings.IntervalMs);
    }

    public void ApplySettings(AppSettings newSettings)
    {
        Settings = newSettings;
        _settingsService.Save(Settings);
        _timer?.Change(Settings.IntervalMs, Settings.IntervalMs);
    }

    public async Task<bool> TestConnectionAsync()
    {
        return await _connectivityChecker.CheckHttpMultiAsync(Settings.TestUrls, Settings.HttpTimeoutMs, CancellationToken.None);
    }

    /// <summary>Runs a real check (updates status, stats, and the tray icon) and returns the resulting status.</summary>
    public async Task<ConnectionStatus> CheckNowAsync()
    {
        await CheckConnectionAsync();
        return LastStatus;
    }

    public void ResetStats()
    {
        DisconnectsToday = 0;
        TotalChecks = 0;
        SuccessfulChecks = 0;
        OnlineSinceUtc = DateTime.UtcNow;
        StatusUpdated?.Invoke();
    }

    public string GetPublicIp() => Settings != null && LastStatus == ConnectionStatus.Online
        ? _publicIpService.GetCachedAndRefresh()
        : "N/A";

    public double AvailabilityPercent => TotalChecks > 0 ? (double)SuccessfulChecks / TotalChecks * 100 : 0;

    public TimeSpan Uptime => LastStatus == ConnectionStatus.Online ? DateTime.UtcNow - OnlineSinceUtc : TimeSpan.Zero;

    private async Task CheckConnectionAsync()
    {
        if (_isChecking) return;
        _isChecking = true;
        try
        {
            var status = await DetermineConnectionStatusAsync();

            if (status != LastStatus || _firstRun)
            {
                HandleStatusChange(status, LastStatus);
                LastStatus = status;
                _firstRun = false;
            }

            TotalChecks++;
            if (status == ConnectionStatus.Online)
                SuccessfulChecks++;

            StatusUpdated?.Invoke();
        }
        finally
        {
            _isChecking = false;
        }
    }

    private async Task<ConnectionStatus> DetermineConnectionStatusAsync()
    {
        if (!_connectivityChecker.HasConnectedNetworkInterface())
            return ConnectionStatus.Offline;

        var online = await _connectivityChecker.CheckHttpMultiAsync(Settings.TestUrls, Settings.HttpTimeoutMs, CancellationToken.None);
        return online ? ConnectionStatus.Online : ConnectionStatus.Offline;
    }

    private void HandleStatusChange(ConnectionStatus newStatus, ConnectionStatus oldStatus)
    {
        var now = DateTime.UtcNow;

        if (newStatus != oldStatus)
        {
            _statusChangeCount++;
            if ((now - _lastStatusChangeUtc).TotalMilliseconds < Settings.IntervalMs && _statusChangeCount > 2)
            {
                // Ignore rapid flapping.
                return;
            }
            _lastStatusChangeUtc = now;
            _statusChangeCount = 0;
        }
        else
        {
            _statusChangeCount = 0;
        }

        if (newStatus == ConnectionStatus.Online)
        {
            if (Settings.VoiceAlerts && oldStatus != ConnectionStatus.Online && !_firstRun)
                _speechService.SpeakAsync("Connection Restored");

            if (oldStatus != ConnectionStatus.Online || _firstRun)
            {
                OnlineSinceUtc = now;
                _ = _publicIpService.RefreshAsync();
            }
        }
        else
        {
            if (Settings.VoiceAlerts && oldStatus != ConnectionStatus.Offline && !_firstRun)
                _speechService.SpeakAsync("Connection Lost");

            if (oldStatus == ConnectionStatus.Online && !_firstRun)
                DisconnectsToday++;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _speechService.Dispose();
    }
}
