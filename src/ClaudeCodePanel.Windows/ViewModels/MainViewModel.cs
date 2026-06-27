using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeCodePanel.Windows.ViewModels;

/// <summary>
/// Maps to the Swift AppPanel enum in SidebarView.swift.
/// Each value corresponds to a top-level panel in the navigation sidebar.
/// </summary>
public enum MainPanelType
{
    Dashboard,
    ApiConfig,
    ConfigEditor,
    McpManager,
    SkillManager,
    Installer,
    EnvCheck
}

/// <summary>
/// A sidebar navigation item that mirrors SidebarItem in the Swift project.
/// Holds vector icon path data, the Chinese display title, and the panel type.
/// </summary>
/// <param name="IconGlyph">Segoe MDL2 Assets font glyph code (legacy).</param>
/// <param name="IconPathData">SVG path data for modern vector icon (Feather/Ionicon style).</param>
/// <param name="Title">Chinese display title matching the macOS AppPanel.title.</param>
/// <param name="PanelType">The <see cref="MainPanelType"/> this item navigates to.</param>
public record SidebarItem(string IconGlyph, string IconPathData, string Title, string Description, MainPanelType PanelType);

/// <summary>
/// Main navigation ViewModel — replaces ContentView's @State selectedPanel in Swift.
/// Owns the sidebar item list, the currently selected panel, and the resolved
/// ViewModel instance for the content area.
///
/// Sidebar buttons bind their Command to NavigateCommand and pass the
/// corresponding <see cref="MainPanelType"/> as CommandParameter.
/// The content area binds its Content to <see cref="SelectedPanelViewModel"/>.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUpdateService _updateService;

    // ── Cached ViewModel references ─────────────────────────────────

    private readonly Dictionary<MainPanelType, object> _viewModelCache = new();

    // ── Observable properties ──────────────────────────────────────

    /// <summary>
    /// The ViewModel instance for the currently selected panel.
    /// Bound to the ContentControl in the main window's content area.
    /// </summary>
    [ObservableProperty]
    private object? _selectedPanelViewModel;

    /// <summary>
    /// The currently selected panel type. Defaults to <see cref="MainPanelType.Dashboard"/>.
    /// Sidebar item highlight state binds to this property.
    /// </summary>
    [ObservableProperty]
    private MainPanelType _selectedPanel = MainPanelType.Dashboard;

    // ── Update notification ────────────────────────────────────────

    /// <summary>True when a newer version is available on GitHub.</summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    /// <summary>The latest version string (e.g. "v1.1.0").</summary>
    [ObservableProperty]
    private string _updateVersion = "";

    /// <summary>Release notes for the available update.</summary>
    [ObservableProperty]
    private string _updateReleaseNotes = "";

    /// <summary>URL to open when the user clicks the update banner.</summary>
    [ObservableProperty]
    private string _updateUrl = "";

    /// <summary>Current app version string (e.g. "v1.0.0") — read from assembly.</summary>
    [ObservableProperty]
    private string _currentVersionText = 
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    /// <summary>Update status shown in the sidebar: 检查中… / 已是最新 / 发现新版本.</summary>
    [ObservableProperty]
    private string _updateStatusText = "检查中…";

    // ── Sidebar items ──────────────────────────────────────────────

    /// <summary>
    /// Static list of sidebar navigation items.
    /// Titles and icons match the macOS AppPanel enum (SidebarView.swift).
    /// Icon glyphs use Segoe MDL2 Assets font codes.
    /// </summary>
    public List<SidebarItem> SidebarItems { get; private set; } = BuildSidebarItems();

    /// <summary>Refreshes sidebar labels from LocalizationService after language change.</summary>
    public void RefreshSidebarLabels()
    {
        SidebarItems = BuildSidebarItems();
        OnPropertyChanged(nameof(SidebarItems));
    }

    private static List<SidebarItem> BuildSidebarItems()
    {
        var loc = LocalizationService.Instance;
        return new()
        {
            new SidebarItem("", "M3 3h4v4H3z M17 3h4v4h-4z M3 17h4v4H3z M17 17h4v4h-4z",
                loc["Sidebar.Dashboard"],    loc["Sidebar.DashboardDesc"],    MainPanelType.Dashboard),
            new SidebarItem("", "M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0z M12 11v4 M8 15h8",
                loc["Sidebar.ApiConfig"],    loc["Sidebar.ApiConfigDesc"],    MainPanelType.ApiConfig),
            new SidebarItem("", "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6",
                loc["Sidebar.ConfigEditor"], loc["Sidebar.ConfigEditorDesc"], MainPanelType.ConfigEditor),
            new SidebarItem("", "M4 6h16M4 12h16M4 18h16 M8 6v12 M16 6v12",
                loc["Sidebar.McpManager"],   loc["Sidebar.McpManagerDesc"],   MainPanelType.McpManager),
            new SidebarItem("", "M20 12a8 8 0 1 1-16 0 8 8 0 0 1 16 0z M12 8v8 M8 12h8",
                loc["Sidebar.SkillManager"], loc["Sidebar.SkillManagerDesc"], MainPanelType.SkillManager),
            new SidebarItem("", "M12 3v12 M5 12l7 7 7-7 M4 20h16",
                loc["Sidebar.Installer"],    loc["Sidebar.InstallerDesc"],    MainPanelType.Installer),
            new SidebarItem("", "M9 5H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V5 M13 3v4H9V3 M9 12l2 2 4-4",
                loc["Sidebar.EnvCheck"],     loc["Sidebar.EnvCheckDesc"],     MainPanelType.EnvCheck),
        };
    }

    // ── Constructor ────────────────────────────────────────────────

    public MainViewModel(IServiceProvider serviceProvider, IUpdateService? updateService = null)
    {
        _serviceProvider = serviceProvider;
        _updateService = updateService ?? UpdateService.Instance;

        // Start on the dashboard panel.
        Navigate(MainPanelType.Dashboard);

        // Fire-and-forget update check (non-blocking, errors silently swallowed)
        CheckForUpdateAsync().SafeFireAndForget("MainViewModel.CheckForUpdate");
    }

    // ── Update check ───────────────────────────────────────────────

    /// <summary>
    /// Checks GitHub Releases for a newer version.  Updates the
    /// <see cref="IsUpdateAvailable"/> properties on success.
    /// Called once on app startup.
    /// </summary>
    private async Task CheckForUpdateAsync()
    {
        await DoCheckUpdateAsync();
    }

    /// <summary>
    /// Manual update check — bound to the sidebar footer button.
    /// </summary>
    [RelayCommand]
    private async Task DoCheckUpdateAsync()
    {
        UpdateStatusText = "检查中…";
        IsUpdateAvailable = false;

        var update = await _updateService.CheckForUpdateAsync();
        if (update is not { IsNewer: true })
        {
            UpdateStatusText = "已是最新";
            return;
        }

        UpdateVersion = update.Version;
        UpdateReleaseNotes = update.ReleaseNotes;
        UpdateUrl = update.ReleaseUrl;
        IsUpdateAvailable = true;
        UpdateStatusText = $"发现 {update.Version}";
    }

    /// <summary>
    /// Opens the GitHub release page in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenUpdateUrl()
    {
        if (string.IsNullOrEmpty(UpdateUrl))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = UpdateUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] OpenUpdateUrl failed: {ex.Message}");
        }
    }

    // ── Navigation ─────────────────────────────────────────────────

    /// <summary>
    /// Navigate to the given panel by resolving its ViewModel from DI
    /// and updating both <see cref="SelectedPanel"/> and
    /// <see cref="SelectedPanelViewModel"/>.
    ///
    /// The generated <c>NavigateCommand</c> (ICommand) is bound to each
    /// sidebar button, with a <see cref="MainPanelType"/> value passed as
    /// CommandParameter.
    /// </summary>
    /// <param name="panel">The target panel to navigate to.</param>
    [RelayCommand]
    public void Navigate(MainPanelType panel)
    {
        SelectedPanel = panel;

        // Resolve from cache or DI (ViewModels are singletons, so caching avoids container lookups)
        if (!_viewModelCache.TryGetValue(panel, out var vm))
        {
            vm = panel switch
            {
                MainPanelType.Dashboard    => _serviceProvider.GetRequiredService<DashboardViewModel>(),
                MainPanelType.ApiConfig    => _serviceProvider.GetRequiredService<APIConfigViewModel>(),
                MainPanelType.ConfigEditor => _serviceProvider.GetRequiredService<ConfigEditorViewModel>(),
                MainPanelType.McpManager   => _serviceProvider.GetRequiredService<MCPManagerViewModel>(),
                MainPanelType.SkillManager => _serviceProvider.GetRequiredService<SkillManagerViewModel>(),
                MainPanelType.Installer    => _serviceProvider.GetRequiredService<InstallerViewModel>(),
                MainPanelType.EnvCheck     => _serviceProvider.GetRequiredService<EnvCheckViewModel>(),
                _ => throw new ArgumentOutOfRangeException(nameof(panel), panel, null)
            };
            _viewModelCache[panel] = vm;
        }

        SelectedPanelViewModel = vm;
    }
}
