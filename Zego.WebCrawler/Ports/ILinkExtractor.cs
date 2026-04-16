namespace Zego.WebCrawler.Ports;

public interface ILinkExtractor
{
    Task<IReadOnlyList<string>> ExtractAsync(string html, CancellationToken cancellationToken = default);
}