using Xunit;
using Zego.WebCrawler.Domain;

namespace Zego.WebCrawler.Tests.Domains;

[Collection("UrlNormalizerTests")]
public class UrlNormalizerTests
{
    [Fact]
    public void Normalize_AbsoluteUrl_ReturnsSameUrl()
    {
        // Arrange
        var normalizer = new UrlNormalizer();
        var url = "https://example.com/page";
        var baseUrl = "https://example.com";

        // Act
        var result = normalizer.Normalize(url, baseUrl);

        // Assert
        Assert.Equal(url, result);
    }
    
    [Theory]
    [InlineData("/about", "https://example.com/about")]
    [InlineData("../contact", "https://example.com/contact")]
    public void Normalize_RelativeUrl_ResolvesToAbsolute(string relative, string expected)
    {
        // Arrange
        var normalizer = new UrlNormalizer();
        var baseUrl = "https://example.com/";

        // Act
        var result = normalizer.Normalize(relative, baseUrl);

        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("https://example.com/page#section", "https://example.com/page")]
    public void Normalize_WithFragment_RemovesFragment(string url, string expected)
    {
        // Arrange
        var normalizer = new UrlNormalizer();
        var baseUrl = "https://example.com";
        
        // Act
        var result = normalizer.Normalize(url, baseUrl);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("https://example.com/page?a=2&b=1", "https://example.com/page?a=2&b=1")]  // Sorted
    [InlineData("https://example.com/page?b=1&a=2", "https://example.com/page?a=2&b=1")]  // Unsorted → Sorted
    public void Normalize_QueryParams_SortsAlphabetically(string url, string expected)
    {
        // Arrange
        var normalizer = new UrlNormalizer();
        var baseUrl = "https://example.com";
        
        // Act
        var result = normalizer.Normalize(url, baseUrl);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("https://example.com/page?utm_source=google&id=5", "https://example.com/page?id=5")]
    [InlineData("https://example.com/page?fbclid=abc123", "https://example.com/page")]
    public void Normalize_WithTrackingParams_RemovesThem(string url, string expected)
    {
        // Arrange
        var normalizer = new UrlNormalizer();
        var baseUrl = "https://example.com";
        
        // Act
        var result = normalizer.Normalize(url, baseUrl);
        
        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_WithCredentials_StripsCredentials()
    {
        // Arrange
        var url = "https://user:pass@example.com/admin";
        var expected = "https://example.com/admin";

        var normalizer = new UrlNormalizer();
        var baseUrl = "https://example.com";

        // Act
        var result = normalizer.Normalize(url, baseUrl);

        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("mailto:test@example.com")]
    [InlineData("tel:+1234567890")]
    [InlineData("javascript:void(0)")]
    public void Normalize_InvalidScheme_ReturnsNull(string url)
    {
        var normalizer = new UrlNormalizer();
        Assert.Null(normalizer.Normalize(url, "https://example.com"));
    }
    
    [Fact]
    public void Normalize_ComplexNormalization_HandlesAllTransformations()
    {
        var url = "https://user:pass@example.com/page?utm_source=google&z=1&a=2#section";
        var expected = "https://example.com/page?a=2&z=1";

        var normalizer = new UrlNormalizer();
        Assert.Equal(expected, normalizer.Normalize(url, "https://example.com"));
    }

}