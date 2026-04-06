using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using DotNetMcp.Services;
using ModelContextProtocol.Server;

namespace DotNetMcp.Tools;

[McpServerToolType]
public static partial class BuildTools
{
    [McpServerTool(Name = "dotnet_restore"), Description("Restore NuGet packages for a solution or project")]
    public static async Task<string> Restore(
        DotNetCliService cli,
        [Description("Path to solution or project file, or directory containing one")] string? project_path = null)
    {
        var args = new StringBuilder("restore");

        project_path = DotNetCliService.SanitizePath(project_path);
        if (!string.IsNullOrEmpty(project_path))
            args.Append($" \"{project_path}\"");

        var result = await cli.RunAsync(args.ToString());
        return FormatRestoreResult(result);
    }

    [McpServerTool(Name = "dotnet_build"), Description("Build a .NET solution or project")]
    public static async Task<string> Build(
        DotNetCliService cli,
        [Description("Path to solution or project file, or directory containing one")] string? project_path = null,
        [Description("Build configuration (Debug or Release)")] string configuration = "Debug",
        [Description("Skip NuGet restore before building")] bool no_restore = false)
    {
        var args = new StringBuilder("build");

        project_path = DotNetCliService.SanitizePath(project_path);
        if (!string.IsNullOrEmpty(project_path))
            args.Append($" \"{project_path}\"");

        args.Append($" -c {configuration}");

        if (no_restore)
            args.Append(" --no-restore");

        // Use -consoleloggerparameters for cleaner output
        args.Append(" -clp:NoSummary");

        var result = await cli.RunAsync(args.ToString());
        return FormatBuildResult(result);
    }

    internal static string FormatRestoreResult(CliResult result)
    {
        if (result.TimedOut)
            return "RESTORE TIMED OUT\n\nPartial output:\n" + TruncateOutput(result.Stdout);

        var sb = new StringBuilder();
        sb.AppendLine(result.ExitCode == 0 ? "RESTORE SUCCEEDED" : "RESTORE FAILED");
        sb.AppendLine();

        if (result.ExitCode != 0)
        {
            // Parse NuGet-specific errors (e.g., NU1101, NU1301)
            var errors = ParseDiagnostics(result.Stdout, "error");
            if (errors.Count > 0)
            {
                sb.AppendLine($"Errors ({errors.Count}):");
                foreach (var e in errors)
                    sb.AppendLine($"  {e}");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("Raw output:");
                sb.AppendLine(TruncateOutput(result.Stdout));
            }
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            sb.AppendLine("Stderr:");
            sb.AppendLine(TruncateOutput(result.Stderr));
        }

        return sb.ToString().TrimEnd();
    }

    internal static string FormatBuildResult(CliResult result)
    {
        if (result.TimedOut)
            return "BUILD TIMED OUT\n\nPartial output:\n" + TruncateOutput(result.Stdout);

        var output = result.Stdout;
        var errors = ParseDiagnostics(output, "error");
        var warnings = ParseDiagnostics(output, "warning");

        var sb = new StringBuilder();
        sb.AppendLine(result.ExitCode == 0 ? "BUILD SUCCEEDED" : "BUILD FAILED");
        sb.AppendLine();

        if (errors.Count > 0)
        {
            sb.AppendLine($"Errors ({errors.Count}):");
            foreach (var e in errors)
                sb.AppendLine($"  {e}");
            sb.AppendLine();
        }

        if (warnings.Count > 0)
        {
            sb.AppendLine($"Warnings ({warnings.Count}):");
            foreach (var w in warnings)
                sb.AppendLine($"  {w}");
            sb.AppendLine();
        }

        if (result.ExitCode != 0 && errors.Count == 0)
        {
            // No parsed errors but build failed — include raw output for debugging
            sb.AppendLine("Raw output:");
            sb.AppendLine(TruncateOutput(output));
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            sb.AppendLine("Stderr:");
            sb.AppendLine(TruncateOutput(result.Stderr));
        }

        return sb.ToString().TrimEnd();
    }

    internal static List<string> ParseDiagnostics(string output, string level)
    {
        var diagnostics = new List<string>();
        foreach (var line in output.Split('\n'))
        {
            // Match MSBuild diagnostic format: path(line,col): error/warning CODE: message
            if (DiagnosticPattern().Match(line) is { Success: true } m
                && m.Groups["level"].Value.Equals(level, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(line.Trim());
            }
        }
        return diagnostics;
    }

    [GeneratedRegex(@": (?<level>error|warning) \w+:")]
    private static partial Regex DiagnosticPattern();

    private static string TruncateOutput(string output, int maxLength = 4000)
    {
        if (output.Length <= maxLength)
            return output;
        return output[..maxLength] + "\n... (truncated)";
    }
}
