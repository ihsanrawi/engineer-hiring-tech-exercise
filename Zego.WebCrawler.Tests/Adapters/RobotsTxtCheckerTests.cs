using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Zego.WebCrawler.Adapters;

namespace Zego.WebCrawler.Tests.Adapters;

public class RobotsTxtCheckerTests
{
    private readonly Mock<ILogger<RobotsTxtChecker>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly FakeHttpClientFactory _httpClientFactory;
    private readonly RobotsTxtChecker _checker;

    public RobotsTxtCheckerTests()
    {
        _mockLogger = new Mock<ILogger<RobotsTxtChecker>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _httpClientFactory = new FakeHttpClientFactory(_httpClient);
        _checker = new RobotsTxtChecker(_mockLogger.Object, _httpClientFactory);
    }

    private class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public FakeHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name)
        {
            return _httpClient;
        }

        public HttpClient CreateClient()
        {
            return _httpClient;
        }
    }

    [Fact]
    public async Task InitializeAsync_WithValidRobotsTxt_ParsesCorrectly()
    {
        // Arrange
        const string robotsTxt = """
            User-agent: *
            Disallow: /admin
            Disallow: /private
            Allow: /public
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        var result1 = await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None);
        var result2 = await _checker.IsAllowedAsync("https://example.com/private", CancellationToken.None);
        var result3 = await _checker.IsAllowedAsync("https://example.com/public", CancellationToken.None);
        var result4 = await _checker.IsAllowedAsync("https://example.com/other", CancellationToken.None);

        Assert.False(result1);  // /admin is disallowed
        Assert.False(result2);  // /private is disallowed
        Assert.True(result3);   // /public is allowed
        Assert.True(result4);   // /other is allowed (default)
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyDisallow_AllowsAll()
    {
        // Arrange
        const string robotsTxt = """
            User-agent: *
            Disallow: /admin
            Disallow:
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        var result = await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None);
        Assert.True(result);  // Empty disallow cleared all rules
    }

    [Fact]
    public async Task InitializeAsync_WithDisallowRoot_BlocksAll()
    {
        // Arrange
        const string robotsTxt = """
            User-agent: *
            Disallow: /
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        var result1 = await _checker.IsAllowedAsync("https://example.com/", CancellationToken.None);
        var result2 = await _checker.IsAllowedAsync("https://example.com/page", CancellationToken.None);
        var result3 = await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None);

        Assert.False(result1);  // Root is disallowed
        Assert.False(result2);  // All paths are disallowed
        Assert.False(result3);  // All paths are disallowed
    }

    [Fact]
    public async Task InitializeAsync_MissingRobotsTxt_AllowsAll()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("404 Not Found"));

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        var result = await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None);
        Assert.True(result);  // No robots.txt means allow all
    }

    [Fact]
    public async Task InitializeAsync_WithComments_IgnoresComments()
    {
        // Arrange
        const string robotsTxt = """
            # This is a comment
            User-agent: *
            Disallow: /admin
            # Another comment
            Allow: /admin/users
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        var result1 = await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None);
        var result2 = await _checker.IsAllowedAsync("https://example.com/admin/users", CancellationToken.None);

        Assert.False(result1);  // /admin is disallowed
        Assert.True(result2);   // /admin/users is allowed (overrides disallow)
    }

    [Fact]
    public async Task IsAllowedAsync_PathMatching_WorksCorrectly()
    {
        // Arrange
        const string robotsTxt = """
            User-agent: *
            Disallow: /admin
            Disallow: /private
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Act & Assert
        // Exact path match
        Assert.False(await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None));
        Assert.False(await _checker.IsAllowedAsync("https://example.com/private", CancellationToken.None));

        // Subpaths are also blocked
        Assert.False(await _checker.IsAllowedAsync("https://example.com/admin/users", CancellationToken.None));
        Assert.False(await _checker.IsAllowedAsync("https://example.com/admin/users/edit", CancellationToken.None));
        Assert.False(await _checker.IsAllowedAsync("https://example.com/private/data", CancellationToken.None));

        // Different paths are allowed
        Assert.True(await _checker.IsAllowedAsync("https://example.com/public", CancellationToken.None));
        Assert.True(await _checker.IsAllowedAsync("https://example.com/about", CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_AllowOverridesDisallow_WorksCorrectly()
    {
        // Arrange
        const string robotsTxt = """
            User-agent: *
            Disallow: /admin
            Allow: /admin/users
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Act & Assert
        Assert.False(await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None));
        Assert.False(await _checker.IsAllowedAsync("https://example.com/admin/settings", CancellationToken.None));
        Assert.True(await _checker.IsAllowedAsync("https://example.com/admin/users", CancellationToken.None));
        Assert.True(await _checker.IsAllowedAsync("https://example.com/admin/users/list", CancellationToken.None));
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyLines_IgnoresEmptyLines()
    {
        // Arrange
        const string robotsTxt = """
            User-agent: *

            Disallow: /admin

            Allow: /public

            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        Assert.False(await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None));
        Assert.True(await _checker.IsAllowedAsync("https://example.com/public", CancellationToken.None));
    }

    [Fact]
    public async Task InitializeAsync_CaseInsensitiveKeys_WorksCorrectly()
    {
        // Arrange
        const string robotsTxt = """
            USER-AGENT: *
            DISALLOW: /admin
            allow: /public
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        Assert.False(await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None));
        Assert.True(await _checker.IsAllowedAsync("https://example.com/public", CancellationToken.None));
    }

    [Fact]
    public async Task InitializeAsync_NonMatchingUserAgent_IgnoresRules()
    {
        // Arrange
        const string robotsTxt = """
            User-agent: Googlebot
            Disallow: /admin

            User-agent: *
            Disallow: /private
            """;

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(robotsTxt)
            });

        // Act
        await _checker.InitializeAsync("https://example.com", CancellationToken.None);

        // Assert
        Assert.True(await _checker.IsAllowedAsync("https://example.com/admin", CancellationToken.None));
        Assert.False(await _checker.IsAllowedAsync("https://example.com/private", CancellationToken.None));
    }
}