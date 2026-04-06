using System.ComponentModel;
using System.Text;
using DotNetMcp.Services;
using ModelContextProtocol.Server;

namespace DotNetMcp.Tools;

[McpServerToolType]
public static class InfoTools
{
    [McpServerTool(Name = "dotnet_info"), Description("Get .NET SDK and runtime version info")]
    public static async Task<string> Info(DotNetCliService cli)
    {
        // Avoid `dotnet --info` — it triggers a NullReferenceException in SDK 10.0+
        // during workload enumeration. Instead, use individual commands that give us
        // the same information without touching the workload subsystem.
        var versionTask = cli.RunAsync("--version");
        var sdksTask = cli.RunAsync("--list-sdks");
        var runtimesTask = cli.RunAsync("--list-runtimes");

        await Task.WhenAll(versionTask, sdksTask, runtimesTask);

        var version = await versionTask;
        var sdks = await sdksTask;
        var runtimes = await runtimesTask;

        var sb = new StringBuilder();

        // Active SDK version
        if (version.ExitCode == 0 && !string.IsNullOrWhiteSpace(version.Stdout))
            sb.AppendLine($"Active SDK: {version.Stdout.Trim()}");

        // Installed SDKs
        sb.AppendLine();
        sb.AppendLine("Installed SDKs:");
        if (sdks.ExitCode == 0 && !string.IsNullOrWhiteSpace(sdks.Stdout))
            sb.AppendLine(sdks.Stdout.Trim());
        else
            sb.AppendLine("  (unable to list SDKs)");

        // Installed runtimes
        sb.AppendLine();
        sb.AppendLine("Installed Runtimes:");
        if (runtimes.ExitCode == 0 && !string.IsNullOrWhiteSpace(runtimes.Stdout))
            sb.AppendLine(runtimes.Stdout.Trim());
        else
            sb.AppendLine("  (unable to list runtimes)");

        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "dotnet_sln_list"), Description("List projects in a solution")]
    public static async Task<string> SlnList(
        DotNetCliService cli,
        [Description("Path to .sln file")] string solution_path)
    {
        solution_path = DotNetCliService.SanitizePath(solution_path);
        var result = await cli.RunAsync($"sln \"{solution_path}\" list");

        if (result.ExitCode != 0)
        {
            var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
            return "FAILED\n\n" + error.Trim();
        }

        return result.Stdout.Trim();
    }
}
