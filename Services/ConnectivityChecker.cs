using System.Net.NetworkInformation;
using System.Net.Http;

namespace NetNotifier.Services;

public class ConnectivityChecker
{
    private readonly HttpClient _httpClient;

    public ConnectivityChecker()
    {
        var handler = new HttpClientHandler();
        _httpClient = new HttpClient(handler);
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

    public async Task<bool> CheckHttpMultiAsync(IEnumerable<string> urls, int timeoutMs, CancellationToken cancellationToken)
    {
        foreach (var url in urls)
        {
            if (await CheckHttpAsync(url, timeoutMs, cancellationToken))
                return true;
        }
        return false;
    }

    private async Task<bool> CheckHttpAsync(string url, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
