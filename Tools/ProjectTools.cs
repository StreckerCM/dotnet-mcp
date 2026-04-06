using System.ComponentModel;
using System.Text;
using DotNetMcp.Services;
using ModelContextProtocol.Server;

namespace DotNetMcp.Tools;

[McpServerToolType]
public static class ProjectTools
{
    [McpServerTool(Name = "dotnet_clean"), Description("Clean build outputs for a project or solution")]
    public static async Task<string> Clean(
        DotNetCliService cli,
        [Description("Path to solution or project file")] string? project_path = null,
        [Description("Build configuration to clean (Debug or Release)")] string configuration = "Debug")
    {
        var args = new StringBuilder("clean");

        if (!string.IsNullOrEmpty(project_path))
            args.Append($" \"{project_path}\"");

        args.Append($" -c {configuration}");

        var result = await cli.RunAsync(args.ToString());

        if (result.ExitCode == 0)
            return "CLEAN SUCCEEDED";

        var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
        return "CLEAN FAILED\n\n" + error.Trim();
    }

    [McpServerTool(Name = "dotnet_run"), Description("Run a .NET project")]
    public static async Task<string> Run(
        DotNetCliService cli,
        [Description("Path to the project file or directory")] string? project_path = null,
        [Description("Arguments to pass to the application")] string? args = null,
        [Description("Build configuration (Debug or Release)")] string configuration = "Debug")
    {
        var cmdArgs = new StringBuilder("run");

        if (!string.IsNullOrEmpty(project_path))
            cmdArgs.Append($" --project \"{project_path}\"");

        cmdArgs.Append($" -c {configuration}");

        if (!string.IsNullOrEmpty(args))
            cmdArgs.Append($" -- {args}");

        // Shorter timeout for run — 2 minutes
        var result = await cli.RunAsync(cmdArgs.ToString(), timeout: TimeSpan.FromMinutes(2));

        var sb = new StringBuilder();

        if (result.TimedOut)
        {
            sb.AppendLine("RUN TIMED OUT (2 min limit)");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(result.Stdout))
            sb.AppendLine(result.Stdout.Trim());

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            sb.AppendLine("Stderr:");
            sb.AppendLine(result.Stderr.Trim());
        }

        if (sb.Length == 0)
            sb.AppendLine(result.ExitCode == 0 ? "RUN COMPLETED (no output)" : $"RUN FAILED (exit code {result.ExitCode})");

        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "dotnet_format"), Description("Format code in a project or solution")]
    public static async Task<string> Format(
        DotNetCliService cli,
        [Description("Path to solution or project file")] string? project_path = null,
        [Description("Only check formatting without making changes")] bool verify_no_changes = false)
    {
        var args = new StringBuilder("format");

        if (!string.IsNullOrEmpty(project_path))
            args.Append($" \"{project_path}\"");

        if (verify_no_changes)
            args.Append(" --verify-no-changes");

        var result = await cli.RunAsync(args.ToString());

        if (result.ExitCode == 0)
            return verify_no_changes ? "FORMAT CHECK PASSED — no changes needed" : "FORMAT SUCCEEDED";

        if (verify_no_changes && result.ExitCode != 0)
        {
            var output = !string.IsNullOrWhiteSpace(result.Stdout) ? result.Stdout : result.Stderr;
            return "FORMAT CHECK FAILED — files need formatting\n\n" + output.Trim();
        }

        var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
        return "FORMAT FAILED\n\n" + error.Trim();
    }

    [McpServerTool(Name = "dotnet_publish"), Description("Publish a .NET project for deployment")]
    public static async Task<string> Publish(
        DotNetCliService cli,
        [Description("Path to the project file")] string? project_path = null,
        [Description("Build configuration (Debug or Release)")] string configuration = "Release",
        [Description("Target runtime (e.g., win-x64, linux-x64)")] string? runtime = null,
        [Description("Output directory")] string? output = null)
    {
        var args = new StringBuilder("publish");

        if (!string.IsNullOrEmpty(project_path))
            args.Append($" \"{project_path}\"");

        args.Append($" -c {configuration}");

        if (!string.IsNullOrEmpty(runtime))
            args.Append($" -r {runtime}");

        if (!string.IsNullOrEmpty(output))
            args.Append($" -o \"{output}\"");

        var result = await cli.RunAsync(args.ToString());

        if (result.ExitCode == 0)
        {
            // Extract the publish output path from stdout
            var sb = new StringBuilder("PUBLISH SUCCEEDED\n");
            foreach (var line in result.Stdout.Split('\n'))
            {
                if (line.Contains("publish", StringComparison.OrdinalIgnoreCase) && line.Contains("->"))
                    sb.AppendLine(line.Trim());
            }
            return sb.ToString().TrimEnd();
        }

        var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
        return "PUBLISH FAILED\n\n" + error.Trim();
    }
}
