namespace Zego.WebCrawler.Domain;

public record CrawlerResult(string StartUrl, IReadOnlyList<string> VisitedUrls, IReadOnlyDictionary<string, List<string>> LinksByPage);