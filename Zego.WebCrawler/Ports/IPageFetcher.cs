namespace Zego.WebCrawler.Ports;

public interface IPageFetcher
{
    Task<string?> FetchAsync(string url, CancellationToken cancellationToken = default);
}