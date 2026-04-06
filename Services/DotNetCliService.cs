using System.Diagnostics;
using System.Text;

namespace DotNetMcp.Services;

public sealed class DotNetCliService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public async Task<CliResult> RunAsync(
        string arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentOverrides = null,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetDotnetPath(),
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Claude Code's sandbox strips critical environment variables.
        // Restore them from the Windows API so dotnet restore/build/test work.
        EnsureEnvironment(psi);

        // Apply caller-provided env vars (e.g. secrets that shouldn't be on the command line)
        if (environmentOverrides is not null)
        {
            foreach (var (key, value) in environmentOverrides)
                psi.Environment[key] = value;
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };

        process.Start();
        process.StandardInput.Close(); // EOF so child doesn't block reading MCP's stdin pipe
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var effectiveTimeout = timeout ?? DefaultTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(effectiveTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            // Flush buffered stdout/stderr — WaitForExitAsync returns when the
            // process exits, but the async pipe readers may still be draining.
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);

            if (!cancellationToken.IsCancellationRequested)
            {
                // Internal timeout
                return new CliResult(
                    ExitCode: -1,
                    Stdout: stdout.ToString(),
                    Stderr: stderr.ToString(),
                    TimedOut: true);
            }

            // Caller cancellation (e.g. MCP server shutting down) — child is
            // killed above, now let the cancellation propagate normally.
            throw;
        }

        return new CliResult(
            ExitCode: process.ExitCode,
            Stdout: stdout.ToString(),
            Stderr: stderr.ToString(),
            TimedOut: false);
    }

    private static void EnsureEnvironment(ProcessStartInfo psi)
    {
        // Map Windows special folders to the env vars that dotnet/NuGet/MSBuild expect.
        // Environment.GetFolderPath reads from the Windows API, not env vars,
        // so it works even when the shell environment is stripped.
        var folderMappings = new (string EnvVar, Environment.SpecialFolder Folder)[]
        {
            ("ProgramData",      Environment.SpecialFolder.CommonApplicationData),
            ("ProgramFiles",     Environment.SpecialFolder.ProgramFiles),
            ("APPDATA",          Environment.SpecialFolder.ApplicationData),
            ("LOCALAPPDATA",     Environment.SpecialFolder.LocalApplicationData),
            ("USERPROFILE",      Environment.SpecialFolder.UserProfile),
            ("TEMP",             Environment.SpecialFolder.LocalApplicationData),
            ("TMP",              Environment.SpecialFolder.LocalApplicationData),
            ("CommonProgramFiles", Environment.SpecialFolder.CommonProgramFiles),
        };

        foreach (var (envVar, folder) in folderMappings)
        {
            psi.Environment.TryGetValue(envVar, out var existing);
            if (string.IsNullOrEmpty(existing))
            {
                var path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(path))
                {
                    // TEMP/TMP need the \Temp suffix
                    if (envVar is "TEMP" or "TMP")
                        path = Path.Combine(path, "Temp");

                    psi.Environment[envVar] = path;
                }
            }
        }

        // ProgramFiles(x86) and CommonProgramFiles(x86) have no simple SpecialFolder enum
        psi.Environment.TryGetValue("ProgramFiles(x86)", out var pfx86);
        if (string.IsNullOrEmpty(pfx86))
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pf))
                psi.Environment["ProgramFiles(x86)"] = pf;
        }

        psi.Environment.TryGetValue("CommonProgramFiles(x86)", out var cpfx86);
        if (string.IsNullOrEmpty(cpfx86))
        {
            var cpf = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);
            if (!string.IsNullOrEmpty(cpf))
                psi.Environment["CommonProgramFiles(x86)"] = cpf;
        }

        // System-level env vars that .NET SDK, MSBuild, and workload resolution depend on.
        // These have no SpecialFolder equivalent — derive from the Windows directory.
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(winDir))
        {
            SetIfMissing(psi, "SystemRoot", winDir);
            SetIfMissing(psi, "windir", winDir);
            SetIfMissing(psi, "SystemDrive", Path.GetPathRoot(winDir)?.TrimEnd('\\') ?? "C:");
        }

        // HOME / HOMEDRIVE / HOMEPATH — many .NET tools and NuGet use these
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            SetIfMissing(psi, "HOME", userProfile);
            SetIfMissing(psi, "HOMEDRIVE", Path.GetPathRoot(userProfile)?.TrimEnd('\\') ?? "C:");
            SetIfMissing(psi, "HOMEPATH", userProfile.Substring(Path.GetPathRoot(userProfile)?.Length ?? 0));
        }

        // DOTNET_ROOT — tells the SDK where to find itself (runtimes, workloads, etc.)
        var dotnetPath = GetDotnetPath();
        var dotnetDir = Path.GetDirectoryName(dotnetPath);
        if (dotnetDir is not null)
        {
            SetIfMissing(psi, "DOTNET_ROOT", dotnetDir);

            // Ensure dotnet is on PATH (PATH itself may be absent in a stripped sandbox)
            psi.Environment.TryGetValue("PATH", out var currentPath);
            if (string.IsNullOrEmpty(currentPath))
            {
                // Build a minimal PATH so child processes (MSBuild, NuGet) can find system tools
                var system32 = Path.Combine(winDir ?? @"C:\Windows", "System32");
                psi.Environment["PATH"] = dotnetDir + ";" + system32;
            }
            else if (!currentPath.Contains(dotnetDir, StringComparison.OrdinalIgnoreCase))
            {
                psi.Environment["PATH"] = dotnetDir + ";" + currentPath;
            }
        }
    }

    /// <summary>
    /// Strip characters that could break out of quoted CLI arguments.
    /// Since UseShellExecute=false, CreateProcess is called directly (no cmd.exe),
    /// but the target program's CRT argument parser still splits on unbalanced quotes.
    /// </summary>
    public static string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        // Remove double quotes (argument delimiter escape) and null bytes
        return path.Replace("\"", "").Replace("\0", "");
    }

    private static void SetIfMissing(ProcessStartInfo psi, string envVar, string value)
    {
        psi.Environment.TryGetValue(envVar, out var existing);
        if (string.IsNullOrEmpty(existing))
            psi.Environment[envVar] = value;
    }

    private static string GetDotnetPath()
    {
        // Prefer the standard install location
        var standard = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet", "dotnet.exe");
        if (File.Exists(standard))
            return standard;

        return "dotnet"; // fall back to PATH resolution
    }

    private static void TryKillProcess(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
    }
}

public record CliResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut);
