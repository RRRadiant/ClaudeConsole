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
/// Dashboard ViewModel — port of DashboardViewModel.swift.
/// Populates the dashboard summary card with Claude configuration status.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IConfigFileService _configFileService;
    private readonly ICredentialService _credentialService;

    [ObservableProperty]
    private DashboardSummary _summary = new();

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Always false on Windows — there is no DMG / /Applications concept.
    /// </summary>
    [ObservableProperty]
    private bool _isRunningFromDMG;

    public DashboardViewModel(IConfigFileService configFileService, ICredentialService credentialService)
    {
        _configFileService = configFileService;
        _credentialService = credentialService;
    }

    /// <summary>
    /// Gathers dashboard data from the local filesystem and Claude CLI,
    /// then applies all updates on the UI thread via Dispatcher.InvokeAsync.
    /// Independent operations run concurrently for faster load times.
    /// </summary>
    public async Task LoadSummaryAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            // Clear previous events to prevent unbounded growth
            Summary.RecentEvents.Clear();

            // ── Launch independent operations concurrently ──
            var claudeStatusTask = InstallerService.Instance.GetClaudeStatusAsync();
            var syncedTask = Task.Run(() => SyncService.Instance.SyncAll());
            var settingsTask = Task.Run(() => _configFileService.TryReadJSON(_configFileService.SettingsPath));
            var mcpTask = Task.Run(() =>
            {
                try
                {
                    var mcpPath = _configFileService.McpPath;
                    if (!File.Exists(mcpPath)) return (0, 0);
                    var mcpJson = File.ReadAllText(mcpPath);
                    using var doc = JsonDocument.Parse(mcpJson);
                    if (doc.RootElement.TryGetProperty("servers", out var serversElement) &&
                        serversElement.ValueKind == JsonValueKind.Array)
                    {
                        var servers = serversElement.EnumerateArray().ToList();
                        int total = servers.Count;
                        int active = servers.Count(s =>
                            s.TryGetProperty("enabled", out var enabledElement)
                                ? enabledElement.GetBoolean()
                                : true);
                        return (total, active);
                    }
                }
                catch (Exception ex) { SharedHelpers.SafeLog("DashboardViewModel.LoadSummary.MCP", ex); }
                return (0, 0);
            });
            var skillsTask = Task.Run(() =>
            {
                try
                {
                    var skillsDir = _configFileService.SkillsDirectory;
                    return Directory.Exists(skillsDir) ? Directory.GetDirectories(skillsDir).Length : 0;
                }
                catch (Exception ex) { SharedHelpers.SafeLog("DashboardViewModel.LoadSummary.Skills", ex); return 0; }
            });

            await Task.WhenAll(claudeStatusTask, syncedTask, settingsTask, mcpTask, skillsTask).ConfigureAwait(true);

            // ── Collect results ──
            var status = await claudeStatusTask;
            bool claudeInstalled = status.Installed;
            string claudeVersion = status.Version ?? "";
            var synced = await syncedTask;
            var hasSyncedApiConfig =
                synced.DidSync &&
                (!string.IsNullOrEmpty(synced.ApiKey) ||
                 !string.IsNullOrEmpty(synced.BaseURL) ||
                 synced.EnabledModels.Count > 0);

            var settingsDict = await settingsTask;
            int enabledModelsCount = hasSyncedApiConfig ? synced.EnabledModels.Count : 0;
            APIProvider? configuredProvider = hasSyncedApiConfig ? synced.Provider : null;

            if (settingsDict != null && (enabledModelsCount == 0 || !configuredProvider.HasValue))
            {
                if (enabledModelsCount == 0 &&
                    settingsDict.TryGetValue("enabledModels", out var element) &&
                    element.ValueKind == JsonValueKind.Array)
                {
                    enabledModelsCount = element.EnumerateArray()
                        .Select(m => m.GetString())
                        .Where(s => s != null)
                        .Count();
                }

                if (!configuredProvider.HasValue &&
                    settingsDict.TryGetValue("provider", out var providerElement))
                {
                    var providerStr = providerElement.GetString()?.ToLowerInvariant();
                    configuredProvider = providerStr switch
                    {
                        "anthropic" => APIProvider.Anthropic,
                        "openai" => APIProvider.OpenAI,
                        "deepseek" => APIProvider.DeepSeek,
                        "custom" => APIProvider.Custom,
                        _ => null
                    };
                }
            }

            var (fileTotalMCPServersCount, fileActiveMCPServersCount) = await mcpTask;
            var fileInstalledSkillsCount = await skillsTask;
            var totalMCPServersCount = synced.DidSync && synced.McpServers.Count > 0
                ? synced.McpServers.Count
                : fileTotalMCPServersCount;
            var activeMCPServersCount = synced.DidSync && synced.McpServers.Count > 0
                ? synced.McpServers.Count(static s => s.Enabled)
                : fileActiveMCPServersCount;
            var installedSkillsCount = synced.DidSync && synced.SkillIds.Count > 0
                ? synced.SkillIds.Count
                : fileInstalledSkillsCount;

            // ── API credential check ──
            bool apiConnected = false;
            string apiProvider = "";
            APIProvider? effectiveProvider = configuredProvider;

            if (effectiveProvider.HasValue &&
                (hasSyncedApiConfig || settingsDict?.ContainsKey("provider") == true) &&
                _credentialService.Exists(effectiveProvider.Value.CredentialKey()))
            {
                apiConnected = true;
                apiProvider = effectiveProvider.Value.DisplayName();
            }

            // ── SyncService fallback ──
            if (synced.DidSync)
            {
                if (!apiConnected && !string.IsNullOrEmpty(synced.ApiKey))
                {
                    apiConnected = true;
                    apiProvider = synced.Provider.DisplayName();
                }
                if (enabledModelsCount == 0 && synced.EnabledModels.Count > 0)
                    enabledModelsCount = synced.EnabledModels.Count;
            }

            // ── Apply all updates on the UI thread ──
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Summary.IsClaudeInstalled = claudeInstalled;
                Summary.ClaudeVersion = claudeVersion;
                Summary.EnabledModelsCount = enabledModelsCount;
                Summary.ApiConnected = apiConnected;
                Summary.ApiProvider = apiProvider;
                Summary.TotalMCPServersCount = totalMCPServersCount;
                Summary.ActiveMCPServersCount = activeMCPServersCount;
                Summary.InstalledSkillsCount = installedSkillsCount;

                if (claudeInstalled)
                    Summary.AddEvent($"Claude CLI 已检测: {claudeVersion}", DashboardEventType.Success);
                else
                    Summary.AddEvent("Claude CLI 未安装", DashboardEventType.Error);

                if (apiConnected)
                    Summary.AddEvent($"API 已连接: {apiProvider}", DashboardEventType.Success);
                else
                    Summary.AddEvent("API 未配置", DashboardEventType.Error);

                if (enabledModelsCount > 0)
                    Summary.AddEvent($"已启用 {enabledModelsCount} 个模型", DashboardEventType.Success);

                if (installedSkillsCount > 0)
                    Summary.AddEvent($"已安装 {installedSkillsCount} 个 Skill", DashboardEventType.Success);

                if (totalMCPServersCount > 0)
                    Summary.AddEvent($"MCP 服务: {activeMCPServersCount}/{totalMCPServersCount} 运行中", DashboardEventType.Success);

                Summary.AddEvent("仪表盘已刷新", DashboardEventType.Info);
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Not ported — Windows does not have a DMG / /Applications concept.
    /// The original Swift implementation is omitted entirely.
    /// </summary>
    // moveToApplications() is intentionally NOT ported.
}
