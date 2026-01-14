using Xunit;
using Q2Browser.Core.Protocol;

namespace Q2Browser.Core.Tests;

public class UrlValidatorTests
{
    [Fact]
    public void IsValidHttpUrl_ValidHttpUrl_ReturnsTrue()
    {
        // Arrange
        string validUrl = "http://example.com";
        
        // Act
        bool result = UrlValidator.IsValidHttpUrl(validUrl);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidHttpUrl_ValidHttpsUrl_ReturnsTrue()
    {
        bool result = UrlValidator.IsValidHttpUrl("https://example.com");
        Assert.True(result);
    }

    [Fact]
    public void IsValidHttpUrl_UrlWithPath_ReturnsTrue()
    {
        bool result = UrlValidator.IsValidHttpUrl("http://example.com/path?query=value");
        Assert.True(result);
    }

    [Fact]
    public void IsValidHttpUrl_UrlWithPort_ReturnsTrue()
    {
        bool result = UrlValidator.IsValidHttpUrl("http://example.com:8080");
        Assert.True(result);
    }

    [Fact]
    public void IsValidHttpUrl_InvalidUrl_ReturnsFalse()
    {
        bool result = UrlValidator.IsValidHttpUrl("not-a-url");
        Assert.False(result);
    }

    [Fact]
    public void IsValidHttpUrl_NullUrl_ReturnsFalse()
    {
        bool result = UrlValidator.IsValidHttpUrl(null);
        Assert.False(result);
    }

    [Fact]
    public void IsValidHttpUrl_EmptyString_ReturnsFalse()
    {
        bool result = UrlValidator.IsValidHttpUrl("");
        Assert.False(result);
    }

    [Fact]
    public void IsValidHttpUrl_WhitespaceOnly_ReturnsFalse()
    {
        bool result = UrlValidator.IsValidHttpUrl("   ");
        Assert.False(result);
    }

    [Fact]
    public void IsValidHttpUrl_FtpUrl_ReturnsFalse()
    {
        bool result = UrlValidator.IsValidHttpUrl("ftp://example.com");
        Assert.False(result);
    }

    [Fact]
    public void IsValidHttpUrl_FileUrl_ReturnsFalse()
    {
        bool result = UrlValidator.IsValidHttpUrl("file:///path/to/file");
        Assert.False(result);
    }

    [Fact]
    public void IsValidHttpUrl_RelativeUrl_ReturnsFalse()
    {
        bool result = UrlValidator.IsValidHttpUrl("/relative/path");
        Assert.False(result);
    }

    [Fact]
    public void IsValidHttpUrl_RealWorldHttpMasterServer_ReturnsTrue()
    {
        bool result = UrlValidator.IsValidHttpUrl("http://q2servers.com/?raw=2");
        Assert.True(result);
    }
}







