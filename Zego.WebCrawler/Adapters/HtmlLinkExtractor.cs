using AngleSharp;
using Zego.WebCrawler.Ports;

namespace Zego.WebCrawler.Adapters;

public class HtmlLinkExtractor : ILinkExtractor
{
    private readonly IBrowsingContext _context;

    public HtmlLinkExtractor()
    {
        var config = new Configuration();
        _context = BrowsingContext.New(config);
    }
    
    public async Task<IReadOnlyList<string>> ExtractAsync(string html, CancellationToken cancellationToken = default)
    {
        var document = await _context.OpenAsync(req => req.Content(html));
        var elements = document.QuerySelectorAll("a[href]");

        return elements
            .Select(element => element.GetAttribute("href")!)
            .ToList();
    }
}