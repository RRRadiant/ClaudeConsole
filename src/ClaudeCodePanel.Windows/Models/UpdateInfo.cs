namespace ClaudeCodePanel.Windows.Models;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

/// <summary>
/// Represents a software update discovered via GitHub Releases.
/// </summary>
public sealed class UpdateInfo
{
    /// <summary>The latest version tag from GitHub (e.g. "v1.1.0").</summary>
    public string Version { get; init; } = "";

    /// <summary>URL to the GitHub release page.</summary>
    public string ReleaseUrl { get; init; } = "";

    /// <summary>Release notes / changelog in Markdown.</summary>
    public string ReleaseNotes { get; init; } = "";

    /// <summary>True when the remote version is newer than the current version.</summary>
    public bool IsNewer { get; init; }

    /// <summary>Download URL for the portable .exe asset, if any.</summary>
    public string? DownloadUrl { get; init; }
}

public sealed class UpdateCheckResult
{
    public UpdateCheckStatus Status { get; init; }
    public UpdateInfo? Update { get; init; }
    public string? ErrorMessage { get; init; }
}
