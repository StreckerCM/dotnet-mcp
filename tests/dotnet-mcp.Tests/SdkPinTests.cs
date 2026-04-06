using System.Text.Json.Nodes;
using DotNetMcp.Services;
using DotNetMcp.Tools;
using NSubstitute;

namespace DotNetMcp.Tests;

public class SdkPinSetTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();

    public SdkPinSetTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnet-mcp-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new CliResult(ExitCode: 0, Stdout: "8.0.419\n", Stderr: "", TimedOut: false));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Set_CreatesGlobalJson()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir, sdk_version: "8.0.419");

        Assert.Contains("SDK pinned to 8.0.419", result);
        Assert.True(File.Exists(Path.Combine(_tempDir, "global.json")));
    }

    [Fact]
    public async Task Set_WritesCorrectJson()
    {
        await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir, sdk_version: "8.0.419", roll_forward: "latestPatch");

        var content = File.ReadAllText(Path.Combine(_tempDir, "global.json"));
        var root = JsonNode.Parse(content);

        Assert.Equal("8.0.419", root?["sdk"]?["version"]?.GetValue<string>());
        Assert.Equal("latestPatch", root?["sdk"]?["rollForward"]?.GetValue<string>());
    }

    [Fact]
    public async Task Set_DefaultRollForward_IsLatestFeature()
    {
        await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir, sdk_version: "8.0.419");

        var content = File.ReadAllText(Path.Combine(_tempDir, "global.json"));
        var root = JsonNode.Parse(content);

        Assert.Equal("latestFeature", root?["sdk"]?["rollForward"]?.GetValue<string>());
    }

    [Fact]
    public async Task Set_PreservesExistingProperties()
    {
        var globalJsonPath = Path.Combine(_tempDir, "global.json");
        File.WriteAllText(globalJsonPath, """{"msbuild-sdks": {"Microsoft.Build.NoTargets": "3.7.0"}}""");

        await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir, sdk_version: "9.0.100");

        var content = File.ReadAllText(globalJsonPath);
        var root = JsonNode.Parse(content);

        Assert.Equal("9.0.100", root?["sdk"]?["version"]?.GetValue<string>());
        Assert.Equal("3.7.0", root?["msbuild-sdks"]?["Microsoft.Build.NoTargets"]?.GetValue<string>());
    }

    [Fact]
    public async Task Set_OverwritesExistingSdkSection()
    {
        var globalJsonPath = Path.Combine(_tempDir, "global.json");
        File.WriteAllText(globalJsonPath, """{"sdk": {"version": "7.0.100", "rollForward": "disable"}}""");

        await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir, sdk_version: "8.0.419");

        var content = File.ReadAllText(globalJsonPath);
        var root = JsonNode.Parse(content);

        Assert.Equal("8.0.419", root?["sdk"]?["version"]?.GetValue<string>());
        Assert.Equal("latestFeature", root?["sdk"]?["rollForward"]?.GetValue<string>());
    }

    [Fact]
    public async Task Set_VerifiesResolvedSdk()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir, sdk_version: "8.0.419");

        Assert.Contains("Resolved SDK: 8.0.419", result);
        await _cli.Received(1).RunAsync("--version", Arg.Is(_tempDir), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Set_MissingSdkVersion_ReturnsError()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir);

        Assert.Contains("ERROR", result);
        Assert.Contains("sdk_version is required", result);
    }

    [Fact]
    public async Task Set_InvalidRollForward_ReturnsError()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "set", directory: _tempDir, sdk_version: "8.0.419", roll_forward: "bogus");

        Assert.Contains("ERROR", result);
        Assert.Contains("Invalid rollForward", result);
    }

    [Fact]
    public async Task Set_NonExistentDirectory_ReturnsError()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "set", directory: @"C:\nonexistent-dir-xyz", sdk_version: "8.0.419");

        Assert.Contains("ERROR", result);
        Assert.Contains("Directory not found", result);
    }
}

public class SdkPinGetTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();

    public SdkPinGetTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnet-mcp-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Get_NoGlobalJson_ReportsDefault()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "get", directory: _tempDir);

        Assert.Contains("No global.json found", result);
        Assert.Contains("using default SDK", result);
    }

    [Fact]
    public async Task Get_WithSdkPin_ReportsVersionAndPolicy()
    {
        File.WriteAllText(Path.Combine(_tempDir, "global.json"),
            """{"sdk": {"version": "8.0.419", "rollForward": "latestPatch"}}""");

        var result = await SdkPinTools.SdkPin(_cli, action: "get", directory: _tempDir);

        Assert.Contains("Version: 8.0.419", result);
        Assert.Contains("Roll-forward: latestPatch", result);
    }

    [Fact]
    public async Task Get_GlobalJsonWithoutSdk_ReportsNoSdkSection()
    {
        File.WriteAllText(Path.Combine(_tempDir, "global.json"),
            """{"msbuild-sdks": {"Foo": "1.0"}}""");

        var result = await SdkPinTools.SdkPin(_cli, action: "get", directory: _tempDir);

        Assert.Contains("no sdk section", result);
    }

    [Fact]
    public async Task Get_MalformedJson_ReturnsError()
    {
        File.WriteAllText(Path.Combine(_tempDir, "global.json"), "not json {{{");

        var result = await SdkPinTools.SdkPin(_cli, action: "get", directory: _tempDir);

        Assert.Contains("ERROR", result);
        Assert.Contains("Failed to parse", result);
    }
}

public class SdkPinRemoveTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();

    public SdkPinRemoveTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnet-mcp-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Remove_NoGlobalJson_ReportsNothing()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "remove", directory: _tempDir);

        Assert.Contains("No global.json found", result);
        Assert.Contains("nothing to remove", result);
    }

    [Fact]
    public async Task Remove_SdkOnly_DeletesFile()
    {
        var path = Path.Combine(_tempDir, "global.json");
        File.WriteAllText(path, """{"sdk": {"version": "8.0.419"}}""");

        var result = await SdkPinTools.SdkPin(_cli, action: "remove", directory: _tempDir);

        Assert.Contains("Removed", result);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Remove_SdkPlusOtherProps_PreservesOtherProps()
    {
        var path = Path.Combine(_tempDir, "global.json");
        File.WriteAllText(path, """{"sdk": {"version": "8.0.419"}, "msbuild-sdks": {"Foo": "1.0"}}""");

        var result = await SdkPinTools.SdkPin(_cli, action: "remove", directory: _tempDir);

        Assert.Contains("Removed sdk section", result);
        Assert.Contains("preserved other properties", result);
        Assert.True(File.Exists(path));

        var content = File.ReadAllText(path);
        var root = JsonNode.Parse(content);
        Assert.Null(root?["sdk"]);
        Assert.Equal("1.0", root?["msbuild-sdks"]?["Foo"]?.GetValue<string>());
    }
}

public class SdkPinValidationTests
{
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();

    [Fact]
    public async Task InvalidAction_ReturnsError()
    {
        var result = await SdkPinTools.SdkPin(_cli, action: "bogus");

        Assert.Contains("ERROR", result);
        Assert.Contains("Invalid action", result);
    }
}
