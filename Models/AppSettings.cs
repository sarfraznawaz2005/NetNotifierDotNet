namespace NetNotifier.Models;

public class AppSettings
{
    public int IntervalMs { get; set; } = Defaults.DefaultIntervalMs;
    public List<string> TestUrls { get; set; } = new() { "https://www.google.com", "https://www.bing.com" };
    public bool VoiceAlerts { get; set; } = true;
    public int HttpTimeoutMs { get; set; } = Defaults.HttpTimeoutDefaultMs;
    public bool StartWithWindows { get; set; } = false;
}

public static class Defaults
{
    public const int DefaultIntervalMs = 5_000;
    public const int MinIntervalMs = 5_000;
    public const int MaxIntervalMs = 300_000;

    public const int HttpTimeoutDefaultMs = 20_000;
    public const int HttpTimeoutMinMs = 5_000;
    public const int HttpTimeoutMaxMs = 30_000;

    public const int IpCacheTtlMs = 5 * 60_000;
}
