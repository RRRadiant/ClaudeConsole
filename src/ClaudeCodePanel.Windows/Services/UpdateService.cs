using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Checks for updates via the GitHub Releases API.
/// Compares the latest release tag against the current version.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    public static UpdateService Instance { get; } = new();

    // ── Configure these for your GitHub repo ──────────────────────
    private const string GitHubOwner = "RRRadiant";
    private const string GitHubRepo = "ClaudeConsole";
    // ──────────────────────────────────────────────────────────────

    /// <summary>当前版本 — 从程序集版本读取，发布时只需更新 csproj 中的 Version</summary>
    private static readonly Version CurrentVersion =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
        ?? new Version(1, 0, 0);

    private static readonly string ReleasesApiUrl =
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    private UpdateService() { }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>
    /// Checks GitHub Releases for a newer version.
    /// Returns a typed result so callers can distinguish up-to-date,
    /// update available, and failed checks.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await HttpClientFactory.Create()
                .SendAsync(request)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.Failed,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}"
                };
            }

            var json = await response.Content
                .ReadAsStringAsync()
                .ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = root.GetProperty("html_url").GetString() ?? "";
            var body = root.GetProperty("body").GetString() ?? "";

            var remoteVersion = ParseVersion(tagName);
            if (remoteVersion == null || remoteVersion <= CurrentVersion)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.UpToDate
                };
            }

            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                downloadUrl = assets.EnumerateArray()
                    .Select(a => a.GetProperty("browser_download_url").GetString())
                    .FirstOrDefault(url =>
                        url != null &&
                        url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            }

            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.UpdateAvailable,
                Update = new UpdateInfo
                {
                    Version = tagName,
                    ReleaseUrl = htmlUrl,
                    ReleaseNotes = body,
                    IsNewer = true,
                    DownloadUrl = downloadUrl
                }
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] CheckForUpdateAsync failed: {ex.Message}");
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static Version? ParseVersion(string tag)
    {
        var verStr = tag.StartsWith('v') || tag.StartsWith('V')
            ? tag[1..]
            : tag;

        return Version.TryParse(verStr, out var v) ? v : null;
    }
}
