using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;
using Zego.WebCrawler.Adapters;

namespace Zego.WebCrawler.Tests.Adapters;

[Collection("HttpPageFetcherTests")]
public class HttpPageFetcherTests
{
    private readonly Mock<ILogger<HttpPageFetcher>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly HttpPageFetcher _fetcher;

    public HttpPageFetcherTests()
    {
        _mockLogger = new Mock<ILogger<HttpPageFetcher>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _fetcher = new HttpPageFetcher(_httpClient);
    }

    [Fact]
    public async Task FetchAsync_Success_ReturnsHtml()
    {
        // Arrange
        const string url = "https://example.com";
        const string html = "<html><body>Test</body></html>";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(html)
            });

        // Act
        var result = await _fetcher.FetchAsync(url, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(html, result);
    }

    [Fact]
    public async Task FetchAsync_WithCancellationToken_CancelsRequest()
    {
        // Arrange
        const string url = "https://example.com";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("Cancelled"));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _fetcher.FetchAsync(url, cts.Token)
        );
    }

    [Fact]
    public async Task FetchAsync_EmptyResponse_ReturnsEmptyString()
    {
        // Arrange
        const string url = "https://example.com/empty";
        const string html = "";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(html)
            });

        // Act
        var result = await _fetcher.FetchAsync(url, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("", result);
    }

    [Fact]
    public async Task FetchAsync_Timeout_ThrowsTaskCanceledException()
    {
        // Arrange
        const string url = "https://example.com/slow";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _fetcher.FetchAsync(url, CancellationToken.None)
        );
    }

    [Fact]
    public async Task FetchAsync_WithDifferentUrls_SendsCorrectRequests()
    {
        // Arrange
        var urls = new[]
        {
            "https://example.com/page1",
            "https://example.com/page2",
            "https://example.com/page3"
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("<html></html>")
            });

        // Act
        foreach (var url in urls)
        {
            await _fetcher.FetchAsync(url, CancellationToken.None);
        }

        // Assert
        _mockHttpMessageHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Exactly(3),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
    }
}