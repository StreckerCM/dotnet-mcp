using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetMcp.Services;
using ModelContextProtocol.Server;

namespace DotNetMcp.Tools;

[McpServerToolType]
public static class SdkPinTools
{
    private const string GlobalJsonFile = "global.json";

    [McpServerTool(Name = "dotnet_sdk_pin"), Description(
        "Pin, read, or remove the .NET SDK version for a directory via global.json. " +
        "Use this to force a specific SDK version (e.g., pin to 8.x when SDK 10 causes issues).")]
    public static async Task<string> SdkPin(
        DotNetCliService cli,
        [Description("Action to perform: 'set' to pin, 'get' to read current pin, 'remove' to unpin")] string action,
        [Description("Directory to manage global.json in (defaults to current directory)")] string? directory = null,
        [Description("SDK version to pin to (required for 'set'). Can be exact (e.g., '8.0.419') or major.minor (e.g., '8.0')")] string? sdk_version = null,
        [Description("Roll-forward policy: 'latestFeature' (default — latest installed matching major.minor), 'latestPatch', 'latestMajor', 'disable' (exact match only)")] string roll_forward = "latestFeature")
    {
        directory = string.IsNullOrWhiteSpace(directory)
            ? Directory.GetCurrentDirectory()
            : DotNetCliService.SanitizePath(directory);

        if (string.IsNullOrWhiteSpace(directory))
            return "ERROR: Invalid directory path.";

        return action.ToLowerInvariant() switch
        {
            "set" => await SetSdkPin(cli, directory, sdk_version, roll_forward),
            "get" => GetSdkPin(directory),
            "remove" => RemoveSdkPin(directory),
            _ => "ERROR: Invalid action. Use 'set', 'get', or 'remove'."
        };
    }

    private static async Task<string> SetSdkPin(DotNetCliService cli, string directory, string? sdkVersion, string rollForward)
    {
        if (string.IsNullOrWhiteSpace(sdkVersion))
            return "ERROR: sdk_version is required for 'set' action.";

        if (!Directory.Exists(directory))
            return $"ERROR: Directory not found: {directory}";

        var validPolicies = new[] { "disable", "patch", "feature", "minor", "major", "latestPatch", "latestFeature", "latestMinor", "latestMajor" };
        if (!validPolicies.Contains(rollForward, StringComparer.OrdinalIgnoreCase))
            return $"ERROR: Invalid rollForward policy '{rollForward}'. Valid values: {string.Join(", ", validPolicies)}";

        var globalJsonPath = Path.Combine(directory, GlobalJsonFile);

        // Read existing global.json to preserve non-sdk properties
        JsonObject root;
        if (File.Exists(globalJsonPath))
        {
            try
            {
                var existing = await File.ReadAllTextAsync(globalJsonPath);
                root = JsonNode.Parse(existing)?.AsObject() ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        // Set or replace the sdk section
        var sdkNode = new JsonObject
        {
            ["version"] = sdkVersion,
            ["rollForward"] = rollForward
        };
        root["sdk"] = sdkNode;

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(globalJsonPath, root.ToJsonString(options));

        // Verify the pin by asking dotnet which SDK it resolves to
        var verify = await cli.RunAsync("--version", workingDirectory: directory);
        var resolvedVersion = verify.ExitCode == 0 ? verify.Stdout.Trim() : "(unable to verify)";

        return $"SDK pinned to {sdkVersion} (rollForward: {rollForward}) in {globalJsonPath}\nResolved SDK: {resolvedVersion}";
    }

    internal static string GetSdkPin(string directory)
    {
        if (!Directory.Exists(directory))
            return $"ERROR: Directory not found: {directory}";

        var globalJsonPath = Path.Combine(directory, GlobalJsonFile);
        if (!File.Exists(globalJsonPath))
            return $"No global.json found in {directory} — using default SDK.";

        try
        {
            var content = File.ReadAllText(globalJsonPath);
            var root = JsonNode.Parse(content);
            var sdk = root?["sdk"];

            if (sdk is null)
                return $"global.json exists in {directory} but has no sdk section — using default SDK.";

            var version = sdk["version"]?.GetValue<string>() ?? "(not set)";
            var rollForward = sdk["rollForward"]?.GetValue<string>() ?? "(not set)";

            return $"SDK pin in {globalJsonPath}:\n  Version: {version}\n  Roll-forward: {rollForward}";
        }
        catch (JsonException ex)
        {
            return $"ERROR: Failed to parse {globalJsonPath}: {ex.Message}";
        }
    }

    internal static string RemoveSdkPin(string directory)
    {
        if (!Directory.Exists(directory))
            return $"ERROR: Directory not found: {directory}";

        var globalJsonPath = Path.Combine(directory, GlobalJsonFile);
        if (!File.Exists(globalJsonPath))
            return $"No global.json found in {directory} — nothing to remove.";

        try
        {
            var content = File.ReadAllText(globalJsonPath);
            var root = JsonNode.Parse(content)?.AsObject();

            if (root is null)
            {
                File.Delete(globalJsonPath);
                return $"Removed {globalJsonPath} (was empty/invalid).";
            }

            root.Remove("sdk");

            if (root.Count == 0)
            {
                // No other properties — delete the file entirely
                File.Delete(globalJsonPath);
                return $"Removed {globalJsonPath} (no remaining properties).";
            }

            // Preserve other properties (e.g., msbuild-sdks)
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(globalJsonPath, root.ToJsonString(options));
            return $"Removed sdk section from {globalJsonPath} (preserved other properties).";
        }
        catch (JsonException ex)
        {
            return $"ERROR: Failed to parse {globalJsonPath}: {ex.Message}";
        }
    }
}
