using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Views.MCP;

/// <summary>
/// Expandable MCP server card matching MCPServerCard.swift.
///
/// Header: server type icon (globe=SSE, terminal=STDIO, cube=内置, puzzle=插件),
///   MCPDisplayNameStore.EffectiveName with "(原名: ...)" hint when renamed,
///   connection status (progress spinner / "未测试" / StatusIndicator),
///   hover menu (test connection, edit, divider, delete).
///
/// Expanded section (animated): badges (type, enabled/disabled, project-level),
///   project path, command/url/args, env count, rename field, test connection button.
/// </summary>
public partial class MCPServerCard : UserControl
{
    // ── Dependency Properties ──────────────────────────────────────────

    public static readonly DependencyProperty ServerProperty =
        DependencyProperty.Register(
            nameof(Server),
            typeof(MCPServerConfig),
            typeof(MCPServerCard),
            new PropertyMetadata(null, OnServerChanged));

    public static readonly DependencyProperty ConnectionResultProperty =
        DependencyProperty.Register(
            nameof(ConnectionResult),
            typeof(MCPConnectionResult),
            typeof(MCPServerCard),
            new PropertyMetadata(MCPConnectionResult.Unknown(), OnConnectionResultChanged));

    public MCPServerConfig? Server
    {
        get => (MCPServerConfig?)GetValue(ServerProperty);
        set => SetValue(ServerProperty, value);
    }

    public MCPConnectionResult ConnectionResult
    {
        get => (MCPConnectionResult)GetValue(ConnectionResultProperty);
        set => SetValue(ConnectionResultProperty, value);
    }

    // ── CLR Events ─────────────────────────────────────────────────────

    public event Action<MCPServerConfig>? TestConnectionRequested;
    public event Action<MCPServerConfig>? ConfigureRequested;
    public event Action<MCPServerConfig>? DeleteRequested;

    // ── Internal State ─────────────────────────────────────────────────

    private bool _isExpanded;

    // ── Constructor ────────────────────────────────────────────────────

    public MCPServerCard()
    {
        InitializeComponent();
    }

    // ── Property Changed Callbacks ─────────────────────────────────────

