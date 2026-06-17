using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeCodePanel.Windows.Models;

public partial class DashboardSummary : ObservableObject
{
    [ObservableProperty]
    private string _claudeVersion = "";

    [ObservableProperty]
    private bool _isClaudeInstalled;

    [ObservableProperty]
    private bool _apiConnected;

    [ObservableProperty]
    private string _apiProvider = "";

    [ObservableProperty]
    private int _enabledModelsCount;

    [ObservableProperty]
    private int _installedSkillsCount;

    [ObservableProperty]
    private int _activeMCPServersCount;

    [ObservableProperty]
    private int _totalMCPServersCount;

    [ObservableProperty]
    private List<DashboardEvent> _recentEvents = new();

    [ObservableProperty]
    private DateTime _lastUpdated = DateTime.UtcNow;

    public void AddEvent(string message, DashboardEventType type)
    {
        RecentEvents.Insert(0, new DashboardEvent(DateTime.UtcNow, message, type));
        if (RecentEvents.Count > 50)
            RecentEvents.RemoveRange(50, RecentEvents.Count - 50);
        LastUpdated = DateTime.UtcNow;
    }
}

public record DashboardEvent(DateTime Timestamp, string Message, DashboardEventType Type);

public enum DashboardEventType
{
    Success,
    Error,
    Info
}
