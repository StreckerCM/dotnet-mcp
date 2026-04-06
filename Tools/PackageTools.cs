using System.ComponentModel;
using System.Text;
using DotNetMcp.Services;
using ModelContextProtocol.Server;

namespace DotNetMcp.Tools;

[McpServerToolType]
public static class PackageTools
{
    [McpServerTool(Name = "dotnet_add_package"), Description("Add a NuGet package to a project")]
    public static async Task<string> AddPackage(
        DotNetCliService cli,
        [Description("Path to the project file (.csproj)")] string project_path,
        [Description("NuGet package name")] string package_name,
        [Description("Package version (latest if omitted)")] string? version = null)
    {
        var args = new StringBuilder($"add \"{project_path}\" package {package_name}");

        if (!string.IsNullOrEmpty(version))
            args.Append($" --version {version}");

        var result = await cli.RunAsync(args.ToString());

        if (result.ExitCode == 0)
            return $"Added {package_name}" + (version is not null ? $" v{version}" : "") + $" to {Path.GetFileName(project_path)}";

        var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
        return $"FAILED to add {package_name}\n\n{error.Trim()}";
    }

    [McpServerTool(Name = "dotnet_remove_package"), Description("Remove a NuGet package from a project")]
    public static async Task<string> RemovePackage(
        DotNetCliService cli,
        [Description("Path to the project file (.csproj)")] string project_path,
        [Description("NuGet package name to remove")] string package_name)
    {
        var result = await cli.RunAsync($"remove \"{project_path}\" package {package_name}");

        if (result.ExitCode == 0)
            return $"Removed {package_name} from {Path.GetFileName(project_path)}";

        var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
        return $"FAILED to remove {package_name}\n\n{error.Trim()}";
    }

    [McpServerTool(Name = "dotnet_list_packages"), Description("List NuGet packages in a project or solution")]
    public static async Task<string> ListPackages(
        DotNetCliService cli,
        [Description("Path to project or solution file")] string project_path,
        [Description("Show outdated packages")] bool outdated = false)
    {
        var args = $"list \"{project_path}\" package" + (outdated ? " --outdated" : "");
        var result = await cli.RunAsync(args);

        if (result.ExitCode != 0)
        {
            var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
            return "FAILED\n\n" + error.Trim();
        }

        return result.Stdout.Trim();
    }

    [McpServerTool(Name = "dotnet_nuget_push"), Description("Push a NuGet package to a feed")]
    public static async Task<string> NuGetPush(
        DotNetCliService cli,
        [Description("Path to the .nupkg file")] string package_path,
        [Description("NuGet feed URL (e.g., https://api.nuget.org/v3/index.json)")] string source,
        [Description("API key for the NuGet feed")] string api_key)
    {
        var result = await cli.RunAsync($"nuget push \"{package_path}\" --source \"{source}\" --api-key {api_key}");

        if (result.ExitCode == 0)
            return $"Pushed {Path.GetFileName(package_path)} to {source}";

        var error = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
        return $"FAILED to push {Path.GetFileName(package_path)}\n\n{error.Trim()}";
    }
}