    private static void OnServerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MCPServerCard card && e.NewValue is MCPServerConfig server)
            card.PopulateCard(server);
    }

    private static void OnConnectionResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MCPServerCard card && e.NewValue is MCPConnectionResult result)
            card.UpdateConnectionResult(result);
    }

    // ── Card Population ────────────────────────────────────────────────

    /// <summary>
    /// Populate all card fields from the given server config.
    /// </summary>
    private void PopulateCard(MCPServerConfig server)
    {
        if (TypeIcon == null || EffectiveNameLabel == null)
            return;

        // ── Type icon ──
        TypeIcon.Text = server.ServerType switch
        {
            MCPServerType.Sse => "",     // Globe
            MCPServerType.Stdio => "",    // CommandPrompt / terminal
            MCPServerType.Builtin => "",  // Cube / builtin
            MCPServerType.Plugin => "",   // Puzzle / extension
            _ => ""
        };

        // ── Effective name + original name hint ──
        var effectiveName = MCPDisplayNameStore.EffectiveName(server);
        EffectiveNameLabel.Text = effectiveName;

        var displayName = MCPDisplayNameStore.DisplayName(server.Id);
        if (displayName != null)
        {
            OriginalNameHint.Text = $"(原名: {server.Name})";
            OriginalNameHint.Visibility = Visibility.Visible;
        }
        else
        {
            OriginalNameHint.Visibility = Visibility.Collapsed;
        }

        // ── Project path (abbreviated) ──
        if (!string.IsNullOrEmpty(server.ProjectPath))
        {
            ProjectPathLabel.Text = AbbreviateProjectPath(server.ProjectPath);
            ProjectPathLabel.Visibility = Visibility.Visible;
        }
        else
        {
            ProjectPathLabel.Visibility = Visibility.Collapsed;
        }

        // ── Connection result ──
        UpdateConnectionResult(ConnectionResult);

        // ── Expanded section: badges ──
        PopulateBadges(server);

        // ── Project path detail ──
        if (!string.IsNullOrEmpty(server.ProjectPath))
        {
            ProjectDetailPath.Text = server.ProjectPath;
            ProjectDetailPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ProjectDetailPanel.Visibility = Visibility.Collapsed;
        }

        // ── Server-type-specific details ──
        PopulateServerDetails(server);

        // ── Env vars count ──
        if (server.Env.Count > 0)
        {
            EnvCountLabel.Text = $"环境变量: {server.Env.Count} 个";
            EnvCountLabel.Visibility = Visibility.Visible;
        }
        else
        {
            EnvCountLabel.Visibility = Visibility.Collapsed;
        }

        // ── Test connection button label ──
        UpdateTestConnectionLabel();
    }

    // ── Badges ─────────────────────────────────────────────────────────

    private void PopulateBadges(MCPServerConfig server)
    {
        if (BadgeRow == null) return;
        BadgeRow.Children.Clear();

        // Type badge
        var typeVariant = server.ServerType switch
        {
            MCPServerType.Stdio => "Info",
            MCPServerType.Sse => "Neutral",
            MCPServerType.Builtin => "Success",
            MCPServerType.Plugin => "Info",
            _ => "Info"
        };
        BadgeRow.Children.Add(new Views.Shared.Badge
        {
            Text = server.ServerType.Label(),
            Variant = typeVariant,
            Margin = new Thickness(0, 0, 6, 0)
        });

        // Enabled/Disabled badge
        BadgeRow.Children.Add(new Views.Shared.Badge
        {
            Text = server.Enabled ? "已启用" : "已禁用",
            Variant = server.Enabled ? "Success" : "Neutral",
            Margin = new Thickness(0, 0, 6, 0)
        });

        // Project-level badge
        if (server.ProjectPath != null)
        {
            BadgeRow.Children.Add(new Views.Shared.Badge
            {
                Text = "项目级",
                Variant = "Neutral"
            });
        }
    }

    // ── Server Details ─────────────────────────────────────────────────

    private void PopulateServerDetails(MCPServerConfig server)
    {
        // Hide all first
        SSEDetailPanel.Visibility = Visibility.Collapsed;
        StdioCmdPanel.Visibility = Visibility.Collapsed;
        StdioArgsLabel.Visibility = Visibility.Collapsed;
        BuiltinPluginLabel.Visibility = Visibility.Collapsed;

        switch (server.ServerType)
        {
            case MCPServerType.Sse:
                SSEUrlLabel.Text = server.Url;
                SSEDetailPanel.Visibility = Visibility.Visible;
                break;

            case MCPServerType.Stdio:
                StdioCommandLabel.Text = server.Command;
                StdioCmdPanel.Visibility = Visibility.Visible;

                if (server.Args.Count > 0)
                {
                    StdioArgsLabel.Text = $"参数: {string.Join(" ", server.Args)}";
                    StdioArgsLabel.Visibility = Visibility.Visible;
                }
                break;

            case MCPServerType.Builtin:
            case MCPServerType.Plugin:
                BuiltinPluginLabel.Text = $"类型: {server.ServerType.Label()}";
                BuiltinPluginLabel.Visibility = Visibility.Visible;
                break;
        }
    }

    // ── Connection Result ──────────────────────────────────────────────

    private void UpdateConnectionResult(MCPConnectionResult result)
    {
        if (ConnectionResultPanel == null) return;

        TestingSpinner.Visibility = Visibility.Collapsed;
        UntestedLabel.Visibility = Visibility.Collapsed;
        ConnectionStatus.Visibility = Visibility.Collapsed;

        switch (result.State)
        {
            case MCPConnectionState.Testing:
                TestingSpinner.Visibility = Visibility.Visible;
                break;

            case MCPConnectionState.Unknown:
                UntestedLabel.Visibility = Visibility.Visible;
                break;

            case MCPConnectionState.Success:
            case MCPConnectionState.Failure:
                ConnectionStatus.Status = result.IndicatorStatus == IndicatorStatus.Running
                    ? "Running" : "Error";
                ConnectionStatus.Label = result.Label;
                ConnectionStatus.Visibility = Visibility.Visible;
                break;
        }

        UpdateTestConnectionLabel();
    }

    private void UpdateTestConnectionLabel()
    {
        if (TestConnectionLabel == null) return;
        TestConnectionLabel.Text = ConnectionResult.State switch
        {
            MCPConnectionState.Unknown => "测试连接",
            MCPConnectionState.Testing => "测试中…",
            _ => "重新测试"
        };
    }

    // ── Expand / Collapse ──────────────────────────────────────────────

    private void OnHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (ExpandedSection == null) return;
        _isExpanded = !_isExpanded;
        ExpandedSection.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;

        // Animate expand/collapse (opacity + slide)
        AnimateExpansion(_isExpanded);
    }

    private void AnimateExpansion(bool show)
    {
        if (ExpandedSection == null) return;

        var animation = new DoubleAnimation
        {
            From = show ? 0.0 : 1.0,
            To = show ? 1.0 : 0.0,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        ExpandedSection.BeginAnimation(OpacityProperty, animation);
    }

    // ── Hover Behavior ─────────────────────────────────────────────────

    private void OnCardMouseEnter(object sender, MouseEventArgs e)
    {
        if (MenuButton != null)
            MenuButton.Visibility = Visibility.Visible;
    }

    private void OnCardMouseLeave(object sender, MouseEventArgs e)
    {
        if (MenuButton != null && !ContextMenuPopup.IsOpen)
            MenuButton.Visibility = Visibility.Collapsed;
    }

    // ── Context Menu ───────────────────────────────────────────────────

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        ContextMenuPopup.IsOpen = !ContextMenuPopup.IsOpen;
    }

    private void OnTestFromMenu(object sender, RoutedEventArgs e)
    {
        ContextMenuPopup.IsOpen = false;
        if (Server != null)
            TestConnectionRequested?.Invoke(Server);
    }

    private void OnEditFromMenu(object sender, RoutedEventArgs e)
    {
        ContextMenuPopup.IsOpen = false;
        if (Server != null)
            ConfigureRequested?.Invoke(Server);
    }

    private void OnDeleteFromMenu(object sender, RoutedEventArgs e)
    {
        ContextMenuPopup.IsOpen = false;
        if (Server != null)
            DeleteRequested?.Invoke(Server);
    }

    // ── Test Connection from Expanded Section ──────────────────────────

    private void OnTestFromCard(object sender, RoutedEventArgs e)
    {
        if (Server != null)
            TestConnectionRequested?.Invoke(Server);
    }

    // ── Rename ─────────────────────────────────────────────────────────

    private void OnStartRename(object sender, RoutedEventArgs e)
    {
        if (Server == null) return;

        if (RenameInput != null)
            RenameInput.Text = MCPDisplayNameStore.DisplayName(Server.Id) ?? Server.Name;

        RenameEditPanel.Visibility = Visibility.Visible;
        RenameIdleBtn.Visibility = Visibility.Collapsed;
    }

    private void OnRenameDoneClick(object sender, RoutedEventArgs e)
    {
        CommitRename();
    }

    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRename();
            e.Handled = true;
        }
    }

    private void OnRenameCancelClick(object sender, RoutedEventArgs e)
    {
        CancelRename();
    }

    /// <summary>
    /// Commit the rename: trim input, reset if empty or matches original,
    /// otherwise save via MCPDisplayNameStore. Matches Swift commitRename().
    /// </summary>
    private void CommitRename()
    {
        if (Server == null || RenameInput == null) return;

        var trimmed = RenameInput.Text.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == Server.Name)
        {
            // Reset — use config name
            MCPDisplayNameStore.SetDisplayName(null, Server.Id);
        }
        else
        {
            MCPDisplayNameStore.SetDisplayName(trimmed, Server.Id);
        }

        RenameInput.Text = "";
        RenameEditPanel.Visibility = Visibility.Collapsed;
        RenameIdleBtn.Visibility = Visibility.Visible;

        // Refresh the displayed name
        PopulateCard(Server);
    }

    private void CancelRename()
    {
        if (RenameInput != null) RenameInput.Text = "";
        RenameEditPanel.Visibility = Visibility.Collapsed;
        RenameIdleBtn.Visibility = Visibility.Visible;
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Shorten project path for display: "0xsteph-pentest-ai-agents-https-github"
    /// becomes "pentest-ai-agents". Matches Swift projectAbbreviation().
    /// </summary>
    private static string AbbreviateProjectPath(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            return path;

        var parts = name.Split('-');
        if (parts.Length > 3)
        {
            return string.Join("-", parts[^3..]);
        }
        return name;
    }
}
