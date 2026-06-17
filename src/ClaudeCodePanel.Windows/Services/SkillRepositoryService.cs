using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Services;

// ─── GitHub API response type ────────────────────────────

internal sealed class GitHubContentItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Type { get; set; } = "";
}

// ─── Cache types ─────────────────────────────────────────

internal sealed class SkillCacheRecord
{
    public DateTime Timestamp { get; set; }
    public List<CachedSkillEntry> Skills { get; set; } = new();
}

internal sealed class CachedSkillEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Source { get; set; } = "Marketplace";
    public int? StarCount { get; set; }
    public string? Category { get; set; }

    public SkillItem ToSkillItem()
    {
        return new SkillItem
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Source = Enum.TryParse<SkillSource>(Source, out var parsed)
                ? parsed : SkillSource.Marketplace,
            IsInstalled = SkillRepositoryService.Instance.IsSkillInstalled(Id),
            StarCount = StarCount,
            Category = Category
        };
    }

    public static CachedSkillEntry FromSkillItem(SkillItem item)
    {
        return new CachedSkillEntry
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Source = item.Source.ToString(),
            StarCount = item.StarCount,
            Category = item.Category
        };
    }
}

// ─── Error type ──────────────────────────────────────────

public sealed class SkillRepoException : Exception
{
    public SkillRepoException(string message) : base(message) { }
    public SkillRepoException(string message, Exception inner) : base(message, inner) { }
}

// ─── Service ─────────────────────────────────────────────

public sealed class SkillRepositoryService
{
    public static SkillRepositoryService Instance { get; } = new();

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(3600);

    private readonly HttpClient _httpClient;

    private SkillRepositoryService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    private static ConfigFileService Config => ConfigFileService.Instance;

    // ── Cache path ────────────────────────────────────────

