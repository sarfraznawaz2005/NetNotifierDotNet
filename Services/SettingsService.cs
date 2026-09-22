using System.IO;
using System.Text.Json;
using NetNotifier.Models;

namespace NetNotifier.Services;

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetNotifier");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return Validate(settings);
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }

    private static AppSettings Validate(AppSettings settings)
    {
        settings.IntervalMs = Math.Clamp(settings.IntervalMs, Defaults.MinIntervalMs, Defaults.MaxIntervalMs);
        settings.HttpTimeoutMs = Math.Clamp(settings.HttpTimeoutMs, Defaults.HttpTimeoutMinMs, Defaults.HttpTimeoutMaxMs);
        if (settings.TestUrls == null || settings.TestUrls.Count == 0)
            settings.TestUrls = new List<string> { "https://www.google.com", "https://www.bing.com" };
        return settings;
    }
}
