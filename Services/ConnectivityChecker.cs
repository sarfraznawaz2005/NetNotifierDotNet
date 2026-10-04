using System.Net.NetworkInformation;
using System.Net.Http;

namespace NetNotifier.Services;

public class ConnectivityChecker
{
    private const int MaxAttempts = 3;
    private const int RetryDelayMs = 1_500;
    private const int RetryTimeoutMs = 5_000;

    private readonly HttpClient _httpClient;

    public ConnectivityChecker()
    {
        var handler = new SocketsHttpHandler
        {
            // Drop old connections so a Wi-Fi/VPN change does not leave a dead one in the pool.
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            // Do not follow redirects: captive portals (hotel/cafe Wi-Fi) redirect to a login page.
            AllowAutoRedirect = false
        };
        _httpClient = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
    }

    public bool HasConnectedNetworkInterface()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(ni =>
                ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
        }
        catch
        {
            // Assume connected if we can't check, to avoid false offline.
            return true;
        }
    }

    public async Task<(bool Success, long? LatencyMs)> CheckHttpMultiAsync(IEnumerable<string> urls, int timeoutMs, CancellationToken cancellationToken)
    {
        var urlList = urls.ToList();
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(RetryDelayMs, cancellationToken);

            // Retry rounds use a shorter timeout so a real outage is reported quickly.
            var roundTimeoutMs = attempt == 0 ? timeoutMs : Math.Min(timeoutMs, RetryTimeoutMs);
            var result = await CheckRoundAsync(urlList, roundTimeoutMs, cancellationToken);
            if (result.Success)
                return result;
        }
        return (false, null);
    }

    /// <summary>Checks all URLs at the same time and returns as soon as one succeeds.</summary>
    private async Task<(bool Success, long? LatencyMs)> CheckRoundAsync(List<string> urls, int timeoutMs, CancellationToken cancellationToken)
    {
        using var roundCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pending = urls.Select(url => CheckHttpAsync(url, timeoutMs, roundCts.Token)).ToList();
        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending);
            pending.Remove(done);
            var result = await done;
            if (result.Success)
            {
                roundCts.Cancel();
                return result;
            }
        }
        return (false, null);
    }

    private static async Task<bool> IsValidResponseAsync(string url, HttpResponseMessage response, CancellationToken token)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;

        // Google check page: must be exactly 204 (a login page would return 200 or a redirect).
        if (path.EndsWith("/generate_204", StringComparison.OrdinalIgnoreCase))
            return response.StatusCode == System.Net.HttpStatusCode.NoContent;

        // Microsoft check page: must be 200 with the exact expected text.
        if (uri.Host.EndsWith("msftconnecttest.com", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith("/connecttest.txt", StringComparison.OrdinalIgnoreCase))
        {
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                return false;
            var body = await response.Content.ReadAsStringAsync(token);
            return body.Trim() == "Microsoft Connect Test";
        }

        // Any other URL: a reply below 400 (including a redirect) means the server was reached.
        return (int)response.StatusCode < 400;
    }

    private async Task<(bool Success, long? LatencyMs)> CheckHttpAsync(string url, int timeoutMs, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            var valid = await IsValidResponseAsync(url, response, linkedCts.Token);
            stopwatch.Stop();
            return (valid, valid ? stopwatch.ElapsedMilliseconds : null);
        }
        catch
        {
            return (false, null);
        }
    }
}