    private static string CacheFilePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(localAppData, "ClaudeCodePanel");
            return Path.Combine(dir, "skillcache.json");
        }
    }

    // ── Fetch marketplace ─────────────────────────────────

    /// <summary>
    /// Fetches the list of marketplace skills from the GitHub API.
    /// Results are cached in <c>%LOCALAPPDATA%/ClaudeCodePanel/skillcache.json</c>
    /// for 3600 seconds.
    /// </summary>
    public async Task<List<SkillItem>> FetchMarketplaceSkillsAsync()
    {
        // Return cached results if fresh
        var cache = LoadCache();
        if (cache != null && (DateTime.UtcNow - cache.Timestamp) < CacheDuration)
        {
            return cache.Skills.Select(e => e.ToSkillItem()).ToList();
        }

        // Fetch fresh data from GitHub
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/repos/anthropic/claude-code/contents/skills");

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            request.Headers.UserAgent.Add(
                new ProductInfoHeaderValue("ClaudeCodePanel", "1.0"));

            using var response = await _httpClient
                .SendAsync(request)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new SkillRepoException(
                    $"Failed to fetch skill repository data (HTTP {(int)response.StatusCode})");
            }

            var json = await response.Content
                .ReadAsStringAsync()
                .ConfigureAwait(false);

            var items = JsonSerializer.Deserialize<List<GitHubContentItem>>(json);
            if (items == null)
            {
                throw new SkillRepoException(
                    "Failed to parse skill repository data");
            }

            var skills = items
                .Where(i => i.Type == "dir")
                .Select(i => new SkillItem
                {
                    Id = i.Name,
                    Name = CapitalizeWords(i.Name.Replace("-", " ")),
                    Description = "",
                    Source = SkillSource.Marketplace,
                    IsInstalled = IsSkillInstalled(i.Name)
                })
                .ToList();

            SaveCache(skills);
            return skills;
        }
        catch (SkillRepoException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SkillRepoException(
                "Failed to fetch skill repository data", ex);
        }
    }

    // ── Search marketplace ────────────────────────────────

    /// <summary>
    /// Searches the marketplace for skills matching the given query.
    /// Matching is case-insensitive against name, id, and description.
    /// An empty or whitespace query returns all marketplace skills.
    /// </summary>
    public async Task<List<SkillItem>> SearchMarketplaceAsync(string query)
    {
        var all = await FetchMarketplaceSkillsAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query))
            return all;

        return all
            .Where(s =>
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ── Install ───────────────────────────────────────────

    /// <summary>
    /// Installs a skill from the given source.
    /// - <see cref="SkillSource.LocalPath"/>: copies the directory at
    ///   <paramref name="pathOrURL"/> into the skills directory.
    /// - <see cref="SkillSource.GitURL"/>: runs <c>git clone</c> to clone
    ///   the repository into the skills directory.
    /// - <see cref="SkillSource.Marketplace"/>: shallow-clones the official
    ///   Claude Code skills repository and copies the matching skill directory.
    /// </summary>
    public void InstallSkill(string id, SkillSource source, string pathOrURL)
    {
        var targetDir = Path.Combine(Config.SkillsDirectory, id);

        switch (source)
        {
            case SkillSource.LocalPath:
                InstallFromLocalPath(id, pathOrURL, targetDir);
                break;

            case SkillSource.GitURL:
                InstallFromGitURL(id, pathOrURL, targetDir);
                break;

            case SkillSource.Marketplace:
                InstallFromMarketplace(id, targetDir);
                break;

            default:
                throw new SkillRepoException(
                    $"Unknown skill source: {source}");
        }
    }

    private void InstallFromLocalPath(string id, string sourcePath, string targetDir)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new SkillRepoException(
                $"Source directory does not exist: {sourcePath}");
        }

        Config.EnsureDirectoryExists(Path.GetDirectoryName(targetDir)!);

        // Remove existing installation if present
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);

        CopyDirectoryRecursive(sourcePath, targetDir);
    }

    private void InstallFromGitURL(string id, string url, string targetDir)
    {
        Config.EnsureDirectoryExists(Path.GetDirectoryName(targetDir)!);

        // Remove existing installation if present
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("clone");
        process.StartInfo.ArgumentList.Add(url);
        process.StartInfo.ArgumentList.Add(targetDir);

        try
        {
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd().Trim();
                throw new SkillRepoException(
                    string.IsNullOrEmpty(error)
                        ? $"git clone failed with exit code {process.ExitCode}"
                        : $"git clone failed: {error}");
            }
        }
        catch (SkillRepoException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SkillRepoException(
                $"Failed to run git clone: {ex.Message}", ex);
        }
        finally
        {
            process.Dispose();
        }
    }

    private void InstallFromMarketplace(string id, string targetDir)
    {
        const string officialRepo = "https://github.com/anthropic/claude-code.git";
        var tempDir = Path.Combine(Path.GetTempPath(), $"claude-code-skill-{Guid.NewGuid()}");

        try
        {
            Config.EnsureDirectoryExists(Path.GetDirectoryName(targetDir)!);

            // Shallow clone with blobless filter for speed
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("clone");
            process.StartInfo.ArgumentList.Add("--depth=1");
            process.StartInfo.ArgumentList.Add("--filter=blob:none");
            process.StartInfo.ArgumentList.Add(officialRepo);
            process.StartInfo.ArgumentList.Add(tempDir);

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd().Trim();
                throw new SkillRepoException(
                    string.IsNullOrEmpty(error)
                        ? $"git clone failed with exit code {process.ExitCode}"
                        : $"git clone failed: {error}");
            }

            var skillSourceDir = Path.Combine(tempDir, "skills", id);
            if (!Directory.Exists(skillSourceDir))
            {
                throw new SkillRepoException(
                    $"Skill '{id}' not found in the official Claude Code repository");
            }

            // Remove existing installation if present
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);

            CopyDirectoryRecursive(skillSourceDir, targetDir);
        }
        catch (SkillRepoException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SkillRepoException(
                $"Failed to install marketplace skill '{id}': {ex.Message}", ex);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    // ── Uninstall ─────────────────────────────────────────

    /// <summary>
    /// Uninstalls a skill by removing its directory from the skills folder.
    /// </summary>
    public void UninstallSkill(string id)
    {
        var targetDir = Path.Combine(Config.SkillsDirectory, id);

        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);
    }

    // ── List installed ────────────────────────────────────

    /// <summary>
    /// Lists all installed skills by enumerating subdirectories of
    /// <c>~/.claude/skills/</c>.
    /// </summary>
    public List<SkillItem> ListInstalledSkills()
    {
        var skillsDir = Config.SkillsDirectory;

        if (!Directory.Exists(skillsDir))
            return new List<SkillItem>();

        return Directory
            .EnumerateDirectories(skillsDir)
            .Select(dir =>
            {
                var dirInfo = new DirectoryInfo(dir);
                var id = dirInfo.Name;

                return new SkillItem
                {
                    Id = id,
                    Name = CapitalizeWords(id.Replace("-", " ")),
                    Description = "",
                    Source = SkillSource.Marketplace,
                    IsInstalled = true,
                    InstalledPath = dirInfo.FullName
                };
            })
            .ToList();
    }

    // ── Check installed ───────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if the skill with the given <paramref name="id"/>
    /// exists as a subdirectory of the skills folder.
    /// </summary>
    public bool IsSkillInstalled(string id)
    {
        var path = Path.Combine(Config.SkillsDirectory, id);
        return Directory.Exists(path);
    }

    // ── Helpers ───────────────────────────────────────────

    private static string CapitalizeWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, dest);
        }
    }

    // ── Cache persistence ─────────────────────────────────

    private static SkillCacheRecord? LoadCache()
    {
        try
        {
            var path = CacheFilePath;
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var cache = JsonSerializer.Deserialize<SkillCacheRecord>(json);
            return cache;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCache(List<SkillItem> skills)
    {
        try
        {
            var cache = new SkillCacheRecord
            {
                Timestamp = DateTime.UtcNow,
                Skills = skills.Select(CachedSkillEntry.FromSkillItem).ToList()
            };

            var path = CacheFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }
        catch
        {
            // Cache write failures are non-fatal; silently ignored
        }
    }
}
