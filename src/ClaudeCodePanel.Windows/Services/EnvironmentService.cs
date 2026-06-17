using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Environment dependency detection service.
/// Ported from Claude-Win src/main/ipc/deps.ts.
/// Detects Node.js, npm, and Git installations on Windows.
/// </summary>
public sealed class EnvironmentService
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
    /// Runs a process, drains stdout/stderr, and returns (exitCode, stdout).
    /// Avoids deadlocks by reading output before WaitForExit.
    /// </summary>
    private static (int exitCode, string stdout, string stderr) RunProcess(
        string fileName, string arguments, int timeoutMs = 5000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (-1, "", "Failed to start process");

            // Read stdout/stderr asynchronously, then wait
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                return (-1, "", "Timeout");
            }

            return (process.ExitCode, stdoutTask.Result.Trim(), stderrTask.Result.Trim());
        }
        catch
        {
            return (-1, "", "");
        }
    }

    /// <summary>
    /// Finds the first path for a command using 'where', or null if not in PATH.
    /// </summary>
    private static string? FindInPath(string cmd)
    {
        var (exitCode, stdout, _) = RunProcess("where", cmd);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
        {
            // 'where' returns one path per line; take the first one
            return stdout.Split('\n')[0].Trim();
        }
        return null;
    }

    // ── Command detection ──────────────────────────────────

    /// <summary>
    /// Check if a command exists by trying 'where', then searching
    /// common installation directories with .cmd/.exe variants.
    /// </summary>
    private static bool HasCmd(string cmd)
    {
        // 1. PATH search via 'where'
        var pathFromWhere = FindInPath(cmd);
        if (!string.IsNullOrEmpty(pathFromWhere) && File.Exists(pathFromWhere))
        {
            var (exitCode, _, _) = RunProcess(pathFromWhere, "--version");
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
                    var (exitCode, _, _) = RunProcess(fullPath, "--version");
                    if (exitCode == 0) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Get the installed version of a command, or null if not found.
    /// </summary>
    private static string? GetCmdVersion(string cmd)
    {
        var foundPath = FindInPath(cmd);
        if (string.IsNullOrEmpty(foundPath) || !File.Exists(foundPath))
            return null;

        var (exitCode, stdout, _) = RunProcess(foundPath, "--version");
        return (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout)) ? stdout : null;
    }

    // ── Git-specific detection ─────────────────────────────

    private static bool HasGit()
    {
        // 1. PATH search
        if (!string.IsNullOrEmpty(FindInPath("git")))
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
                var (exitCode, _, _) = RunProcess(gitPath, "--version");
                if (exitCode == 0) return true;
            }
        }

        return false;
    }

    private static string? GetGitVersion()
    {
        var (exitCode, stdout, _) = RunProcess("git", "--version");
        return (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout)) ? stdout : null;
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>
    /// Check all environment dependencies (Node.js, npm, Git).
    /// </summary>
    public List<DepCheckResult> CheckAllDeps()
    {
        var nodeInstalled = HasCmd("node");
        var npmInstalled = HasCmd("npm");
        var gitInstalled = HasGit();

        var results = new List<DepCheckResult>
        {
            new(
                name: "node",
                description: "Node.js",
                installed: nodeInstalled,
                version: nodeInstalled ? GetCmdVersion("node") : null,
                downloadUrl: "https://nodejs.org/en/download"
            ),
            new(
                name: "npm",
                description: "npm",
                installed: npmInstalled,
                version: npmInstalled ? GetCmdVersion("npm") : null,
                downloadUrl: string.Empty
            ),
            new(
                name: "git",
                description: "Git",
                installed: gitInstalled,
                version: gitInstalled ? GetGitVersion() : null,
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
