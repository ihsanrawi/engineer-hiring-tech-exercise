namespace Zego.WebCrawler.Ports;

public interface IRobotsTxtChecker
{
    Task InitializeAsync(string baseUrl, CancellationToken cancellationToken = default);
    Task<bool> IsAllowedAsync(string url, CancellationToken cancellationToken = default);
}