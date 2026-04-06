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

### Core

| Tool | Description | Key Parameters |
|------|-------------|----------------|
| `dotnet_info` | Get SDK/runtime version info | (none) |
| `dotnet_restore` | Restore NuGet packages | `project_path` |
| `dotnet_build` | Build a solution or project | `project_path`, `configuration`, `no_restore` |
| `dotnet_test` | Run tests with structured results | `project_path`, `configuration`, `filter`, `no_build` |

### Package Management

| Tool | Description | Key Parameters |
|------|-------------|----------------|
| `dotnet_add_package` | Add a NuGet package | `project_path`, `package_name`, `version` |
| `dotnet_remove_package` | Remove a NuGet package | `project_path`, `package_name` |
| `dotnet_list_packages` | List installed packages | `project_path`, `outdated` |
| `dotnet_nuget_push` | Push a package to a feed | `package_path`, `source`, `api_key` |

### Project Operations

| Tool | Description | Key Parameters |
|------|-------------|----------------|
| `dotnet_clean` | Clean build outputs | `project_path`, `configuration` |
| `dotnet_run` | Run a project | `project_path`, `args`, `configuration` |
| `dotnet_format` | Format code | `project_path`, `verify_no_changes` |
| `dotnet_publish` | Publish for deployment | `project_path`, `configuration`, `runtime`, `output` |
| `dotnet_sln_list` | List projects in a solution | `solution_path` |

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

> **No `env` block needed.** The server automatically reconstructs all required Windows environment variables (`ProgramData`, `SystemRoot`, `APPDATA`, `TEMP`, `DOTNET_ROOT`, etc.) using Windows APIs (`Environment.GetFolderPath`) before spawning any `dotnet` child process. This works even when Claude Code's sandbox strips the host shell environment.

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

Claude Code's sandbox on Windows strips critical environment variables (notably `ProgramData` and `PATH`) from all child processes, including MCP servers. This causes two problems:

1. **`dotnet` not on PATH** — The server can't find `dotnet.exe` to spawn child processes. Solved by using the full path (`C:\Program Files\dotnet\dotnet.exe`) resolved via `Environment.GetFolderPath()`, and by providing `PATH` in the `.mcp.json` `env` block as a belt-and-suspenders approach.

2. **Missing Windows paths** — NuGet/MSBuild need `ProgramData`, `APPDATA`, etc. to resolve package caches and temp directories. Solved by reconstructing these from `Environment.GetFolderPath()` (a .NET API that reads from the Windows registry, not environment variables) before spawning each `dotnet` command.

### SDK 10.0 Compatibility

.NET SDK 10.0+ has a known bug where `dotnet --info` crashes with a `NullReferenceException` during workload enumeration. The `dotnet_info` tool avoids this entirely by using `dotnet --version`, `dotnet --list-sdks`, and `dotnet --list-runtimes` instead — individual commands that don't trigger the workload subsystem.

The server communicates with Claude Code over stdin/stdout using the MCP protocol (JSON-RPC 2.0, newline-delimited).

## Development

```bash
# Build
dotnet build -c Release

# Run tests
dotnet test

# Quick smoke test — send MCP initialize + tools/list
{
  echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"0.1"}}}'
  echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
  sleep 2
} | ./bin/Release/net8.0/dotnet-mcp.exe 2>/dev/null
```

### Test Suite

The test project (`tests/dotnet-mcp.Tests`) uses xUnit and NSubstitute with 81 tests covering:

- **SanitizePath** — input sanitization edge cases (quotes, null bytes, whitespace)
- **Output formatting** — build, restore, and test result parsing (errors, warnings, timeouts, summaries)
- **Argument construction** — CLI argument assembly for all 15 tools via mocked `DotNetCliService`
- **EnsureEnvironment** — env var reconstruction logic that fixes the sandbox stripping issue

## License

MIT
