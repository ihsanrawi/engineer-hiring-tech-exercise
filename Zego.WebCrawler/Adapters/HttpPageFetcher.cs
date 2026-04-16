using Zego.WebCrawler.Ports;

namespace Zego.WebCrawler.Adapters;

public class HttpPageFetcher(HttpClient httpClient) : IPageFetcher
{
    public async Task<string?> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}