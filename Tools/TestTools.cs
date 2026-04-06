using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using DotNetMcp.Services;
using ModelContextProtocol.Server;

namespace DotNetMcp.Tools;

[McpServerToolType]
public static partial class TestTools
{
    [McpServerTool(Name = "dotnet_test"), Description("Run .NET tests with structured results")]
    public static async Task<string> Test(
        DotNetCliService cli,
        [Description("Path to test project or solution")] string? project_path = null,
        [Description("Build configuration (Debug or Release)")] string configuration = "Debug",
        [Description("Test filter expression (e.g., 'FullyQualifiedName~MyTest')")] string? filter = null,
        [Description("Skip building before running tests")] bool no_build = false)
    {
        var args = new StringBuilder("test");

        if (!string.IsNullOrEmpty(project_path))
            args.Append($" \"{project_path}\"");

        args.Append($" -c {configuration}");

        if (!string.IsNullOrEmpty(filter))
            args.Append($" --filter \"{filter}\"");

        if (no_build)
            args.Append(" --no-build");

        // Verbosity normal gives us the test result lines
        args.Append(" -v normal");

        var result = await cli.RunAsync(args.ToString());
        return FormatTestResult(result);
    }

    private static string FormatTestResult(CliResult result)
    {
        if (result.TimedOut)
            return "TESTS TIMED OUT\n\nPartial output:\n" + TruncateOutput(result.Stdout);

        var output = result.Stdout;
        var sb = new StringBuilder();

        // Extract the summary line (e.g., "Passed!  - Failed:     0, Passed:    42, Skipped:     0, Total:    42")
        var summaryMatch = SummaryPattern().Match(output);
        if (summaryMatch.Success)
        {
            sb.AppendLine(summaryMatch.Value.Trim());
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine(result.ExitCode == 0 ? "TESTS PASSED" : "TESTS FAILED");
            sb.AppendLine();
        }

        // Extract failed test details
        var failedTests = ParseFailedTests(output);
        if (failedTests.Count > 0)
        {
            sb.AppendLine($"Failed Tests ({failedTests.Count}):");
            foreach (var test in failedTests)
                sb.AppendLine($"  {test}");
            sb.AppendLine();
        }

        // If tests failed but we couldn't parse specifics, show raw output
        if (result.ExitCode != 0 && failedTests.Count == 0 && !summaryMatch.Success)
        {
            sb.AppendLine("Raw output:");
            sb.AppendLine(TruncateOutput(output));
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            var stderrTrimmed = result.Stderr.Trim();
            // Filter out non-error stderr noise (like "Determining projects to restore...")
            if (stderrTrimmed.Length > 0 && !stderrTrimmed.StartsWith("Determining projects"))
            {
                sb.AppendLine("Stderr:");
                sb.AppendLine(TruncateOutput(stderrTrimmed));
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static List<string> ParseFailedTests(string output)
    {
        var failed = new List<string>();
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (FailedTestPattern().IsMatch(trimmed))
            {
                failed.Add(trimmed);
            }
        }
        return failed;
    }

    [GeneratedRegex(@"(Failed|Passed!|Skipped).*Total:", RegexOptions.Singleline)]
    private static partial Regex SummaryPattern();

    [GeneratedRegex(@"^\s*Failed\s+")]
    private static partial Regex FailedTestPattern();

    private static string TruncateOutput(string output, int maxLength = 4000)
    {
        if (output.Length <= maxLength)
            return output;
        return output[..maxLength] + "\n... (truncated)";
    }
}
