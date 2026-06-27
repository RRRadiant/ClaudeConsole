using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Environment dependency detection service.
/// Ported from Claude-Win src/main/ipc/deps.ts.
/// Detects Node.js, npm, and Git installations on Windows.
/// </summary>
public sealed class EnvironmentService : IEnvironmentService
{
    public static EnvironmentService Instance { get; } = new();

    private EnvironmentService() { }

    // ── Public model ───────────────────────────────────────

    public sealed class DepCheckResult
    {
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool Installed { get; init; }
        public string? Version { get; init; }
        public string? DownloadUrl { get; init; }

        public DepCheckResult(string name, string description, bool installed, string? version, string? downloadUrl)
        {
            Name = name;
            Description = description;
            Installed = installed;
            Version = version;
            DownloadUrl = downloadUrl;
        }
    }

    // ── Process helpers ────────────────────────────────────

    /// <summary>
    /// Runs a process, drains stdout/stderr asynchronously, and returns (exitCode, stdout).
    /// Uses cmd.exe /c wrapper for .cmd/.bat files to ensure reliable execution across
    /// Windows versions, matching the proven pattern in InstallerService.RunCommandAsync.
    /// </summary>
    private static async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(
        string fileName, string arguments, int timeoutMs = 5000)
    {
        var result = await ProcessRunner.RunAsync(fileName, arguments, timeoutMs).ConfigureAwait(false);
        return (result.ExitCode, result.Stdout, result.Stderr);
    }

    /// <summary>
    /// Finds the first path for a command using 'where', or null if not in PATH.
    /// Falls back to .cmd/.exe variants when 'where' returns a path without extension
    /// (common for npm/node on some Windows installations).
    /// </summary>
    private static async Task<string?> FindInPathAsync(string cmd)
    {
        // Primary: 'where cmd'
        var (exitCode, stdout, _) = await RunProcessAsync("where", cmd);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
        {
            var path = stdout.Split('\n')[0].Trim();

            // Direct match
            if (File.Exists(path))
                return path;

            // 'where npm' sometimes returns the bare name on some systems;
            // try .cmd / .exe variants
            foreach (var ext in new[] { ".cmd", ".exe" })
            {
                if (File.Exists(path + ext))
                    return path + ext;
            }
        }

        // Fallback: 'where cmd.cmd' (some shells need explicit extension)
        var (exitCode2, stdout2, _) = await RunProcessAsync("where", $"{cmd}.cmd");
        if (exitCode2 == 0 && !string.IsNullOrWhiteSpace(stdout2))
        {
            var path = stdout2.Split('\n')[0].Trim();
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    // ── Command detection ──────────────────────────────────

    /// <summary>
    /// Check if a command exists by trying 'where', then searching
    /// common installation directories with .cmd/.exe variants.
    /// </summary>
    private static async Task<bool> HasCmdAsync(string cmd)
    {
        // 1. PATH search via 'where'
        var pathFromWhere = await FindInPathAsync(cmd);
        if (!string.IsNullOrEmpty(pathFromWhere) && File.Exists(pathFromWhere))
        {
            var (exitCode, _, _) = await RunProcessAsync(pathFromWhere, "--version");
            if (exitCode == 0) return true;
        }

        // 2. Search common install directories
        var homedir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var dirs = new[]
        {
            Path.Combine(localAppData, "Programs", "nodejs"),
            Path.Combine(homedir, ".npm-global", "bin"),
            Path.Combine(appData, "npm"),
            Path.Combine(programFiles, "nodejs"),
            @"C:\Program Files\nodejs",
            @"C:\Program Files (x86)\nodejs",
        };

        foreach (var dir in dirs)
        {
            foreach (var ext in new[] { ".cmd", ".exe", "" })
            {
                var fullPath = Path.Combine(dir, $"{cmd}{ext}");
                if (File.Exists(fullPath))
                {
                    var (exitCode, _, _) = await RunProcessAsync(fullPath, "--version");
                    if (exitCode == 0) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Get the installed version of a command, or null if not found.
    /// </summary>
    private static async Task<string?> GetCmdVersionAsync(string cmd)
    {
        var foundPath = await FindInPathAsync(cmd);
        if (string.IsNullOrEmpty(foundPath) || !File.Exists(foundPath))
            return null;

        var (exitCode, stdout, _) = await RunProcessAsync(foundPath, "--version");
        return (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout)) ? stdout : null;
    }

    // ── Git-specific detection ─────────────────────────────

    private static async Task<bool> HasGitAsync()
    {
        // 1. PATH search
        if (!string.IsNullOrEmpty(await FindInPathAsync("git")))
            return true;

        // 2. Common Git directories
        var homedir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var gitDirs = new[]
        {
            @"C:\Git\bin\git.exe",
            @"C:\Git\cmd\git.exe",
            @"C:\Program Files\Git\bin\git.exe",
            @"C:\Program Files (x86)\Git\bin\git.exe",
            Path.Combine(homedir, "AppData", "Local", "Programs", "Git", "bin", "git.exe"),
        };

        foreach (var gitPath in gitDirs)
        {
            if (File.Exists(gitPath))
            {
                var (exitCode, _, _) = await RunProcessAsync(gitPath, "--version");
                if (exitCode == 0) return true;
            }
        }

        return false;
    }

    private static async Task<string?> GetGitVersionAsync()
    {
        var (exitCode, stdout, _) = await RunProcessAsync("git", "--version");
        return (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout)) ? stdout : null;
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>
    /// Check all environment dependencies (Node.js, npm, Git).
    /// </summary>
    public async Task<List<DepCheckResult>> CheckAllDepsAsync()
    {
        var nodeInstalled = await HasCmdAsync("node");
        var npmInstalled = await HasCmdAsync("npm");
        var gitInstalled = await HasGitAsync();

        var results = new List<DepCheckResult>
        {
            new(
                name: "node",
                description: "Node.js",
                installed: nodeInstalled,
                version: nodeInstalled ? await GetCmdVersionAsync("node") : null,
                downloadUrl: "https://nodejs.org/en/download"
            ),
            new(
                name: "npm",
                description: "npm",
                installed: npmInstalled,
                version: npmInstalled ? await GetCmdVersionAsync("npm") : null,
                downloadUrl: string.Empty
            ),
            new(
                name: "git",
                description: "Git",
                installed: gitInstalled,
                version: gitInstalled ? await GetGitVersionAsync() : null,
                downloadUrl: "https://git-scm.com/download/win"
            ),
        };

        return results;
    }

    /// <summary>
    /// Open the download URL in the system browser.
    /// </summary>
    public void OpenDownloadUrl(string depType)
    {
        var url = depType switch
        {
            "node" => "https://nodejs.org/en/download",
            "git" => "https://git-scm.com/download/win",
            _ => throw new ArgumentException($"Unknown dependency type: {depType}")
        };

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
