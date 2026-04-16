using Zego.WebCrawler.Domain;
using Zego.WebCrawler.Ports;

namespace Zego.WebCrawler.Adapters;

public class ConsoleResultWriter : ICrawlResultWriter
{
    public Task WriteAsync(CrawlerResult result, CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine($"Crawl completed for: {result.StartUrl}");
        Console.WriteLine($"Total pages visited: {result.VisitedUrls.Count}");
        Console.WriteLine();
        
        Console.WriteLine("Visited URLs:");
        foreach (var url in result.VisitedUrls)
        {
            Console.WriteLine($"  {url}");
            if (result.LinksByPage.TryGetValue(url, out var links) && links.Count > 0)
            {
                Console.WriteLine($"    Links found ({links.Count}):");
                foreach (var link in links)
                {
                    Console.WriteLine($"      → {link}");
                }
            }

            Console.WriteLine();
        }
        
        return Task.CompletedTask;
    }
}