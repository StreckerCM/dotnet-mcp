using DotNetMcp.Services;
using DotNetMcp.Tools;

namespace DotNetMcp.Tests;

public class FormatBuildResultTests
{
    [Fact]
    public void SuccessfulBuild_ReturnsBuildSucceeded()
    {
        var result = new CliResult(ExitCode: 0, Stdout: "Build succeeded.\n", Stderr: "", TimedOut: false);

        var output = BuildTools.FormatBuildResult(result);

        Assert.StartsWith("BUILD SUCCEEDED", output);
    }

    [Fact]
    public void FailedBuild_ReturnsBuildFailed()
    {
        var result = new CliResult(ExitCode: 1, Stdout: "Build FAILED.\n", Stderr: "", TimedOut: false);

        var output = BuildTools.FormatBuildResult(result);

        Assert.StartsWith("BUILD FAILED", output);
    }

    [Fact]
    public void TimedOut_ReturnsBuildTimedOut()
    {
        var result = new CliResult(ExitCode: -1, Stdout: "partial...", Stderr: "", TimedOut: true);

        var output = BuildTools.FormatBuildResult(result);

        Assert.StartsWith("BUILD TIMED OUT", output);
        Assert.Contains("partial...", output);
    }

    [Fact]
    public void BuildWithErrors_ParsesErrorLines()
    {
        var stdout = """
            Program.cs(10,5): error CS1002: ; expected
            Program.cs(15,1): error CS0246: The type or namespace name 'Foo' could not be found
            Build FAILED.
            """;
        var result = new CliResult(ExitCode: 1, Stdout: stdout, Stderr: "", TimedOut: false);

        var output = BuildTools.FormatBuildResult(result);

        Assert.Contains("Errors (2):", output);
        Assert.Contains("error CS1002", output);
        Assert.Contains("error CS0246", output);
    }

    [Fact]
    public void BuildWithWarnings_ParsesWarningLines()
    {
        var stdout = """
            Foo.cs(3,1): warning CS0168: The variable 'x' is declared but never used
            Build succeeded.
            """;
        var result = new CliResult(ExitCode: 0, Stdout: stdout, Stderr: "", TimedOut: false);

        var output = BuildTools.FormatBuildResult(result);

        Assert.Contains("BUILD SUCCEEDED", output);
        Assert.Contains("Warnings (1):", output);
        Assert.Contains("warning CS0168", output);
    }

    [Fact]
    public void BuildWithErrorsAndWarnings_ParsesBoth()
    {
        var stdout = """
            Foo.cs(3,1): warning CS0168: The variable 'x' is declared but never used
            Bar.cs(10,5): error CS1002: ; expected
            Build FAILED.
            """;
        var result = new CliResult(ExitCode: 1, Stdout: stdout, Stderr: "", TimedOut: false);

        var output = BuildTools.FormatBuildResult(result);

        Assert.Contains("Errors (1):", output);
        Assert.Contains("Warnings (1):", output);
    }

    [Fact]
    public void FailedBuildNoErrors_ShowsRawOutput()
    {
        var result = new CliResult(ExitCode: 1, Stdout: "Something unexpected happened", Stderr: "", TimedOut: false);

        var output = BuildTools.FormatBuildResult(result);

        Assert.Contains("Raw output:", output);
        Assert.Contains("Something unexpected happened", output);
    }

    [Fact]
    public void Stderr_IncludedInOutput()
    {
        var result = new CliResult(ExitCode: 0, Stdout: "Build succeeded.\n", Stderr: "some warning on stderr", TimedOut: false);

        var output = BuildTools.FormatBuildResult(result);

        Assert.Contains("Stderr:", output);
        Assert.Contains("some warning on stderr", output);
    }
}

public class FormatRestoreResultTests
{
    [Fact]
    public void SuccessfulRestore_ReturnsRestoreSucceeded()
    {
        var result = new CliResult(ExitCode: 0, Stdout: "Restore completed.\n", Stderr: "", TimedOut: false);

        var output = BuildTools.FormatRestoreResult(result);

        Assert.StartsWith("RESTORE SUCCEEDED", output);
    }

    [Fact]
    public void FailedRestore_ReturnsRestoreFailed()
    {
        var stdout = "NuGet.targets(123,5): error NU1101: Unable to find package 'FakePackage'\n";
        var result = new CliResult(ExitCode: 1, Stdout: stdout, Stderr: "", TimedOut: false);

        var output = BuildTools.FormatRestoreResult(result);

        Assert.StartsWith("RESTORE FAILED", output);
        Assert.Contains("Errors (1):", output);
        Assert.Contains("error NU1101", output);
    }

    [Fact]
    public void TimedOut_ReturnsRestoreTimedOut()
    {
        var result = new CliResult(ExitCode: -1, Stdout: "partial restore...", Stderr: "", TimedOut: true);

        var output = BuildTools.FormatRestoreResult(result);

        Assert.StartsWith("RESTORE TIMED OUT", output);
    }

    [Fact]
    public void FailedRestoreNoErrors_ShowsRawOutput()
    {
        var result = new CliResult(ExitCode: 1, Stdout: "Something broke", Stderr: "", TimedOut: false);

        var output = BuildTools.FormatRestoreResult(result);

        Assert.Contains("Raw output:", output);
    }
}

public class ParseDiagnosticsTests
{
    [Fact]
    public void ParsesErrors_IgnoresWarnings()
    {
        var output = """
            Foo.cs(3,1): warning CS0168: unused
            Bar.cs(10,5): error CS1002: ; expected
            """;

        var errors = BuildTools.ParseDiagnostics(output, "error");

        Assert.Single(errors);
        Assert.Contains("CS1002", errors[0]);
    }

    [Fact]
    public void ParsesWarnings_IgnoresErrors()
    {
        var output = """
            Foo.cs(3,1): warning CS0168: unused
            Bar.cs(10,5): error CS1002: ; expected
            """;

        var warnings = BuildTools.ParseDiagnostics(output, "warning");

        Assert.Single(warnings);
        Assert.Contains("CS0168", warnings[0]);
    }

    [Fact]
    public void EmptyOutput_ReturnsEmptyList()
    {
        Assert.Empty(BuildTools.ParseDiagnostics("", "error"));
    }

    [Fact]
    public void MsbuildErrorFormat_Parses()
    {
        var output = "MSBUILD : error MSB1050: Specify which project or solution file to use\n";
        var errors = BuildTools.ParseDiagnostics(output, "error");

        Assert.Single(errors);
        Assert.Contains("MSB1050", errors[0]);
    }

    [Fact]
    public void NuGetWarningFormat_Parses()
    {
        var output = "MyProject.csproj : warning NU1701: Package 'Foo 1.0' was restored using wrong framework\n";
        var warnings = BuildTools.ParseDiagnostics(output, "warning");

        Assert.Single(warnings);
    }
}
