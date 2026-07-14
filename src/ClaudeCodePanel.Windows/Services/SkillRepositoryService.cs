using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;
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

public sealed class SkillRepositoryService : ISkillRepositoryService
{
    public static SkillRepositoryService Instance { get; } = new();

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(3600);
    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = true
    };

    private SkillRepositoryService() { }

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
    /// Fetches the list of marketplace skills from the GitHub API,
    /// then enriches each with name/description from SKILL.md.
    /// Results are cached for 3600 seconds.
    /// </summary>
    public async Task<List<SkillItem>> FetchMarketplaceSkillsAsync()
    {
        var cache = LoadCache();
        if (cache != null && (DateTime.UtcNow - cache.Timestamp) < CacheDuration)
        {
            return cache.Skills.Select(e => e.ToSkillItem()).ToList();
        }

        try
        {
            // 1. Fetch directory listing from GitHub API (with mirror fallback)
            var apiUrls = new[]
            {
                "https://api.github.com/repos/anthropic/claude-code/contents/skills",
                "https://ghproxy.com/https://api.github.com/repos/anthropic/claude-code/contents/skills",
            };

            List<GitHubContentItem>? items = null;
            foreach (var apiUrl in apiUrls)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                    request.Headers.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                    request.Headers.UserAgent.Add(
                        new ProductInfoHeaderValue("ClaudeConsole", "1.0"));

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using var response = await HttpClientFactory.Create().SendAsync(request, cts.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                        items = JsonSerializer.Deserialize<List<GitHubContentItem>>(json);
                        if (items != null) break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SkillRepositoryService] GitHub API mirror {apiUrl} failed: {ex.Message}");
                }
            }

            if (items == null)
                return GetBuiltInSkillList();

            var dirs = items.Where(i => i.Type == "dir").ToList();

            // 2. Fetch SKILL.md metadata in parallel (raw CDN + mirrors)
            var skills = new List<SkillItem>();
            var semaphore = new SemaphoreSlim(6);

            var tasks = dirs.Select(async dir =>
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var skill = await FetchSkillMetadataAsync(dir.Name).ConfigureAwait(false);
                    lock (skills) { skills.Add(skill); }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SkillRepositoryService] FetchSkillMetadataAsync for '{dir.Name}' failed: {ex.Message}");
                    var skill = new SkillItem
                    {
                        Id = dir.Name,
                        Name = SharedHelpers.CapitalizeWords(dir.Name.Replace("-", " ")),
                        Description = "",
                        Source = SkillSource.Marketplace,
                        IsInstalled = IsSkillInstalled(dir.Name)
                    };
                    lock (skills) { skills.Add(skill); }
                }
                finally { semaphore.Release(); }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            skills.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            SaveCache(skills);
            return skills;
        }
        catch (SkillRepoException) { throw; }
        catch
        {
            return GetBuiltInSkillList();
        }
    }

    /// <summary>
    /// Built-in skill list used as fallback when GitHub is unreachable.
    /// Update this list periodically to keep it in sync with the official repo.
    /// </summary>
    private List<SkillItem> GetBuiltInSkillList()
    {
        var ids = new[]
        {
            "agents-for", "caveman", "design-an-interface", "diagnose",
            "edit-article", "explore", "git-guardrails-claude-code",
            "grill-me", "grill-with-docs", "handoff",
            "improve-codebase-architecture", "init", "install-capability",
            "karpathy-guidelines", "migrate-to-shoehorn", "obsidian-vault",
            "prototype", "qa", "request-refactor-plan", "research",
            "review", "scaffold-exercises", "security-review",
            "setup-matt-pocock-skills", "setup-pre-commit", "tdd",
            "teach", "test", "to-issues", "to-prd",
            "triage", "ubiquitous-language", "write-a-skill",
            "writing-beats", "writing-fragments", "writing-shape", "zoom-out"
        };

        return ids.Select(id => new SkillItem
        {
            Id = id,
            Name = SharedHelpers.CapitalizeWords(id.Replace("-", " ")),
            Description = "Claude Code 官方技能",
            Source = SkillSource.Marketplace,
            IsInstalled = IsSkillInstalled(id)
        }).ToList();
    }

    /// <summary>
    /// Fetches SKILL.md from raw.githubusercontent.com (with mirror fallback)
    /// and extracts YAML frontmatter fields (name, description).
    /// </summary>
    private async Task<SkillItem> FetchSkillMetadataAsync(string skillId)
    {
        var rawPaths = new[]
        {
            $"https://raw.githubusercontent.com/anthropic/claude-code/main/skills/{skillId}/SKILL.md",
            $"https://ghproxy.com/https://raw.githubusercontent.com/anthropic/claude-code/main/skills/{skillId}/SKILL.md",
            $"https://raw.fastgit.org/anthropic/claude-code/main/skills/{skillId}/SKILL.md",
        };

        string? content = null;
        foreach (var url in rawPaths)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                content = await HttpClientFactory.Create().GetStringAsync(url, cts.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(content))
                    break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkillRepositoryService] SKILL.md fetch mirror {url} failed: {ex.Message}");
            }
        }

        if (content == null)
        {
            // All mirrors failed — use directory name
            return new SkillItem
            {
                Id = skillId,
                Name = SharedHelpers.CapitalizeWords(skillId.Replace("-", " ")),
                Description = "",
                Source = SkillSource.Marketplace,
                IsInstalled = IsSkillInstalled(skillId)
            };
        }

        var (name, description) = ParseSkillMarkdownFrontmatter(content);

        return new SkillItem
        {
            Id = skillId,
            Name = !string.IsNullOrWhiteSpace(name)
                ? name
                : SharedHelpers.CapitalizeWords(skillId.Replace("-", " ")),
            Description = description ?? "",
            Source = SkillSource.Marketplace,
            IsInstalled = IsSkillInstalled(skillId)
        };
    }

    /// <summary>
    /// Extracts YAML frontmatter (--- delimited block) from a SKILL.md
    /// and reads the "name" and "description" fields.
    /// </summary>
    internal static (string? name, string? description) ParseSkillMarkdownFrontmatter(string markdown)
    {
        string? name = null;
        string? description = null;

        // Look for YAML frontmatter between --- delimiters
        var lines = markdown.Split('\n');
        if (lines.Length < 3 || lines[0].Trim() != "---")
            return (null, null);

        var endIndex = Array.FindIndex(lines, 1, l => l.Trim() == "---");
        if (endIndex < 0)
            return (null, null);

        for (int i = 1; i < endIndex; i++)
        {
            var line = lines[i];
            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;

            var key = line[..colonIdx].Trim().ToLowerInvariant();
            var value = line[(colonIdx + 1)..].Trim().Trim('"', '\'');

            if (key == "name" && name == null)
                name = value;
            else if (key == "description" && description == null)
                description = value;
        }

        return (name, description);
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
        var normalizedId = NormalizeSkillId(id);
        var targetDir = GetSafeSkillDirectory(Config.SkillsDirectory, normalizedId);

        switch (source)
        {
            case SkillSource.LocalPath:
                InstallFromLocalPath(normalizedId, pathOrURL, targetDir);
                break;

            case SkillSource.GitURL:
                InstallFromGitURL(normalizedId, pathOrURL, targetDir);
                break;

            case SkillSource.Marketplace:
                InstallFromMarketplace(normalizedId, targetDir);
                break;

            default:
                throw new SkillRepoException(
                    $"Unknown skill source: {source}");
        }
    }

    private static void InstallFromLocalPath(string id, string sourcePath, string targetDir)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new SkillRepoException(
                $"Source directory does not exist: {sourcePath}");
        }

        Config.EnsureDirectoryExists(Path.GetDirectoryName(targetDir)!);

        // Remove existing installation if present
        DeleteDirectoryIfExists(targetDir);

        CopyDirectoryRecursive(sourcePath, targetDir);
    }

    private static void InstallFromGitURL(string id, string url, string targetDir)
    {
        Config.EnsureDirectoryExists(Path.GetDirectoryName(targetDir)!);

        // Remove existing installation if present
        DeleteDirectoryIfExists(targetDir);

        try
        {
            InstallFromGitURLAsync(url, targetDir).GetAwaiter().GetResult();
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
    }

    private static async Task InstallFromGitURLAsync(string url, string targetDir)
    {
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
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3));
            var exitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(exitTask, timeoutTask).ConfigureAwait(false);

            if (completed == timeoutTask)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { SharedHelpers.SafeLog("SkillRepositoryService.InstallFromGitURLAsync.Kill", ex); }
                throw new SkillRepoException("git clone timed out after 3 minutes");
            }

            await exitTask.ConfigureAwait(false);
            await stdoutTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var error = (await stderrTask.ConfigureAwait(false)).Trim();
                throw new SkillRepoException(
                    string.IsNullOrEmpty(error)
                        ? $"git clone failed with exit code {process.ExitCode}"
                        : $"git clone failed: {error}");
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void InstallFromMarketplace(string id, string targetDir)
    {
        const string officialRepo = "https://github.com/anthropic/claude-code.git";
        var tempDir = Path.Combine(Path.GetTempPath(), $"claude-code-skill-{Guid.NewGuid()}");

        try
        {
            Config.EnsureDirectoryExists(Path.GetDirectoryName(targetDir)!);

            InstallFromMarketplaceAsync(id, targetDir, officialRepo, tempDir).GetAwaiter().GetResult();
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkillRepositoryService] Temp directory cleanup failed: {ex.Message}");
            }
        }
    }

    private static async Task InstallFromMarketplaceAsync(string id, string targetDir, string officialRepo, string tempDir)
    {
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

        try
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3));
            var exitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(exitTask, timeoutTask);

            if (completed == timeoutTask)
            {
                try { process.Kill(); } catch (Exception ex) { SharedHelpers.SafeLog("SkillRepositoryService.InstallFromMarketplaceAsync.Kill", ex); }
                throw new SkillRepoException("git clone timed out after 3 minutes");
            }

            await exitTask;
            await stdoutTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var error = (await stderrTask.ConfigureAwait(false)).Trim();
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

            DeleteDirectoryIfExists(targetDir);

            CopyDirectoryRecursive(skillSourceDir, targetDir);
        }
        finally
        {
            process.Dispose();
        }
    }

    // ── Uninstall ─────────────────────────────────────────

    /// <summary>
    /// Uninstalls a skill by removing its directory from the skills folder.
    /// </summary>
    public void UninstallSkill(string id)
    {
        var normalizedId = NormalizeSkillId(id);
        var targetDir = GetSafeSkillDirectory(Config.SkillsDirectory, normalizedId);
        DeleteDirectoryIfExists(targetDir);
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
                    Name = SharedHelpers.CapitalizeWords(id.Replace("-", " ")),
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
        var path = GetSafeSkillDirectory(Config.SkillsDirectory, id);
        return Directory.Exists(path);
    }

    // ── Helpers ───────────────────────────────────────────

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
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkillRepositoryService] LoadCache failed: {ex.Message}");
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

            var json = JsonSerializer.Serialize(cache, CacheJsonOptions);

            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkillRepositoryService] SaveCache failed: {ex.Message}");
        }
    }

    internal static string NormalizeSkillId(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new SkillRepoException("Skill id cannot be empty.");

        var trimmed = rawValue.Trim().TrimEnd('/', '\\');
        var candidate = Path.GetFileName(trimmed);
        if (candidate.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            candidate = Path.GetFileNameWithoutExtension(candidate);

        if (string.IsNullOrWhiteSpace(candidate) || candidate is "." or "..")
            throw new SkillRepoException($"Invalid skill id: {rawValue}");

        if (candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new SkillRepoException($"Invalid skill id: {rawValue}");

        return candidate;
    }

    internal static string GetSafeSkillDirectory(string skillsDirectory, string rawSkillId)
    {
        var root = Path.GetFullPath(skillsDirectory);
        var skillId = NormalizeSkillId(rawSkillId);
        var fullPath = Path.GetFullPath(Path.Combine(root, skillId));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new SkillRepoException($"Resolved skill path escaped the skills directory: {rawSkillId}");

        return fullPath;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
