namespace Zego.WebCrawler.Ports;

public interface IUrlNormalizer
{
    string? Normalize(string url, string baseUrl);
}