using DotNetMcp.Services;
using DotNetMcp.Tools;

namespace DotNetMcp.Tests;

public class FormatTestResultTests
{
    [Fact]
    public void TestsPassed_WithSummaryLine()
    {
        var stdout = """
            Starting test execution, please wait...
            Passed!  - Failed:     0, Passed:    42, Skipped:     0, Total:    42
            """;
        var result = new CliResult(ExitCode: 0, Stdout: stdout, Stderr: "", TimedOut: false);

        var output = TestTools.FormatTestResult(result);

        Assert.Contains("Passed!", output);
        Assert.Contains("Total:", output);
    }

    [Fact]
    public void TestsPassed_NoSummaryLine()
    {
        var result = new CliResult(ExitCode: 0, Stdout: "all good\n", Stderr: "", TimedOut: false);

        var output = TestTools.FormatTestResult(result);

        Assert.Contains("TESTS PASSED", output);
    }

    [Fact]
    public void TestsFailed_WithFailedTestDetails()
    {
        var stdout = """
            Starting test execution, please wait...
            Failed MyNamespace.MyTests.TestMethod1
              Expected: 5
              Actual:   3
            Failed!  - Failed:     1, Passed:    41, Skipped:     0, Total:    42
            """;
        var result = new CliResult(ExitCode: 1, Stdout: stdout, Stderr: "", TimedOut: false);

        var output = TestTools.FormatTestResult(result);

        Assert.Contains("Failed Tests (1):", output);
        Assert.Contains("Failed MyNamespace.MyTests.TestMethod1", output);
    }

    [Fact]
    public void TestsFailed_NoSummaryNoDetails_ShowsRawOutput()
    {
        var result = new CliResult(ExitCode: 1, Stdout: "Something crashed", Stderr: "", TimedOut: false);

        var output = TestTools.FormatTestResult(result);

        Assert.Contains("TESTS FAILED", output);
        Assert.Contains("Raw output:", output);
        Assert.Contains("Something crashed", output);
    }

    [Fact]
    public void TimedOut_ReturnsTestsTimedOut()
    {
        var result = new CliResult(ExitCode: -1, Stdout: "partial...", Stderr: "", TimedOut: true);

        var output = TestTools.FormatTestResult(result);

        Assert.StartsWith("TESTS TIMED OUT", output);
    }

    [Fact]
    public void Stderr_FiltersDeterminingProjects()
    {
        var result = new CliResult(ExitCode: 0, Stdout: "test output\n", Stderr: "Determining projects to restore...\n", TimedOut: false);

        var output = TestTools.FormatTestResult(result);

        Assert.DoesNotContain("Stderr:", output);
        Assert.DoesNotContain("Determining projects", output);
    }

    [Fact]
    public void Stderr_RealErrors_Included()
    {
        var result = new CliResult(ExitCode: 1, Stdout: "test output\n", Stderr: "Unhandled exception in test host\n", TimedOut: false);

        var output = TestTools.FormatTestResult(result);

        Assert.Contains("Stderr:", output);
        Assert.Contains("Unhandled exception", output);
    }

    [Fact]
    public void MultipleFailedTests_AllCaptured()
    {
        var stdout = """
            Failed MyTests.Test1
            Failed MyTests.Test2
            Failed MyTests.Test3
            Failed!  - Failed:     3, Passed:    10, Skipped:     0, Total:    13
            """;
        var result = new CliResult(ExitCode: 1, Stdout: stdout, Stderr: "", TimedOut: false);

        var output = TestTools.FormatTestResult(result);

        Assert.Contains("Failed Tests (3):", output);
    }
}

public class ParseFailedTestsTests
{
    [Fact]
    public void ParsesFailedLines()
    {
        var output = """
            Passed MyTests.GoodTest
            Failed MyTests.BadTest
            Passed MyTests.AnotherGoodTest
            """;

        var failed = TestTools.ParseFailedTests(output);

        Assert.Single(failed);
        Assert.Contains("Failed MyTests.BadTest", failed[0]);
    }

    [Fact]
    public void NoFailedTests_ReturnsEmpty()
    {
        Assert.Empty(TestTools.ParseFailedTests("Passed MyTests.GoodTest\n"));
    }
}
