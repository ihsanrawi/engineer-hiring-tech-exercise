using Zego.WebCrawler.Domain;

namespace Zego.WebCrawler.Ports;

public interface ICrawlResultWriter
{
    Task WriteAsync(CrawlerResult result, CancellationToken cancellationToken = default);
}