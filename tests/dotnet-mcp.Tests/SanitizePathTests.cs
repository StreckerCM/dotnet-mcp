using DotNetMcp.Services;

namespace DotNetMcp.Tests;

public class SanitizePathTests
{
    [Fact]
    public void NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DotNetCliService.SanitizePath(null));
    }

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DotNetCliService.SanitizePath(""));
    }

    [Fact]
    public void WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DotNetCliService.SanitizePath("   "));
    }

    [Fact]
    public void NormalPath_PassesThrough()
    {
        Assert.Equal(@"C:\Projects\MyApp\MyApp.sln", DotNetCliService.SanitizePath(@"C:\Projects\MyApp\MyApp.sln"));
    }

    [Fact]
    public void PathWithSpaces_PassesThrough()
    {
        Assert.Equal(@"C:\My Projects\My App.sln", DotNetCliService.SanitizePath(@"C:\My Projects\My App.sln"));
    }

    [Fact]
    public void DoubleQuotes_AreStripped()
    {
        Assert.Equal(@"C:\Projects\MyApp.sln", DotNetCliService.SanitizePath("\"C:\\Projects\\MyApp.sln\""));
    }

    [Fact]
    public void NullBytes_AreStripped()
    {
        Assert.Equal(@"C:\Projects\MyApp.sln", DotNetCliService.SanitizePath("C:\\Projects\\\0MyApp.sln"));
    }

    [Fact]
    public void EmbeddedQuotesAndNullBytes_BothStripped()
    {
        Assert.Equal("pathvalue", DotNetCliService.SanitizePath("path\"\0value"));
    }

    [Fact]
    public void ForwardSlashPath_PassesThrough()
    {
        Assert.Equal("/home/user/project", DotNetCliService.SanitizePath("/home/user/project"));
    }
}
