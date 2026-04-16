using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Zego.WebCrawler.Ports;

namespace Zego.WebCrawler.Domain;

public class CrawlerEngine : ICrawlerEngine
{
    private readonly ILogger<CrawlerEngine> _logger;
    private readonly IPageFetcher _pageFetcher;
    private readonly ILinkExtractor _linkExtractor;
    private readonly IUrlNormalizer _urlNormalizer;
    private readonly IRobotsTxtChecker _robotsTxtChecker;

    private readonly ConcurrentDictionary<string, bool> _visitedUrls;
    private readonly ConcurrentQueue<string> _urlQueue;
    private readonly ConcurrentDictionary<string, List<string>> _linksByPage;

    public CrawlerEngine(ILogger<CrawlerEngine> logger, IPageFetcher pageFetcher, ILinkExtractor linkExtractor, IUrlNormalizer urlNormalizer, IRobotsTxtChecker robotsTxtChecker)
    {
        _logger = logger;
        _pageFetcher = pageFetcher;
        _linkExtractor = linkExtractor;
        _urlNormalizer = urlNormalizer;
        _robotsTxtChecker = robotsTxtChecker;
        _visitedUrls = [];
        _urlQueue = [];
        _linksByPage = [];
    }

    public async Task<CrawlerResult> CrawlAsync(string startUrl, bool respectRobots = true, int maxConcurrentTasks = 5, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(startUrl, UriKind.Absolute, out Uri startUri))
        {
            throw new ArgumentException("Invalid URL format", nameof(startUrl));
        }

        if (startUri.Scheme != Uri.UriSchemeHttp && startUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Url must use http or https scheme", nameof(startUrl));
        }
        
        if(respectRobots)
        {
            await _robotsTxtChecker.InitializeAsync(startUrl, cancellationToken);
        }

        var baseDomain = startUri.Host;
        
        _visitedUrls[startUrl] = true;
        _urlQueue.Enqueue(startUrl);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrentTasks,
            CancellationToken = cancellationToken
        };

        // Keep processing until queue is empty and no tasks are running
        var activeTaskCount = 0;
        var activeTasksLock = new object();
        while (!_urlQueue.IsEmpty || activeTaskCount > 0)
        {
            var urlsToProcess = new List<string>();

            // Dequeue all available URLs
            while (_urlQueue.TryDequeue(out var url))
            {
                urlsToProcess.Add(url);
            }

            if (urlsToProcess.Count == 0)
            {
                await Task.Delay(10, cancellationToken); // Small delay before checking again
                continue;
            }

            // Process this batch in parallel
            lock (activeTasksLock)
            {
                activeTaskCount += urlsToProcess.Count;
            }

            try
            {
                await Parallel.ForEachAsync(
                    urlsToProcess,
                    options,
                    async (url, token) =>
                    {
                        try
                        {
                            await ProcessUrlAsync(url, baseDomain, respectRobots, token);
                        }
                        finally
                        {
                            lock (activeTasksLock)
                            {
                                activeTaskCount--;
                            }
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                lock (activeTasksLock)
                {
                    activeTaskCount -= urlsToProcess.Count;
                }
                
                _logger.LogError(ex, "Error processing batch of URLs");
                throw;
            }
            
        }

        return new CrawlerResult(
            startUrl,
            _visitedUrls.Keys.ToList(),
            _linksByPage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        );
    }

    private async Task ProcessUrlAsync(string url, string baseDomain, bool respectRobots, CancellationToken token)
    {
        try
        {
            if (respectRobots && !await _robotsTxtChecker.IsAllowedAsync(url, token))
            {
                _logger.LogInformation("URL blocked by robots.txt: {Url}", url);
                return;
            }
            
            // Fetch HTML
            var html = await _pageFetcher.FetchAsync(url, token);
            if (string.IsNullOrEmpty(html))
            {
                _logger.LogWarning("Failed to fetch URL: {Url}", url);
                return;
            }

            // Extract links
            var rawLinks = await _linkExtractor.ExtractAsync(html, token);
            if (rawLinks == null || rawLinks.Count == 0)
            {
                _logger.LogInformation("No links found on page: {Url}", url);
                return;
            }

            _linksByPage[url] = rawLinks.ToList();
            
            // Normalize and add new URLs to queue
            foreach (var rawLink in rawLinks)
            {
                var normalized = _urlNormalizer.Normalize(rawLink, url);
                if (normalized == null)
                {
                    continue;
                }
                
                var normalizedUri = new Uri(normalized);
                if (normalizedUri.Host != baseDomain) // Different domain, skip
                {
                    continue;
                }

                // add to queue if not visited
                if (_visitedUrls.TryAdd(normalized, true))
                {
                    _urlQueue.Enqueue(normalized);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing URL: {Url}", url);
        }
    }
}