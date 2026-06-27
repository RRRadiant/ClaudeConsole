using System;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Centralized constants for timeouts, common paths, and registry URLs.
/// </summary>
internal static class AppConstants
{
    // ── Timeouts (milliseconds) ──────────────────────────────

    /// <summary>Quick process check (e.g. --version).</summary>
    public const int TimeoutQuick = 5_000;

    /// <summary>MCP / API connection test.</summary>
    public const int TimeoutConnection = 8_000;

    /// <summary>GitHub API version check.</summary>
    public const int TimeoutVersionCheck = 10_000;

    /// <summary>npm install timeout.</summary>
    public const int TimeoutInstall = 180_000;

    /// <summary>npm uninstall timeout.</summary>
    public const int TimeoutUninstall = 60_000;

    /// <summary>git clone timeout for marketplace installs.</summary>
    public const int TimeoutGitClone = 180_000; // 3 minutes

    // ── Common paths ─────────────────────────────────────────

    public static readonly string[] CommonNodePaths =
    {
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "nodejs"),
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".npm-global", "bin"),
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm"),
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "nodejs"),
        @"C:\Program Files\nodejs",
        @"C:\Program Files (x86)\nodejs",
    };

    public static readonly string[] CommonGitPaths =
    {
        @"C:\Git\bin\git.exe",
        @"C:\Git\cmd\git.exe",
        @"C:\Program Files\Git\bin\git.exe",
        @"C:\Program Files (x86)\Git\bin\git.exe",
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "Local", "Programs", "Git", "bin", "git.exe"),
    };

    // ── Registry ─────────────────────────────────────────────

    public const string NpmMirror = "--registry=https://registry.npmmirror.com";
    public const string NpmDefaultRegistry = "";

    // ── GitHub ───────────────────────────────────────────────

    public const string GitHubOwner = "RRRadiant";
    public const string GitHubRepo = "ClaudeConsole";
}
