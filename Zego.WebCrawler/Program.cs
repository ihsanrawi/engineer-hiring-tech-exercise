using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Zego.WebCrawler.Adapters;
using Zego.WebCrawler.Domain;
using Zego.WebCrawler.Ports;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        
        // Suppress verbose HTTP request logs
        logging.AddFilter("System.Net.Http", LogLevel.Warning);
        logging.AddFilter("Microsoft.AspNetCore.HostFiltering", LogLevel.Warning);
    })
    .ConfigureServices(services =>
    {
        services.AddHttpClient<IPageFetcher, HttpPageFetcher>()
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

        services.AddHttpClient();
        services.AddSingleton<IRobotsTxtChecker, RobotsTxtChecker>();
        services.AddSingleton<ILinkExtractor, HtmlLinkExtractor>();
        services.AddSingleton<IUrlNormalizer, UrlNormalizer>();
        services.AddSingleton<ICrawlerEngine, CrawlerEngine>();
        services.AddSingleton<ICrawlResultWriter, ConsoleResultWriter>();
    })
    .Build();

var engine = host.Services.GetRequiredService<ICrawlerEngine>();
var writer = host.Services.GetRequiredService<ICrawlResultWriter>();

var respectRobots = !args.Contains("--no-respect-robots");

var maxConcurrentIndex = Array.IndexOf(args, "--max-concurrent");
var maxConcurrent = 5; // Default
if (maxConcurrentIndex >= 0 && maxConcurrentIndex + 1 < args.Length)
{
    if (int.TryParse(args[maxConcurrentIndex + 1], out var parsedValue))
    {
        maxConcurrent = Math.Max(1, Math.Min(10, parsedValue)); // Clamp between 1 and 10
    }
    else
    {
        Console.WriteLine("Error: --max-concurrent must be a number between 1 and 10");
        return 1;
    }
}

try
{
    if (args.Length == 0)
    {
        Console.WriteLine("Usage: Zego.WebCrawler <url> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <url>                 The URL to start crawling from (must be first argument)");
        Console.WriteLine("  --no-respect-robots    Ignore robots.txt rules");
        Console.WriteLine("  --max-concurrent N     Max concurrent requests (1-10, default: 5)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Zego.WebCrawler https://example.com");
        Console.WriteLine("  Zego.WebCrawler https://example.com --max-concurrent 10");
        Console.WriteLine("  Zego.WebCrawler https://example.com --no-respect-robots --max-concurrent 3");

        return 1;
    }

    var url = args[0];
    if (url.StartsWith("--"))
    {
        Console.WriteLine("Error: First argument must be a URL, not an option");
        Console.WriteLine("Usage: Zego.WebCrawler <url> [options]");
        return 1;
    }

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        Console.WriteLine("Cancellation requested...");
        cts.Cancel();
        e.Cancel = true;
    };

    var result = await engine.CrawlAsync(url, respectRobots, maxConcurrent, cts.Token);
    await writer.WriteAsync(result,  cts.Token);
    return 0;

}
catch (ArgumentException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Network error: {ex.Message}");
    return 2;
}
catch (OperationCanceledException)
{
    Console.WriteLine($"Crawl cancelled.");
    return 3;
}
catch (Exception e)
{
    Console.WriteLine($"Unexpected error: {e.Message}");
    return 99;
}