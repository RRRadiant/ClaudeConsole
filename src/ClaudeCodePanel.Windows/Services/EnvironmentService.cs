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

    private readonly Func<string, string, int, Task<ProcessResult>> _runProcess;
    private readonly Func<string, bool> _fileExists;

    private EnvironmentService()
        : this(ProcessRunner.RunAsync, File.Exists)
    {
    }

    internal EnvironmentService(
        Func<string, string, int, Task<ProcessResult>> runProcess,
        Func<string, bool> fileExists)
    {
        _runProcess = runProcess;
        _fileExists = fileExists;
    }

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
    private async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(
        string fileName, string arguments, int timeoutMs = 5000)
    {
        var result = await _runProcess(fileName, arguments, timeoutMs).ConfigureAwait(false);
        return (result.ExitCode, result.Stdout, result.Stderr);
    }

    /// <summary>
    /// Finds the first path for a command using 'where', or null if not in PATH.
    /// Falls back to .cmd/.exe variants when 'where' returns a path without extension
    /// (common for npm/node on some Windows installations).
    /// </summary>
    private async Task<string?> FindInPathAsync(string cmd)
    {
        // Primary: 'where cmd'
        var (exitCode, stdout, _) = await RunProcessAsync("where", cmd);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
        {
            var path = stdout.Split('\n')[0].Trim();

            // Direct match
            if (_fileExists(path))
                return path;

            // 'where npm' sometimes returns the bare name on some systems;
            // try .cmd / .exe variants
            foreach (var ext in new[] { ".cmd", ".exe" })
            {
                if (_fileExists(path + ext))
                    return path + ext;
            }
        }

        // Fallback: 'where cmd.cmd' (some shells need explicit extension)
        var (exitCode2, stdout2, _) = await RunProcessAsync("where", $"{cmd}.cmd");
        if (exitCode2 == 0 && !string.IsNullOrWhiteSpace(stdout2))
        {
            var path = stdout2.Split('\n')[0].Trim();
            if (_fileExists(path))
                return path;
        }

        return null;
    }

    private async Task<(bool installed, string? version)> ProbeCommandAsync(
        string command,
        IEnumerable<string> fallbackPaths)
    {
        var path = await FindInPathAsync(command).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(path))
        {
            var result = await RunProcessAsync(path, "--version").ConfigureAwait(false);
            if (result.exitCode == 0)
            {
                return (true,
                    string.IsNullOrWhiteSpace(result.stdout) ? null : result.stdout);
            }
        }

        foreach (var fallbackPath in fallbackPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_fileExists(fallbackPath))
                continue;

            var result = await RunProcessAsync(fallbackPath, "--version").ConfigureAwait(false);
            if (result.exitCode == 0)
            {
                return (true,
                    string.IsNullOrWhiteSpace(result.stdout) ? null : result.stdout);
            }
        }

        return (false, null);
    }

    private static IEnumerable<string> GetCommandFallbackPaths(string command)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var directories = new[]
        {
            Path.Combine(localAppData, "Programs", "nodejs"),
            Path.Combine(home, ".npm-global", "bin"),
            Path.Combine(appData, "npm"),
            Path.Combine(programFiles, "nodejs"),
            @"C:\Program Files\nodejs",
            @"C:\Program Files (x86)\nodejs"
        };

        return directories.SelectMany(directory => new[]
        {
            Path.Combine(directory, $"{command}.cmd"),
            Path.Combine(directory, $"{command}.exe"),
            Path.Combine(directory, command)
        });
    }

    private static string[] GetGitFallbackPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            @"C:\Git\bin\git.exe",
            @"C:\Git\cmd\git.exe",
            @"C:\Program Files\Git\bin\git.exe",
            @"C:\Program Files (x86)\Git\bin\git.exe",
            Path.Combine(home, "AppData", "Local", "Programs", "Git", "bin", "git.exe")
        };
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>
    /// Check all environment dependencies (Node.js, npm, Git).
    /// </summary>
    public async Task<List<DepCheckResult>> CheckAllDepsAsync()
    {
        var nodeTask = ProbeCommandAsync("node", GetCommandFallbackPaths("node"));
        var npmTask = ProbeCommandAsync("npm", GetCommandFallbackPaths("npm"));
        var gitTask = ProbeCommandAsync("git", GetGitFallbackPaths());
        await Task.WhenAll(nodeTask, npmTask, gitTask).ConfigureAwait(false);

        var node = await nodeTask.ConfigureAwait(false);
        var npm = await npmTask.ConfigureAwait(false);
        var git = await gitTask.ConfigureAwait(false);

        var results = new List<DepCheckResult>
        {
            new(
                name: "node",
                description: "Node.js",
                installed: node.installed,
                version: node.version,
                downloadUrl: "https://nodejs.org/en/download"
            ),
            new(
                name: "npm",
                description: "npm",
                installed: npm.installed,
                version: npm.version,
                downloadUrl: string.Empty
            ),
            new(
                name: "git",
                description: "Git",
                installed: git.installed,
                version: git.version,
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
