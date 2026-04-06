using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DotNetMcp.Tools;

[McpServerToolType]
public static class InfoTools
{
    [McpServerTool(Name = "dotnet_info"), Description("Get .NET SDK and runtime version info")]
    public static string Info()
    {
        return "dotnet-mcp is alive!";
    }
}
