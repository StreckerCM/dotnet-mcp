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
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Claude Code's sandbox strips critical environment variables.
        // Restore them from the Windows API so dotnet restore/build/test work.
        EnsureEnvironment(psi);

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
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var effectiveTimeout = timeout ?? DefaultTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(effectiveTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            return new CliResult(
                ExitCode: -1,
                Stdout: stdout.ToString(),
                Stderr: stderr.ToString(),
                TimedOut: true);
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
        var required = new (string EnvVar, Environment.SpecialFolder Folder)[]
        {
            ("ProgramData",      Environment.SpecialFolder.CommonApplicationData),
            ("ProgramFiles",     Environment.SpecialFolder.ProgramFiles),
            ("APPDATA",          Environment.SpecialFolder.ApplicationData),
            ("LOCALAPPDATA",     Environment.SpecialFolder.LocalApplicationData),
            ("USERPROFILE",      Environment.SpecialFolder.UserProfile),
            ("TEMP",             Environment.SpecialFolder.LocalApplicationData),
            ("TMP",              Environment.SpecialFolder.LocalApplicationData),
        };

        foreach (var (envVar, folder) in required)
        {
            if (string.IsNullOrEmpty(psi.Environment[envVar]))
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

        // ProgramFiles(x86) has no SpecialFolder enum — derive from ProgramFiles
        if (string.IsNullOrEmpty(psi.Environment["ProgramFiles(x86)"]))
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pf))
                psi.Environment["ProgramFiles(x86)"] = pf;
        }

        // Ensure dotnet is on PATH
        var dotnetDir = Path.GetDirectoryName(GetDotnetPath());
        if (dotnetDir is not null && psi.Environment.TryGetValue("PATH", out var currentPath))
        {
            if (!currentPath.Contains(dotnetDir, StringComparison.OrdinalIgnoreCase))
                psi.Environment["PATH"] = dotnetDir + ";" + currentPath;
        }
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
