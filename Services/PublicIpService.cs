using System.Net.Http;
using NetNotifier.Models;

namespace NetNotifier.Services;

public class PublicIpService
{
    private static readonly string[] Providers = { "https://api.ipify.org/", "https://icanhazip.com/" };

    private readonly HttpClient _httpClient = new();

    private string _cache = "N/A";
    private DateTime _lastFetch = DateTime.MinValue;
    private bool _isFetching;

    public string CachedIp => _cache;

    /// <summary>Returns the cached IP immediately and kicks off a background refresh if the cache is stale.</summary>
    public string GetCachedAndRefresh()
    {
        if (!_isFetching && DateTime.UtcNow - _lastFetch > TimeSpan.FromMilliseconds(Defaults.IpCacheTtlMs))
        {
            _ = RefreshAsync();
        }
        return _cache;
    }

    public async Task RefreshAsync()
    {
        if (_isFetching) return;
        _isFetching = true;
        try
        {
            foreach (var provider in Providers)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var response = await _httpClient.GetStringAsync(provider, cts.Token);
                    var ip = response.Trim();
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        _cache = ip;
                        _lastFetch = DateTime.UtcNow;
                        return;
                    }
                }
                catch
                {
                    // try next provider
                }
            }
            _cache = "N/A";
            _lastFetch = DateTime.UtcNow;
        }
        finally
        {
            _isFetching = false;
        }
    }

    public void Reset()
    {
        _cache = "N/A";
        _lastFetch = DateTime.MinValue;
    }
}
