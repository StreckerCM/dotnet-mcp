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
        var result = await cli.RunAsync("--info");
        if (result.ExitCode != 0)
            return "Failed to get dotnet info:\n" + result.Stderr;

        return result.Stdout.Trim();
    }

    [McpServerTool(Name = "dotnet_sln_list"), Description("List projects in a solution")]
    public static async Task<string> SlnList(
        DotNetCliService cli,
        [Description("Path to .sln file")] string solution_path)
    {
        var result = await cli.RunAsync($"sln \"{solution_path}\" list");

        if (result.ExitCode != 0)
        {
            var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
            return "FAILED\n\n" + error.Trim();
        }

        return result.Stdout.Trim();
    }
}
