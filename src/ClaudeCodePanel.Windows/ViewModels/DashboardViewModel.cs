using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
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
    private readonly ConfigFileService _configFileService;
    private readonly CredentialService _credentialService;

    [ObservableProperty]
    private DashboardSummary _summary = new();

    /// <summary>
    /// Always false on Windows — there is no DMG / /Applications concept.
    /// </summary>
    [ObservableProperty]
    private bool _isRunningFromDMG;

    public DashboardViewModel(ConfigFileService configFileService, CredentialService credentialService)
    {
        _configFileService = configFileService;
        _credentialService = credentialService;
    }

    /// <summary>
    /// Gathers dashboard data from the local filesystem and Claude CLI,
    /// then applies all updates on the UI thread via Dispatcher.InvokeAsync.
    /// </summary>
    public async Task LoadSummaryAsync()
    {
        // ── 1. Check Claude CLI via "claude --version" ──────────────────
        bool claudeInstalled = false;
        string claudeVersion = "";

        try
        {
            var startInfo = new ProcessStartInfo("claude", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    claudeInstalled = true;
                    var output = await process.StandardOutput.ReadToEndAsync();
                    claudeVersion = output?.Trim() ?? "";
                }
            }
        }
        catch
        {
            // Claude CLI not found — leave flags at defaults
        }

        // ── 2. Read settings.json once for models + provider ──────────
        int enabledModelsCount = 0;
        bool apiConnected = false;
        string apiProvider = "";
        APIProvider? configuredProvider = null;

        try
        {
            var settingsDict = _configFileService.ReadJSON(_configFileService.SettingsPath);
            if (settingsDict != null)
            {
                // Count enabled models
                if (settingsDict.TryGetValue("enabledModels", out var element) &&
                    element.ValueKind == JsonValueKind.Array)
                {
                    var models = element.EnumerateArray()
                        .Select(m => m.GetString())
                        .Where(s => s != null)
                        .Select(s => s!)
                        .ToList();
                    enabledModelsCount = models.Count;
                }

                // Determine API provider
                if (settingsDict.TryGetValue("provider", out var providerElement))
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
        }
        catch
        {
            // Ignore errors reading settings
        }

        if (configuredProvider.HasValue)
        {
            // Check the configured provider's credential first
            if (_credentialService.Exists(configuredProvider.Value.CredentialKey()))
            {
                apiConnected = true;
                apiProvider = configuredProvider.Value.DisplayName();
            }
        }

        // Fallback: if no provider configured or its credential is missing,
        // scan all providers for any existing credential.
        if (!apiConnected)
        {
            foreach (var provider in APIProviderExtensions.AllCases)
            {
                if (_credentialService.Exists(provider.CredentialKey()))
                {
                    apiConnected = true;
                    apiProvider = provider.DisplayName();
                    break;
                }
            }
        }

        // ── 4. Count MCP servers from mcp.json ─────────────────────────
        int totalMCPServersCount = 0;
        int activeMCPServersCount = 0;

        try
        {
            var mcpPath = _configFileService.McpPath;
            if (File.Exists(mcpPath))
            {
                var mcpJson = await File.ReadAllTextAsync(mcpPath);
                using var doc = JsonDocument.Parse(mcpJson);
                if (doc.RootElement.TryGetProperty("servers", out var serversElement) &&
                    serversElement.ValueKind == JsonValueKind.Array)
                {
                    var servers = serversElement.EnumerateArray().ToList();

                    totalMCPServersCount = servers.Count;
                    activeMCPServersCount = servers.Count(s =>
                        s.TryGetProperty("enabled", out var enabledElement)
                            ? enabledElement.GetBoolean()
                            : true);
                }
            }
        }
        catch
        {
            // Ignore errors reading MCP config
        }

        // ── 5. Count installed skills from ~/.claude/skills/ directory ─
        int installedSkillsCount = 0;

        try
        {
            var skillsDir = _configFileService.SkillsDirectory;
            if (Directory.Exists(skillsDir))
            {
                installedSkillsCount = Directory.GetDirectories(skillsDir).Length;
            }
        }
        catch
        {
            // Ignore errors reading skills directory
        }

        // ── 6. Fallback: SyncService.SyncAll() to fill any gaps ────────
        var synced = SyncService.Instance.SyncAll();
        if (synced.DidSync)
        {
            if (!apiConnected && !string.IsNullOrEmpty(synced.ApiKey))
            {
                apiConnected = true;
                apiProvider = synced.Provider.DisplayName();
            }

            if (totalMCPServersCount == 0 && synced.McpServers.Count > 0)
            {
                totalMCPServersCount = synced.McpServers.Count;
                activeMCPServersCount = synced.McpServers.Count(s => s.Enabled);
            }

            if (installedSkillsCount == 0 && synced.SkillIds.Count > 0)
            {
                installedSkillsCount = synced.SkillIds.Count;
            }

            if (enabledModelsCount == 0 && synced.EnabledModels.Count > 0)
            {
                enabledModelsCount = synced.EnabledModels.Count;
            }
        }

        // ── Apply all updates on the UI thread ─────────────────────────
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

            // 7. Add "仪表盘已刷新" event
            Summary.AddEvent("仪表盘已刷新", DashboardEventType.Info);
        });
    }

    /// <summary>
    /// Not ported — Windows does not have a DMG / /Applications concept.
    /// The original Swift implementation is omitted entirely.
    /// </summary>
    // moveToApplications() is intentionally NOT ported.
}
