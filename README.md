# dotnet-mcp

An MCP (Model Context Protocol) server that exposes .NET SDK commands as tools for Claude Code. Bypasses Claude Code's shell sandbox on Windows which strips critical environment variables (like `ProgramData`), causing `dotnet restore/build/test` to fail with `Value cannot be null. (Parameter 'path1')`.

The server reconstructs the correct environment using Windows APIs (`Environment.GetFolderPath`) before spawning any `dotnet` child process, ensuring NuGet restore and MSBuild work correctly regardless of the host shell's environment.

## Architecture

```
Claude Code (sandboxed shell)
    │
    ├── stdin/stdout (JSON-RPC 2.0)
    │
    ▼
dotnet-mcp.exe (native Windows process, full environment)
    │
    ├── dotnet build ...
    ├── dotnet test ...
    └── etc.
```

## Tools

| Tool | Description | Key Parameters |
|------|-------------|----------------|
| `dotnet_info` | Verify the server is alive | (none) |
| `dotnet_build` | Build a solution or project | `project_path`, `configuration`, `no_restore` |
| `dotnet_test` | Run tests with structured results | `project_path`, `configuration`, `filter`, `no_build` |

Build errors/warnings and test results are parsed into structured output so Claude can act on them without parsing raw CLI noise.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed and on PATH

## Installation

### 1. Clone and build

```bash
git clone https://github.com/StreckerCM/dotnet-mcp.git
cd dotnet-mcp
dotnet build -c Release
```

The compiled server will be at `bin/Release/net8.0/dotnet-mcp.exe`.

### 2. Configure Claude Code

Add the server to your Claude Code MCP settings. You can configure it at the user level or per-project.

**User level** (`~/.claude/.mcp.json`):

```json
{
  "mcpServers": {
    "dotnet": {
      "command": "C:\\path\\to\\dotnet-mcp\\bin\\Release\\net8.0\\dotnet-mcp.exe"
    }
  }
}
```

**Per-project** (`.mcp.json` in your repo root):

```json
{
  "mcpServers": {
    "dotnet": {
      "command": "C:\\path\\to\\dotnet-mcp\\bin\\Release\\net8.0\\dotnet-mcp.exe"
    }
  }
}
```

Replace `C:\path\to\dotnet-mcp` with the actual path where you cloned the repo.

### 3. Restart Claude Code

Start a new Claude Code session. The `dotnet_info`, `dotnet_build`, and `dotnet_test` tools will appear in `/mcp`.

### 4. Verify

Ask Claude to run:

```
Use the dotnet_build tool to build my solution.
```

If it returns build results instead of a sandbox error, the server is working.

## Usage Examples

**Build a solution:**
> Build the DownholePro solution in Release configuration

**Run tests:**
> Run the tests in tests/DownholePro.Survey.Tests

**Run filtered tests:**
> Run only the tests matching "Minimum_Curvature" in the survey test project

## How It Works

Claude Code's Bash tool runs in a sandboxed shell on Windows that strips critical environment variables (notably `ProgramData`). This causes NuGet package resolution to fail with `Value cannot be null. (Parameter 'path1')`.

MCP servers are spawned as child processes by Claude Code. While they inherit the same stripped environment, this server explicitly reconstructs the required variables using `Environment.GetFolderPath()` — a .NET API that reads from the Windows registry/system APIs rather than environment variables. This ensures `ProgramData`, `APPDATA`, `LOCALAPPDATA`, `USERPROFILE`, `TEMP`, and other critical paths are always correct before spawning `dotnet` CLI commands.

The server communicates with Claude Code over stdin/stdout using the MCP protocol (JSON-RPC 2.0, newline-delimited).

## Development

```bash
# Build
dotnet build -c Release

# Quick smoke test — send MCP initialize + tools/list
{
  echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"0.1"}}}'
  echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
  sleep 2
} | ./bin/Release/net8.0/dotnet-mcp.exe 2>/dev/null
```

## License

MIT
