using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.WebUI;

public sealed record DashboardEventSnapshot(
    DateTime Timestamp,
    string Message,
    string Type);

public sealed record DashboardSnapshot(
    string ClaudeVersion,
    bool IsClaudeInstalled,
    bool ApiConnected,
    string ApiProvider,
    int EnabledModelsCount,
    int InstalledSkillsCount,
    int ActiveMcpServersCount,
    int TotalMcpServersCount,
    IReadOnlyList<DashboardEventSnapshot> RecentEvents,
    DateTime LastUpdated)
{
    public static DashboardSnapshot FromSummary(DashboardSummary summary) => new(
        summary.ClaudeVersion,
        summary.IsClaudeInstalled,
        summary.ApiConnected,
        summary.ApiProvider,
        summary.EnabledModelsCount,
        summary.InstalledSkillsCount,
        summary.ActiveMCPServersCount,
        summary.TotalMCPServersCount,
        summary.RecentEvents.Select(static item => new DashboardEventSnapshot(
            item.Timestamp,
            item.Message,
            item.Type.ToString().ToLowerInvariant())).ToArray(),
        summary.LastUpdated);
}

public sealed record ThemeSnapshot(string Mode, bool IsDark, string AccentColor);

public sealed record NavigationSnapshot(string Panel, bool UseNativeShell);

public sealed record NativeShellSnapshot(bool UseNativeShell);

public sealed record AppBootstrapSnapshot(DashboardSnapshot Dashboard, ThemeSnapshot Theme);
