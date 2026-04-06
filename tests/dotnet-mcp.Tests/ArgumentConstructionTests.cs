using DotNetMcp.Services;
using DotNetMcp.Tools;
using NSubstitute;

namespace DotNetMcp.Tests;

public class BuildArgumentTests
{
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();
    private readonly CliResult _successResult = new(ExitCode: 0, Stdout: "Build succeeded.\n", Stderr: "", TimedOut: false);

    public BuildArgumentTests()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_successResult);
    }

    [Fact]
    public async Task Build_DefaultArgs_UsesDebugConfig()
    {
        await BuildTools.Build(_cli);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("build") && a.Contains("-c Debug")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Build_WithProjectPath_IncludesQuotedPath()
    {
        await BuildTools.Build(_cli, project_path: @"C:\My Projects\App.sln");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("\"C:\\My Projects\\App.sln\"")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Build_ReleaseConfig()
    {
        await BuildTools.Build(_cli, configuration: "Release");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("-c Release")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Build_NoRestore_IncludesFlag()
    {
        await BuildTools.Build(_cli, no_restore: true);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("--no-restore")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Build_SanitizesPath_StripsQuotes()
    {
        await BuildTools.Build(_cli, project_path: "\"injected\"");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("\"injected\"") && !a.Contains("\"\"injected\"\"")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_DefaultArgs()
    {
        await BuildTools.Restore(_cli);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a == "restore"),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_WithProjectPath()
    {
        await BuildTools.Restore(_cli, project_path: @"C:\App.sln");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("restore") && a.Contains("\"C:\\App.sln\"")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }
}

public class TestArgumentTests
{
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();
    private readonly CliResult _successResult = new(ExitCode: 0, Stdout: "Passed!\n", Stderr: "", TimedOut: false);

    public TestArgumentTests()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_successResult);
    }

    [Fact]
    public async Task Test_DefaultArgs()
    {
        await TestTools.Test(_cli);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("test") && a.Contains("-c Debug") && a.Contains("-v normal")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Test_WithFilter()
    {
        await TestTools.Test(_cli, filter: "FullyQualifiedName~MyTest");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("--filter \"FullyQualifiedName~MyTest\"")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Test_NoBuild_IncludesFlag()
    {
        await TestTools.Test(_cli, no_build: true);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("--no-build")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }
}

public class PackageArgumentTests
{
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();
    private readonly CliResult _successResult = new(ExitCode: 0, Stdout: "Success\n", Stderr: "", TimedOut: false);

    public PackageArgumentTests()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_successResult);
    }

    [Fact]
    public async Task AddPackage_BasicArgs()
    {
        await PackageTools.AddPackage(_cli, project_path: @"C:\App.csproj", package_name: "Newtonsoft.Json");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("add") && a.Contains("\"C:\\App.csproj\"") && a.Contains("package Newtonsoft.Json")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPackage_WithVersion()
    {
        await PackageTools.AddPackage(_cli, project_path: @"C:\App.csproj", package_name: "Newtonsoft.Json", version: "13.0.3");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("--version 13.0.3")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPackage_Success_ReturnsAddedMessage()
    {
        var result = await PackageTools.AddPackage(_cli, project_path: @"C:\App.csproj", package_name: "Foo");

        Assert.Contains("Added Foo", result);
        Assert.Contains("App.csproj", result);
    }

    [Fact]
    public async Task AddPackage_Failure_ReturnsFailedMessage()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new CliResult(ExitCode: 1, Stdout: "", Stderr: "Package not found", TimedOut: false));

        var result = await PackageTools.AddPackage(_cli, project_path: @"C:\App.csproj", package_name: "FakePackage");

        Assert.Contains("FAILED", result);
        Assert.Contains("Package not found", result);
    }

    [Fact]
    public async Task RemovePackage_Args()
    {
        await PackageTools.RemovePackage(_cli, project_path: @"C:\App.csproj", package_name: "OldPackage");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("remove") && a.Contains("package OldPackage")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPackages_Default()
    {
        await PackageTools.ListPackages(_cli, project_path: @"C:\App.sln");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("list") && a.Contains("package") && !a.Contains("--outdated")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPackages_Outdated()
    {
        await PackageTools.ListPackages(_cli, project_path: @"C:\App.sln", outdated: true);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("--outdated")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NuGetPush_MasksApiKeyOnFailure()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new CliResult(ExitCode: 1, Stdout: "", Stderr: "403 Forbidden with key abc-secret-123", TimedOut: false));

        var result = await PackageTools.NuGetPush(_cli,
            package_path: @"C:\pkg.nupkg",
            source: "https://api.nuget.org/v3/index.json",
            api_key: "abc-secret-123");

        Assert.Contains("FAILED", result);
        Assert.Contains("***", result);
        Assert.DoesNotContain("abc-secret-123", result);
    }
}

public class ProjectToolArgumentTests
{
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();
    private readonly CliResult _successResult = new(ExitCode: 0, Stdout: "", Stderr: "", TimedOut: false);

    public ProjectToolArgumentTests()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(_successResult);
    }

    [Fact]
    public async Task Clean_DefaultArgs()
    {
        await ProjectTools.Clean(_cli);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("clean") && a.Contains("-c Debug")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Clean_Success_ReturnsCleanSucceeded()
    {
        var result = await ProjectTools.Clean(_cli);

        Assert.Equal("CLEAN SUCCEEDED", result);
    }

    [Fact]
    public async Task Run_WithProjectAndArgs()
    {
        await ProjectTools.Run(_cli, project_path: @"C:\App", args: "--verbose --port 8080");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("run") && a.Contains("--project") && a.Contains("-- --verbose --port 8080")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_TimedOut_ReportsTimeout()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new CliResult(ExitCode: -1, Stdout: "partial output", Stderr: "", TimedOut: true));

        var result = await ProjectTools.Run(_cli);

        Assert.Contains("RUN TIMED OUT", result);
    }

    [Fact]
    public async Task Format_VerifyNoChanges()
    {
        await ProjectTools.Format(_cli, verify_no_changes: true);

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("format") && a.Contains("--verify-no-changes")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Format_VerifyPassed_ReturnsCheckPassed()
    {
        var result = await ProjectTools.Format(_cli, verify_no_changes: true);

        Assert.Contains("FORMAT CHECK PASSED", result);
    }

    [Fact]
    public async Task Publish_WithRuntimeAndOutput()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new CliResult(ExitCode: 0, Stdout: "MyApp -> C:\\publish\\MyApp.dll\n", Stderr: "", TimedOut: false));

        await ProjectTools.Publish(_cli, runtime: "win-x64", output: @"C:\publish");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("publish") && a.Contains("-r win-x64") && a.Contains("-o \"C:\\publish\"") && a.Contains("-c Release")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }
}

public class InfoToolArgumentTests
{
    private readonly DotNetCliService _cli = Substitute.For<DotNetCliService>();

    public InfoToolArgumentTests()
    {
        _cli.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new CliResult(ExitCode: 0, Stdout: "10.0.201\n", Stderr: "", TimedOut: false));
    }

    [Fact]
    public async Task Info_CallsVersionSdksAndRuntimes()
    {
        await InfoTools.Info(_cli);

        // Should call --version, --list-sdks, and --list-runtimes
        await _cli.Received(1).RunAsync("--version", Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
        await _cli.Received(1).RunAsync("--list-sdks", Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
        await _cli.Received(1).RunAsync("--list-runtimes", Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SlnList_PassesSolutionPath()
    {
        await InfoTools.SlnList(_cli, solution_path: @"C:\App.sln");

        await _cli.Received(1).RunAsync(
            Arg.Is<string>(a => a.Contains("sln") && a.Contains("\"C:\\App.sln\"") && a.Contains("list")),
            Arg.Any<string?>(), Arg.Any<TimeSpan?>(),
            Arg.Any<Dictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }
}
