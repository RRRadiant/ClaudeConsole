using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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
/// Holds the icon glyph (Segoe MDL2 Assets code), the Chinese display title,
/// and the panel type it navigates to.
/// </summary>
/// <param name="IconGlyph">Segoe MDL2 Assets font glyph code (e.g. "").</param>
/// <param name="Title">Chinese display title matching the macOS AppPanel.title.</param>
/// <param name="PanelType">The <see cref="MainPanelType"/> this item navigates to.</param>
public record SidebarItem(string IconGlyph, string Title, string Description, MainPanelType PanelType);

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

    // ── Sidebar items ──────────────────────────────────────────────

    /// <summary>
    /// Static list of sidebar navigation items.
    /// Titles and icons match the macOS AppPanel enum (SidebarView.swift).
    /// Icon glyphs use Segoe MDL2 Assets font codes — the closest Windows
    /// equivalents of the SF Symbol icons used on macOS.
    ///
    /// Mapping:
    ///   Dashboard    — SF "chart.bar.fill"           → MDL2 "" (Chart)
    ///   API Config   — SF "key.fill"                 → MDL2 "" (Permissions)
    ///   Config Edit  — SF "doc.text.fill"            → MDL2 "" (Document)
    ///   MCP Manager  — SF "server.rack"              → MDL2 "" (Server)
    ///   Skill Mgr    — SF "puzzlepiece.extension.fill" → MDL2 "" (Extension)
    /// </summary>
    public List<SidebarItem> SidebarItems { get; } = new()
    {
        new SidebarItem("", "概览",       "系统状态与概览",     MainPanelType.Dashboard),
        new SidebarItem("", "API 配置",   "配置 API 密钥与模型", MainPanelType.ApiConfig),
        new SidebarItem("", "配置文件",   "编辑与管理配置文件",   MainPanelType.ConfigEditor),
        new SidebarItem("", "MCP 服务器", "管理 MCP 服务器连接",  MainPanelType.McpManager),
        new SidebarItem("", "技能",       "管理 Claude Code 技能", MainPanelType.SkillManager),
        new SidebarItem("", "安装器",     "安装和管理 Claude Code CLI", MainPanelType.Installer),
        new SidebarItem("", "环境检测",   "检测 Node.js、npm、Git 环境依赖", MainPanelType.EnvCheck),
    };

    // ── Constructor ────────────────────────────────────────────────

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // Start on the dashboard panel.
        Navigate(MainPanelType.Dashboard);

        // Fire-and-forget update check (non-blocking, errors silently swallowed)
        _ = CheckForUpdateAsync().ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
                Debug.WriteLine($"[MainViewModel] Update check failed: {t.Exception.GetBaseException().Message}");
        }, TaskScheduler.Default);
    }

    // ── Update check ───────────────────────────────────────────────

    /// <summary>
    /// Checks GitHub Releases for a newer version.  Updates the
    /// <see cref="IsUpdateAvailable"/> properties on success.
    /// Called once on app startup.
    /// </summary>
    private async Task CheckForUpdateAsync()
    {
        var update = await UpdateService.Instance.CheckForUpdateAsync();
        if (update is not { IsNewer: true })
            return;

        UpdateVersion = update.Version;
        UpdateReleaseNotes = update.ReleaseNotes;
        UpdateUrl = update.ReleaseUrl;
        IsUpdateAvailable = true;
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
        catch { }
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
        SelectedPanelViewModel = panel switch
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
    }
}
