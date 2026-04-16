using System.Web;
using Zego.WebCrawler.Ports;

namespace Zego.WebCrawler.Domain;

public class UrlNormalizer : IUrlNormalizer
{
    private readonly HashSet<string> _trackingParameters =
    [
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "gclid", "fbclid", "mc_cid", "mc_eid"
    ];
    
    public string? Normalize(string url, string baseUrl)
    {
        try
        {
            var baseUri = new Uri(baseUrl);
            var resolvedUri = new Uri(baseUri, url);
            var normalizedUri = new UriBuilder(resolvedUri) { Fragment = "" }.Uri;
            
            // scheme filtering
            if (normalizedUri.Scheme != "http" && normalizedUri.Scheme != "https")
            {
                return null;
            }

            // auth stripping
            if (!string.IsNullOrEmpty(normalizedUri.UserInfo))
            {
                normalizedUri = new UriBuilder(normalizedUri) { UserName = "", Password = "" }.Uri;
            }
            
            // Sort query parameters and remove tracking parameters
            var queryParams = HttpUtility.ParseQueryString(normalizedUri.Query);
            var allKeys = queryParams.AllKeys ?? Array.Empty<string>();
            var sortedQuery = string
                .Join("&", allKeys
                                    .Except(_trackingParameters)
                                    .OrderBy(k => k)
                                    .Select(k => $"{k}={queryParams[k]}")
                );
            
            // Rebuild the URI with the sorted query
            normalizedUri = new UriBuilder(normalizedUri)
                {
                    Query = sortedQuery
                }.Uri;
            
            return normalizedUri.ToString();
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}