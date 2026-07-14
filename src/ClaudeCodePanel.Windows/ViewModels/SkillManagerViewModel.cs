using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeCodePanel.Windows.ViewModels;

/// <summary>
/// Skill Manager ViewModel — port of SkillManagerViewModel.swift.
/// Manages installed skills and marketplace browsing, installation,
/// uninstallation, and search.
/// </summary>
public partial class SkillManagerViewModel : ObservableObject
{
    // ── Services ──────────────────────────────────────────────────

    private readonly ISkillRepositoryService _skillRepo;
    private readonly SyncService _syncService;
    private readonly IConfigFileService _configFileService;

    // ── Constructor ───────────────────────────────────────────────

    public SkillManagerViewModel(
        ISkillRepositoryService? skillRepositoryService = null,
        SyncService? syncService = null,
        IConfigFileService? configFileService = null)
    {
        _skillRepo = skillRepositoryService ?? SkillRepositoryService.Instance;
        _syncService = syncService ?? SyncService.Instance;
        _configFileService = configFileService ?? ConfigFileService.Instance;
    }

    // ── SkillTab enum ─────────────────────────────────────────────

    /// <summary>
    /// Tab selection for the skill manager view.
    /// </summary>
    public enum SkillTab
    {
        /// <summary>"我的技能" — Installed skills tab</summary>
        Installed,

        /// <summary>"浏览" — Marketplace / browse tab</summary>
        Marketplace
    }

    /// <summary>
    /// Returns the Chinese display name for the given tab.
    /// </summary>
    public static string GetTabDisplayName(SkillTab tab) => tab switch
    {
        SkillTab.Installed => "我的技能",
        SkillTab.Marketplace => "浏览",
        _ => tab.ToString()
    };

    // ── Observable properties ─────────────────────────────────────

    /// <summary>List of locally installed skills (merged from disk + SyncService).</summary>
    [ObservableProperty]
    private List<SkillItem> _installedSkills = new();

    /// <summary>List of skills fetched from the GitHub marketplace.</summary>
    [ObservableProperty]
    private List<SkillItem> _marketplaceSkills = new();

    /// <summary>Current search query text.</summary>
    [ObservableProperty]
    private string _searchQuery = "";

    /// <summary>Currently selected tab.</summary>
    [ObservableProperty]
    private SkillTab _selectedTab = SkillTab.Installed;

    /// <summary>True while marketplace skills are being fetched.</summary>
    [ObservableProperty]
    private bool _isLoadingMarketplace;

    /// <summary>True while a skill install is in progress.</summary>
    [ObservableProperty]
    private bool _isInstalling;

    /// <summary>Last error message, or null if no error.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Controls visibility of the "install from source" sheet/dialog.</summary>
    [ObservableProperty]
    private bool _showInstallSheet;

    /// <summary>The source type selected in the install sheet (LocalPath or GitURL).</summary>
    [ObservableProperty]
    private SkillSource _installSource = SkillSource.LocalPath;

    /// <summary>The path or URL entered in the install sheet.</summary>
    [ObservableProperty]
    private string _installPathOrURL = "";

    /// <summary>True when the search query looks like a GitHub URL.</summary>
    [ObservableProperty]
    private bool _isGithubUrl;

    /// <summary>GitHub URL skill item for one-click install from search.</summary>
    [ObservableProperty]
    private SkillItem? _githubUrlSkill;

    // ── GitHub URL pattern ─────────────────────────────────────────

