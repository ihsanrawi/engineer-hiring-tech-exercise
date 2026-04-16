using Zego.WebCrawler.Domain;

namespace Zego.WebCrawler.Ports;

public interface ICrawlerEngine
{
    Task<CrawlerResult> CrawlAsync(string startUrl, bool respectRobots = true, int maxConcurrentTasks = 5, CancellationToken cancellationToken = default);
}