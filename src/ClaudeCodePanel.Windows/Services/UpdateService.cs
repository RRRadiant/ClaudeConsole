using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Checks for updates via the GitHub Releases API.
/// Compares the latest release tag against the current version.
/// </summary>
public sealed class UpdateService
{
    public static UpdateService Instance { get; } = new();

    // ── Configure these for your GitHub repo ──────────────────────
    private const string GitHubOwner = "RRRadiant";
    private const string GitHubRepo = "ClaudeConsole";
    // ──────────────────────────────────────────────────────────────

    /// <summary>当前版本 — 发布新版本时记得同步更新 csproj 中的 Version</summary>
    private static readonly Version CurrentVersion = new Version(1, 1, 0);

    private static readonly string ReleasesApiUrl =
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    private readonly HttpClient _httpClient;

    private UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ClaudeConsole", "1.0"));
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>
    /// Checks GitHub Releases for a newer version.
    /// Returns null if already up-to-date, offline, or on error.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            using var response = await _httpClient
                .GetAsync(ReleasesApiUrl)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

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
                return null;

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

            return new UpdateInfo
            {
                Version = tagName,
                ReleaseUrl = htmlUrl,
                ReleaseNotes = body,
                IsNewer = true,
                DownloadUrl = downloadUrl
            };
        }
        catch
        {
            return null;
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