    private static readonly System.Text.RegularExpressions.Regex GitHubUrlRegex = new(
        @"^https?://github\.com/([^/]+)/([^/]+?)(\.git)?/?$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // ── Computed / filtered properties ────────────────────────────

    // ── Cached filtered collections ────────────────────────────────

    private List<SkillItem> _filteredInstalledSkills = new();
    private List<SkillItem> _filteredMarketplaceSkills = new();

    /// <summary>
    /// Installed skills filtered by the current search query (cached).
    /// Matches against name and id (case-insensitive).
    /// </summary>
    public List<SkillItem> FilteredInstalledSkills
    {
        get => _filteredInstalledSkills;
        private set
        {
            _filteredInstalledSkills = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Marketplace skills filtered by the current search query (cached).
    /// Matches against name, id, and description (case-insensitive).
    /// </summary>
    public List<SkillItem> FilteredMarketplaceSkills
    {
        get => _filteredMarketplaceSkills;
        private set
        {
            _filteredMarketplaceSkills = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Rebuilds both filtered collections from source + query.</summary>
    private void RebuildFilters()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            FilteredInstalledSkills = InstalledSkills;
            FilteredMarketplaceSkills = MarketplaceSkills;
        }
        else
        {
            FilteredInstalledSkills = InstalledSkills
                .Where(s =>
                    s.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    s.Id.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

            FilteredMarketplaceSkills = MarketplaceSkills
                .Where(s =>
                    s.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    s.Id.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    // ── Property-change cascade for cached filters ─────────────────

    partial void OnSearchQueryChanged(string value)
    {
        DetectGitHubUrl(value);
        RebuildFilters();
    }

    /// <summary>
    /// Detects if the search query is a GitHub repository URL and
    /// creates a virtual SkillItem for one-click install.
    /// </summary>
    private void DetectGitHubUrl(string query)
    {
        var match = GitHubUrlRegex.Match(query.Trim());
        if (match.Success)
        {
            var owner = match.Groups[1].Value;
            var repo = match.Groups[2].Value;
            var id = repo;

            IsGithubUrl = true;
            GithubUrlSkill = new SkillItem
            {
                Id = id,
                Name = $"{owner}/{repo}",
                Description = $"从 GitHub 安装 {owner}/{repo}",
                Source = SkillSource.GitURL,
                IsInstalled = _skillRepo.IsSkillInstalled(id)
            };

            // Also set install sheet values for the dialog
            InstallSource = SkillSource.GitURL;
            InstallPathOrURL = query.Trim();
        }
        else
        {
            IsGithubUrl = false;
            GithubUrlSkill = null;
        }
    }

    /// <summary>
    /// Installs the detected GitHub URL skill.
    /// </summary>
    public async Task InstallGithubUrlSkillAsync()
    {
        if (GithubUrlSkill == null || string.IsNullOrWhiteSpace(SearchQuery))
            return;

        var url = SearchQuery.Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = $"https://github.com/{url}";

        IsInstalling = true;
        try
        {
            await Task.Run(() =>
            {
                _skillRepo.InstallSkill(
                    id: GithubUrlSkill.Id,
                    source: SkillSource.GitURL,
                    pathOrURL: url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url : url + ".git");
            });

            GithubUrlSkill.IsInstalled = true;
            LoadInstalledSkills();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    partial void OnInstalledSkillsChanged(List<SkillItem> value)
    {
        RebuildFilters();
    }

    partial void OnMarketplaceSkillsChanged(List<SkillItem> value)
    {
        RebuildFilters();
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>
    /// Loads installed skills from the skills directory on disk,
    /// then merges with skill IDs detected by SyncService (from
    /// claude.json enabledPlugins, etc.).  Skills referenced in config
    /// but missing on disk are shown with a note.
    /// </summary>
    public void LoadInstalledSkills()
    {
        var skills = _skillRepo.ListInstalledSkills();
        var pluginStates = LoadPluginStates().States;
        var seenIDs = new HashSet<string>(skills.Select(s => s.Id));

        foreach (var skill in skills)
        {
            if (pluginStates.TryGetValue(skill.Id, out var isEnabled))
                skill.IsEnabled = isEnabled;
        }

        // Merge with skills detected via SyncService (from claude.json enabledPlugins, etc.)
        var synced = _syncService.SyncAll();
        if (synced.DidSync && synced.SkillIds.Count > 0)
        {
            foreach (var id in synced.SkillIds)
            {
                if (seenIDs.Add(id))
                {
                    // Check whether the skill directory exists on disk
                    var skillPath = Path.Combine(
                        _configFileService.SkillsDirectory,
                        id);
                    var isOnDisk = Directory.Exists(skillPath);

                    skills.Add(new SkillItem
                    {
                        Id = id,
                        Name = SharedHelpers.CapitalizeWords(id.Replace("-", " ")),
                        Description = isOnDisk
                            ? "已安装"
                            : "配置中引用 (未下载)",
                        Source = SkillSource.Marketplace,
                        IsInstalled = isOnDisk,
                        IsEnabled = pluginStates.GetValueOrDefault(id, true),
                        InstalledPath = isOnDisk ? skillPath : null
                    });
                }
            }
        }

        InstalledSkills = skills;
    }

    /// <summary>
    /// Fetches marketplace skills from the GitHub API (with caching)
    /// and populates <see cref="MarketplaceSkills"/>.
    /// </summary>
    public async Task LoadMarketplaceSkillsAsync()
    {
        IsLoadingMarketplace = true;
        try
        {
            MarketplaceSkills = await _skillRepo
                .SearchMarketplaceAsync(SearchQuery)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingMarketplace = false;
        }
    }

    /// <summary>
    /// Installs the given marketplace skill and refreshes the installed
    /// skills list on completion.
    /// </summary>
    public async Task InstallSkillAsync(SkillItem skill)
    {
        IsInstalling = true;
        try
        {
            await Task.Run(() =>
            {
                _skillRepo.InstallSkill(
                    id: skill.Id,
                    source: skill.Source,
                    pathOrURL: "");
            });

            skill.IsInstalled = true;
            LoadInstalledSkills();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Uninstalls the given skill and refreshes the installed skills
    /// list on completion.
    /// </summary>
    public async Task UninstallSkillAsync(SkillItem skill)
    {
        try
        {
            await Task.Run(() =>
            {
                _skillRepo.UninstallSkill(id: skill.Id);
            });

            skill.IsInstalled = false;
            LoadInstalledSkills();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Toggles the enabled state of the given skill.
    /// SkillItem now raises its own PropertyChanged, so no list rebind is needed.
    /// </summary>
    public void ToggleSkill(SkillItem skill)
    {
        var nextState = !skill.IsEnabled;
        try
        {
            PersistPluginState(skill.Id, nextState);
            skill.IsEnabled = nextState;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Installs a skill from a local path or Git URL (depending on the
    /// currently selected <see cref="InstallSource"/>).  Closes the
    /// install sheet and refreshes the installed list on success.
    /// </summary>
    public async Task InstallFromSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(InstallPathOrURL))
            return;

        IsInstalling = true;
        try
        {
            // Derive the skill id from the last path component of the
            // local path or Git URL (matches Swift URL.lastPathComponent
            // behaviour).
            var id = Path.GetFileName(InstallPathOrURL.TrimEnd('/', '\\'));

            await Task.Run(() =>
            {
                _skillRepo.InstallSkill(
                    id: id,
                    source: InstallSource,
                    pathOrURL: InstallPathOrURL);
            });

            ShowInstallSheet = false;
            InstallPathOrURL = "";
            LoadInstalledSkills();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    internal static string SkillIdFromPluginKey(string pluginKey)
    {
        var atIndex = pluginKey.IndexOf('@');
        return atIndex >= 0 ? pluginKey[..atIndex] : pluginKey;
    }

    internal static Dictionary<string, bool> ReadPluginStates(Dictionary<string, JsonElement>? settingsDict)
    {
        var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (settingsDict == null ||
            !settingsDict.TryGetValue("enabledPlugins", out var pluginsElement) ||
            pluginsElement.ValueKind != JsonValueKind.Object)
        {
            return states;
        }

        foreach (var plugin in pluginsElement.EnumerateObject())
        {
            var id = SkillIdFromPluginKey(plugin.Name);
            var isEnabled = plugin.Value.ValueKind != JsonValueKind.False;
            states[id] = isEnabled;
        }

        return states;
    }

    internal static void WritePluginState(
        Dictionary<string, JsonElement> settingsDict,
        string skillId,
        bool isEnabled)
    {
        var enabledPlugins = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (settingsDict.TryGetValue("enabledPlugins", out var existingPlugins) &&
            existingPlugins.ValueKind == JsonValueKind.Object)
        {
            foreach (var plugin in existingPlugins.EnumerateObject())
                enabledPlugins[plugin.Name] = plugin.Value.Clone();
        }

        var targetKey = enabledPlugins.Keys
            .FirstOrDefault(key => SkillIdFromPluginKey(key).Equals(skillId, StringComparison.OrdinalIgnoreCase))
            ?? skillId;

        enabledPlugins[targetKey] = JsonSerializer.SerializeToElement(isEnabled);
        settingsDict["enabledPlugins"] = JsonSerializer.SerializeToElement(enabledPlugins);
    }

    private PluginSettingsState LoadPluginStates()
    {
        Dictionary<string, JsonElement>? settingsDict;
        Dictionary<string, JsonElement>? localDict;
        try
        {
            settingsDict = _configFileService.ReadJSON(_configFileService.SettingsPath);
        }
        catch
        {
            settingsDict = null;
        }

        try
        {
            localDict = _configFileService.ReadJSON(_configFileService.SettingsLocalPath);
        }
        catch
        {
            localDict = null;
        }

        var mergedStates = ReadPluginStates(settingsDict);
        foreach (var kvp in ReadPluginStates(localDict))
            mergedStates[kvp.Key] = kvp.Value;

        return new PluginSettingsState(settingsDict, localDict, mergedStates);
    }

    private void PersistPluginState(string skillId, bool isEnabled)
    {
        var pluginSettings = LoadPluginStates();
        var useLocalSettings = pluginSettings.LocalDict is not null &&
            (PluginKeyExists(pluginSettings.LocalDict, skillId) ||
             PluginContainerExists(pluginSettings.LocalDict));

        var settingsPath = useLocalSettings
            ? _configFileService.SettingsLocalPath
            : _configFileService.SettingsPath;
        var dir = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var rootDict = _configFileService.ReadJSONOrEmpty(settingsPath);

        WritePluginState(rootDict, skillId, isEnabled);
        _configFileService.WriteJSON(rootDict, settingsPath);
    }

    private static bool PluginKeyExists(Dictionary<string, JsonElement> settingsDict, string skillId)
    {
        if (!settingsDict.TryGetValue("enabledPlugins", out var pluginsElement) ||
            pluginsElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return pluginsElement.EnumerateObject().Any(plugin =>
            SkillIdFromPluginKey(plugin.Name).Equals(skillId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PluginContainerExists(Dictionary<string, JsonElement> settingsDict)
    {
        return settingsDict.TryGetValue("enabledPlugins", out var pluginsElement) &&
               pluginsElement.ValueKind == JsonValueKind.Object;
    }

    private sealed record PluginSettingsState(
        Dictionary<string, JsonElement>? SettingsDict,
        Dictionary<string, JsonElement>? LocalDict,
        Dictionary<string, bool> States);

}
