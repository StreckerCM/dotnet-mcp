using System.Diagnostics;
using DotNetMcp.Services;

namespace DotNetMcp.Tests;

public class EnsureEnvironmentTests
{
    private static ProcessStartInfo CreateStrippedPsi()
    {
        // Simulate a fully stripped sandbox environment
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false
        };
        // Clear all env vars to simulate sandbox stripping
        psi.Environment.Clear();
        return psi;
    }

    [Fact]
    public void RestoresProgramData_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("ProgramData"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["ProgramData"]));
    }

    [Fact]
    public void RestoresProgramFiles_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("ProgramFiles"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["ProgramFiles"]));
    }

    [Fact]
    public void RestoresAppData_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("APPDATA"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["APPDATA"]));
    }

    [Fact]
    public void RestoresLocalAppData_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("LOCALAPPDATA"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["LOCALAPPDATA"]));
    }

    [Fact]
    public void RestoresUserProfile_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("USERPROFILE"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["USERPROFILE"]));
    }

    [Fact]
    public void RestoresTemp_WithTempSuffix()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("TEMP"));
        Assert.EndsWith("Temp", psi.Environment["TEMP"]!);
    }

    [Fact]
    public void RestoresTmp_WithTempSuffix()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("TMP"));
        Assert.EndsWith("Temp", psi.Environment["TMP"]!);
    }

    [Fact]
    public void RestoresSystemRoot_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("SystemRoot"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["SystemRoot"]));
    }

    [Fact]
    public void RestoresWinDir_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("windir"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["windir"]));
    }

    [Fact]
    public void RestoresDotnetRoot_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("DOTNET_ROOT"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["DOTNET_ROOT"]));
    }

    [Fact]
    public void RestoresHome_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("HOME"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["HOME"]));
    }

    [Fact]
    public void RestoresPath_WhenMissing()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("PATH"));
        Assert.False(string.IsNullOrEmpty(psi.Environment["PATH"]));
    }

    [Fact]
    public void DoesNotOverwrite_ExistingValues()
    {
        var psi = CreateStrippedPsi();
        psi.Environment["ProgramData"] = @"D:\CustomProgramData";
        psi.Environment["APPDATA"] = @"D:\CustomAppData";

        DotNetCliService.EnsureEnvironment(psi);

        Assert.Equal(@"D:\CustomProgramData", psi.Environment["ProgramData"]);
        Assert.Equal(@"D:\CustomAppData", psi.Environment["APPDATA"]);
    }

    [Fact]
    public void Path_IncludesDotnetDirectory()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.Contains("dotnet", psi.Environment["PATH"]!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Path_IncludesSystem32()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.Contains("System32", psi.Environment["PATH"]!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemDrive_SetCorrectly()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("SystemDrive"));
        // Should be something like "C:" without trailing backslash
        var drive = psi.Environment["SystemDrive"]!;
        Assert.Matches(@"^[A-Z]:$", drive);
    }

    [Fact]
    public void HomeDrive_SetCorrectly()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("HOMEDRIVE"));
        Assert.True(psi.Environment.ContainsKey("HOMEPATH"));
    }

    [Fact]
    public void ProgramFilesX86_Set()
    {
        var psi = CreateStrippedPsi();

        DotNetCliService.EnsureEnvironment(psi);

        Assert.True(psi.Environment.ContainsKey("ProgramFiles(x86)"));
    }
}
