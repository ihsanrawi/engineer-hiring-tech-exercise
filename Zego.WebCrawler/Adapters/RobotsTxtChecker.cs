using Microsoft.Extensions.Logging;
using Zego.WebCrawler.Ports;

namespace Zego.WebCrawler.Adapters;

public class RobotsTxtChecker : IRobotsTxtChecker
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RobotsTxtChecker> _logger;
    private readonly List<string> _disallowedPaths = [];
    private readonly List<string> _allowedPaths = [];
    private readonly object _lock = new();

    public RobotsTxtChecker(ILogger<RobotsTxtChecker> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task InitializeAsync(string baseUrl, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(baseUrl);
        var robotsUrl = $"{baseUri.Scheme}://{baseUri.Host}/robots.txt";

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(robotsUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                ParseRobotsTxt(content);
                _logger.LogInformation("Successfully fetched and parsed robots.txt from {Url}", robotsUrl);
            }
        }
        catch (HttpRequestException )
        {
            _logger.LogWarning("No robots.txt file found at {Url}", robotsUrl);
        }
    }

    public async Task<bool> IsAllowedAsync(string url, CancellationToken cancellationToken)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath;

        lock (_lock)
        {
            foreach (var allowedPath in _allowedPaths)
            {
                if(path.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (var disallowedPath in _disallowedPaths)
            {
                if(path.StartsWith(disallowedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ParseRobotsTxt(string content)
    {
        string? userAgent = null;

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }
            
            var parts = trimmed.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key.ToLower())
            {
                case "user-agent":
                    userAgent = value;
                    break;
                case "disallow":
                    if (userAgent == "*")
                    {
                        if(string.IsNullOrEmpty(value))
                        {
                            _disallowedPaths.Clear();
                            _allowedPaths.Clear();
                        }
                        else
                        {
                            _disallowedPaths.Add(value);
                        }
                    }
                    break;
                case "allow":
                    if (userAgent == "*")
                    {
                        _allowedPaths.Add(value);
                    }
                    break;
            }
        }
    }
}