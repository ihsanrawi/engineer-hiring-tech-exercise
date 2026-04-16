using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Zego.WebCrawler.Domain;
using Zego.WebCrawler.Ports;

namespace Zego.WebCrawler.Tests.Domains;

[Collection("CrawlerEngineTests")]
public class CrawlerEngineTests
{
    private readonly CrawlerEngine _crawlerEngine;
    private readonly Mock<IPageFetcher> _mockPageFetcher;
    private readonly Mock<ILinkExtractor> _mockLinkExtractor;
    private readonly Mock<IUrlNormalizer> _mockUrlNormalizer;
    private readonly Mock<IRobotsTxtChecker> _mockRobotsTxtChecker;

    public CrawlerEngineTests()
    {
        var mockLogger = new Mock<ILogger<CrawlerEngine>>();
        _mockPageFetcher = new Mock<IPageFetcher>();
        _mockLinkExtractor = new Mock<ILinkExtractor>();
        _mockUrlNormalizer = new Mock<IUrlNormalizer>();
        _mockRobotsTxtChecker = new Mock<IRobotsTxtChecker>();

        _mockRobotsTxtChecker
            .Setup(r => r.InitializeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockRobotsTxtChecker
            .Setup(r => r.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _crawlerEngine = new CrawlerEngine(mockLogger.Object, _mockPageFetcher.Object, _mockLinkExtractor.Object, _mockUrlNormalizer.Object, _mockRobotsTxtChecker.Object);
    }
    
    [Theory]
    [InlineData("https://example.com", false)]
    [InlineData("http://example.com", false)]
    [InlineData("ftp://example.com", true)]
    public async Task CrawlAsync_SchemeValidation_Works(string url, bool shouldThrow)
    {
        if (shouldThrow)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _crawlerEngine.CrawlAsync(url, respectRobots: true, 5, TestContext.Current.CancellationToken));
        }
        else
        {
            _mockPageFetcher
                .Setup(pf => pf.FetchAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync("<html></html>");

            _mockLinkExtractor
                .Setup(le => le.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            var result = await _crawlerEngine.CrawlAsync(url, respectRobots: true, 5, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task CrawlAsync_SinglePage_ReturnsLinks()
    {
        const string startUrl = "http://example.com";
        const string html = "<html><body><a href='http://example.com/page1'>Page 1</a></body></html>";
        List<string> rawLinks = ["/page1"];
        string normalizedLink = "http://example.com/page1";
        
        // Setup mocks
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(startUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(html);
        
        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawLinks);
        
        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page1", startUrl))
            .Returns(normalizedLink);
    
        // Mock page1 to return empty HTML (so crawler stops)
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(normalizedLink, It.IsAny<CancellationToken>()))
            .ReturnsAsync("");
        
        _mockLinkExtractor
            .Setup(le => le.ExtractAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        
        // Act
        var result = await _crawlerEngine.CrawlAsync(startUrl, respectRobots: true, 5, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(startUrl, result.StartUrl);
        Assert.Equal(2, result.VisitedUrls.Count);  // Both URLs visited
        Assert.Contains(startUrl, result.VisitedUrls);
        Assert.Contains(normalizedLink, result.VisitedUrls);

        Assert.Single(result.LinksByPage);  // Only startUrl has links (page1 has none)
        Assert.True(result.LinksByPage.ContainsKey(startUrl));
        Assert.Contains("/page1", result.LinksByPage[startUrl]);

    }

    [Fact]
    public async Task CrawlAsync_MultiplePages_CrawlsInParallel()
    {
        // Test: Multiple pages are discovered and processed
        const string startUrl = "http://example.com";
        const string page1Url = "http://example.com/page1";
        const string page2Url = "http://example.com/page2";
        const string page3Url = "http://example.com/page3";

        // Start page has links to 3 pages
        const string startHtml = @"
            <html>
                <body>
                    <a href='/page1'>Page 1</a>
                    <a href='/page2'>Page 2</a>
                    <a href='/page3'>Page 3</a>
                </body>
            </html>";

        var startLinks = new List<string> { "/page1", "/page2", "/page3" };

        // Each page has different content
        const string page1Html = "<html><body><h1>Page 1</h1></body></html>";
        const string page2Html = "<html><body><h1>Page 2</h1></body></html>";
        const string page3Html = "<html><body><h1>Page 3</h1></body></html>";

        var emptyLinks = new List<string>();

        // Setup mocks for start page
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(startUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startHtml);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(startHtml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startLinks);

        // Setup normalizer for all links
        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page1", startUrl))
            .Returns(page1Url);

        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page2", startUrl))
            .Returns(page2Url);

        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page3", startUrl))
            .Returns(page3Url);

        // Setup mocks for all 3 pages
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(page1Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page1Html);

        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(page2Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page2Html);

        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(page3Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page3Html);

        // All pages return no links
        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(page1Html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyLinks);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(page2Html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyLinks);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(page3Html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyLinks);

        // Act
        var result = await _crawlerEngine.CrawlAsync(startUrl, respectRobots: true, 5, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.VisitedUrls.Count);  // startUrl + 3 pages

        // Verify all pages are visited
        Assert.Contains(startUrl, result.VisitedUrls);
        Assert.Contains(page1Url, result.VisitedUrls);
        Assert.Contains(page2Url, result.VisitedUrls);
        Assert.Contains(page3Url, result.VisitedUrls);

        // Verify start page has all 3 links
        Assert.True(result.LinksByPage.ContainsKey(startUrl));
        Assert.Equal(3, result.LinksByPage[startUrl].Count);
        Assert.Contains("/page1", result.LinksByPage[startUrl]);
        Assert.Contains("/page2", result.LinksByPage[startUrl]);
        Assert.Contains("/page3", result.LinksByPage[startUrl]);

        // Verify other pages are NOT in LinksByPage (they have no links)
        Assert.False(result.LinksByPage.ContainsKey(page1Url));
        Assert.False(result.LinksByPage.ContainsKey(page2Url));
        Assert.False(result.LinksByPage.ContainsKey(page3Url));
    }

    [Fact]
    public async Task CrawlAsync_DuplicateLinks_DoesNotRevisit()
    {
        // Test: Same link on multiple pages = visited once
        const string startUrl = "http://example.com";
        const string page1Url = "http://example.com/page1";
        const string page2Url = "http://example.com/page2";
        const string contactUrl = "http://example.com/contact";

        // Start page has links to page1 and page2
        const string startHtml = @"
            <html>
                <body>
                    <a href='/page1'>Page 1</a>
                    <a href='/page2'>Page 2</a>
                </body>
            </html>";

        var startLinks = new List<string> { "/page1", "/page2" };

        // Both page1 and page2 have the same link to /contact
        const string page1Html = @"
            <html>
                <body>
                    <a href='/contact'>Contact Us</a>
                </body>
            </html>";

        const string page2Html = @"
            <html>
                <body>
                    <a href='/contact'>Contact Us</a>
                </body>
            </html>";

        var contactLinks = new List<string> { "/contact" };

        // Setup mocks for start page
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(startUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startHtml);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(startHtml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startLinks);

        // Setup normalizer for start page links
        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page1", startUrl))
            .Returns(page1Url);

        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page2", startUrl))
            .Returns(page2Url);

        // Setup mocks for page1
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(page1Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page1Html);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(page1Html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contactLinks);

        _mockUrlNormalizer
            .Setup(un => un.Normalize("/contact", page1Url))
            .Returns(contactUrl);

        // Setup mocks for page2
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(page2Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(page2Html);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(page2Html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contactLinks);

        _mockUrlNormalizer
            .Setup(un => un.Normalize("/contact", page2Url))
            .Returns(contactUrl);

        // Mock contact page to return empty HTML (stops crawling)
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(contactUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _crawlerEngine.CrawlAsync(startUrl, respectRobots: true, 5, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.VisitedUrls.Count);  // startUrl, page1, page2, contact

        // Verify all URLs are visited
        Assert.Contains(startUrl, result.VisitedUrls);
        Assert.Contains(page1Url, result.VisitedUrls);
        Assert.Contains(page2Url, result.VisitedUrls);
        Assert.Contains(contactUrl, result.VisitedUrls);

        // Verify contact URL appears only once in visited URLs (no duplicates!)
        Assert.Single(result.VisitedUrls.Where(u => u == contactUrl));

        // Verify both page1 and page2 have the contact link
        Assert.True(result.LinksByPage.ContainsKey(page1Url));
        Assert.True(result.LinksByPage.ContainsKey(page2Url));
        Assert.Contains("/contact", result.LinksByPage[page1Url]);
        Assert.Contains("/contact", result.LinksByPage[page2Url]);
    }

    [Fact]
    public async Task CrawlAsync_DifferentDomain_Skips()
    {
        // Test: Link to different domain is ignored
        const string startUrl = "http://example.com";
        const string html = @"
            <html>
                <body>
                    <a href='http://example.com/page1'>Same Domain</a>
                    <a href='http://other.com/external'>Different Domain</a>
                    <a href='http://another.org/page'>Another Domain</a>
                </body>
            </html>";

        var rawLinks = new List<string> { "/page1", "http://other.com/external", "http://another.org/page" };

        // Setup mocks for start page
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(startUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(html);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawLinks);

        // Mock normalizer to return appropriate URLs
        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page1", startUrl))
            .Returns("http://example.com/page1");

        _mockUrlNormalizer
            .Setup(un => un.Normalize("http://other.com/external", startUrl))
            .Returns("http://other.com/external");

        _mockUrlNormalizer
            .Setup(un => un.Normalize("http://another.org/page", startUrl))
            .Returns("http://another.org/page");

        // Mock same-domain page to return empty HTML
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync("http://example.com/page1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _crawlerEngine.CrawlAsync(startUrl, respectRobots: true, 5, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.VisitedUrls.Count);  // Only startUrl and page1

        // Verify same-domain URLs are visited
        Assert.Contains(startUrl, result.VisitedUrls);
        Assert.Contains("http://example.com/page1", result.VisitedUrls);

        // Verify different-domain URLs are NOT visited
        Assert.DoesNotContain("http://other.com/external", result.VisitedUrls);
        Assert.DoesNotContain("http://another.org/page", result.VisitedUrls);

        // Verify only same-domain links are stored
        Assert.Single(result.LinksByPage);
        Assert.True(result.LinksByPage.ContainsKey(startUrl));
        Assert.Contains("/page1", result.LinksByPage[startUrl]);
    }
    
    [Fact]
    public async Task CrawlAsync_WithRespectRobotsFalse_DoesNotCheckRobotsTxt()
    {
        // Arrange
        const string startUrl = "http://example.com";
        const string adminUrl = "http://example.com/admin";

        // Setup: Mock robots.txt to block /admin
        _mockRobotsTxtChecker
            .Setup(r => r.IsAllowedAsync(adminUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);  // Blocked by robots.txt

        // Setup: Mock start page with link to /admin
        const string startHtml = @"
            <html>
                <body>
                    <a href='/admin'>Admin Panel</a>
                </body>
            </html>";

        var adminLinks = new List<string> { "/admin" };

        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(startUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startHtml);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(startHtml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminLinks);

        _mockUrlNormalizer
            .Setup(un => un.Normalize("/admin", startUrl))
            .Returns(adminUrl);

        // Mock admin page to return empty HTML (stops crawling)
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(adminUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Act: Crawl with respectRobots = false (robots.txt should be ignored)
        var result = await _crawlerEngine.CrawlAsync(startUrl, respectRobots: false, 5, TestContext.Current.CancellationToken);

        // Assert: /admin should be visited even though robots.txt blocks it
        Assert.NotNull(result);
        Assert.Equal(2, result.VisitedUrls.Count);  // Both startUrl and adminUrl visited
        Assert.Contains(startUrl, result.VisitedUrls);
        Assert.Contains(adminUrl, result.VisitedUrls);  // ✅ Admin URL visited despite robots.txt block

        // Verify IsAllowedAsync was never called when respectRobots = false
        _mockRobotsTxtChecker.Verify(
            r => r.IsAllowedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CrawlAsync_WithCancellation_StopsGracefully()
    {
        // Arrange
        const string startUrl = "http://example.com";
        const string page1Url = "http://example.com/page1";

        const string startHtml = @"
            <html>
                <body>
                    <a href='/page1'>Page 1</a>
                </body>
            </html>";

        var startLinks = new List<string> { "/page1" };

        // Setup start page
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(startUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startHtml);

        _mockLinkExtractor
            .Setup(le => le.ExtractAsync(startHtml, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startLinks);

        _mockUrlNormalizer
            .Setup(un => un.Normalize("/page1", startUrl))
            .Returns(page1Url);

        // Setup page1 to throw cancellation exception
        _mockPageFetcher
            .Setup(pf => pf.FetchAsync(page1Url, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Cancelled"));

        // Act
        var result = await _crawlerEngine.CrawlAsync(startUrl, respectRobots: true, 5, TestContext.Current.CancellationToken);

        // Assert - Should still complete with startUrl
        Assert.NotNull(result);
        Assert.True(result.VisitedUrls.Count >= 1); // At least startUrl visited
        Assert.Contains(startUrl, result.VisitedUrls);
    }
}