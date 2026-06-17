using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        RebuildFilters();
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
        var skills = SkillRepositoryService.Instance.ListInstalledSkills();
        var seenIDs = new HashSet<string>(skills.Select(s => s.Id));

        // Merge with skills detected via SyncService (from claude.json enabledPlugins, etc.)
        var synced = SyncService.Instance.SyncAll();
        if (synced.DidSync && synced.SkillIds.Count > 0)
        {
            foreach (var id in synced.SkillIds)
            {
                if (!seenIDs.Contains(id))
                {
                    seenIDs.Add(id);

                    // Check whether the skill directory exists on disk
                    var skillPath = Path.Combine(
                        ConfigFileService.Instance.SkillsDirectory,
                        id);
                    var isOnDisk = Directory.Exists(skillPath);

                    skills.Add(new SkillItem
                    {
                        Id = id,
                        Name = CapitalizeWords(id.Replace("-", " ")),
                        Description = isOnDisk
                            ? "已安装"
                            : "配置中引用 (未下载)",
                        Source = SkillSource.Marketplace,
                        IsInstalled = isOnDisk,
                        IsEnabled = true,
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
            MarketplaceSkills = await SkillRepositoryService.Instance
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
                SkillRepositoryService.Instance.InstallSkill(
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
                SkillRepositoryService.Instance.UninstallSkill(id: skill.Id);
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
        skill.IsEnabled = !skill.IsEnabled;
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
                SkillRepositoryService.Instance.InstallSkill(
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

    /// <summary>
    /// Re-queries the marketplace (convenience wrapper around
    /// <see cref="LoadMarketplaceSkillsAsync"/>).
    /// </summary>
    public async Task SearchMarketplaceAsync()
    {
        await LoadMarketplaceSkillsAsync();
    }

    // ── Private helpers ───────────────────────────────────────────

    /// <summary>
    /// Converts a dash-separated id into a title-cased display name.
    /// E.g. "my-awesome-skill" becomes "My Awesome Skill".
    /// </summary>
    private static string CapitalizeWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            input.ToLowerInvariant());
    }
}
