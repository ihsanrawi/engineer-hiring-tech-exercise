using Xunit;
using Zego.WebCrawler.Adapters;

namespace Zego.WebCrawler.Tests.Adapters;

[Collection("HtmlLinkExtractorTests")]
public class HtmlLinkExtractorTests
{
    [Theory]
    [InlineData("<a href='https://example.com/page1'>Page 1</a>", "https://example.com/page1")]
    [InlineData("<a href='/about'>About</a>", "/about")]
    [InlineData("<a href='../contact'>Contact</a>", "../contact")]
    public void Extract_ReturnsAllHrefs(string href, string expectedHref)
    {
        // Arrange
        var html = $@"
                    <html>
                        <body>
                            {href}
                        </body>
                    </html>";
        var extractor = new HtmlLinkExtractor();

        // Act
        var result = extractor.ExtractAsync(html).Result;

        // Assert
        Assert.Contains(expectedHref, result);
    }
    
    [Theory]
    [InlineData("<html><body></body></html>")]
    [InlineData("<html><body><p>No links here</p></body></html>")]
    public void Extract_ReturnsEmptyListWhenNoAnchorTags(string html)
    {
        var extractor = new HtmlLinkExtractor();

        // Act
        var result = extractor.ExtractAsync(html).Result;

        // Assert
        Assert.Empty(result);
    }
    
    [Fact]
    public void Extract_ExcludesAnchorsWithoutHref()
    {
        // Arrange
        var html = @"
                    <html>
                        <body>
                            <a name='top'>Top</a>
                            <a href='https://example.com'>Example</a>
                        </body>
                    </html>";
        var extractor = new HtmlLinkExtractor();
        
        // Act
        var result = extractor.ExtractAsync(html).Result;

        // Assert
        Assert.DoesNotContain("Top", result);
        Assert.Single(result);
        Assert.Contains("https://example.com", result);
    }

    [Fact]
    public void Extract_HandlesEmptyHref()
    {
        // Arrange
        var html = @"
                    <html>
                        <body>
                            <a href=''>Empty Link</a>
                        </body>
                    </html>";
        var extractor = new HtmlLinkExtractor();
        
        // Act
        var result = extractor.ExtractAsync(html).Result;

        // Assert
        Assert.Contains("", result);
    }
    
    [Fact]
    public  void Extract_HandlesAnchorsWithHref()
    {
        // Arrange
        var html = @"
                    <html>
                        <body>
                            <a href='#section'>Section</a>
                        </body>
                    </html>";
        var extractor = new HtmlLinkExtractor();
        
        // Act
        var result = extractor.ExtractAsync(html).Result;

        // Assert
        Assert.Contains("#section", result);
    }
    
    [Theory]
    [InlineData("<a href='mailto:test@example.com'>Email</a>", "mailto:test@example.com")]
    [InlineData("<a href='tel:+1234567890'>Call</a>", "tel:+1234567890")]
    public void Extract_ReturnsNonHttpHrefs(string html, string expectedHref)
    {
        var extractor = new HtmlLinkExtractor();

        // Act
        var result = extractor.ExtractAsync(html).Result;

        // Assert
        Assert.Contains(expectedHref, result);
    }
    
    [Fact]
    public void Extract_ReturnsDuplicateHrefs()
    {
        // Arrange
        var html = @"
                    <html>
                        <body>
                            <a href='https://example.com'>Example</a>
                            <a href='https://example.com'>Example Duplicate</a>
                        </body>
                    </html>";
        var extractor = new HtmlLinkExtractor();
        
        // Act
        var result = extractor.ExtractAsync(html).Result;

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, href => Assert.Equal("https://example.com", href));
    }
}