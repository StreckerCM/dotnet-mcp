# CLAUDE.md

## Project Overview

MCP (Model Context Protocol) server for .NET SDK operations on Windows. Provides `dotnet build`, `dotnet test`, `dotnet restore`, and other CLI commands as MCP tools, running as a native Windows process with full environment access. This bypasses shell sandbox limitations that strip critical environment variables (like `ProgramData`) causing NuGet restore failures.

## Tech Stack

- .NET 8.0
- Model Context Protocol (MCP) SDK
- Runs as stdio MCP server (no TCP/WSL bridge needed — native Windows)

## Key Design Decisions

- **stdio transport** — Claude Code connects directly, no TCP bridge needed since we're on Windows natively
- **Full environment inheritance** — The MCP server process inherits the complete Windows environment, solving the sandbox env var stripping issue
- **Simple tool surface** — Wrap common `dotnet` CLI commands as MCP tools with structured input/output
- **Solution/project-aware** — Tools accept solution or project paths

## Development Workflow

- All development happens on `main` branch (simple project, no branch protection needed initially)
- Test by configuring the MCP in Claude Code settings and verifying `dotnet` commands work
