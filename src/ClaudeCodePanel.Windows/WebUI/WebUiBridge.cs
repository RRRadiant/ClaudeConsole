using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.WebUI;

public sealed class WebUiBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<string, MainPanelType> PanelMap =
        new Dictionary<string, MainPanelType>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard"] = MainPanelType.Dashboard,
            ["api-config"] = MainPanelType.ApiConfig,
            ["config-editor"] = MainPanelType.ConfigEditor,
            ["mcp-manager"] = MainPanelType.McpManager,
            ["skill-manager"] = MainPanelType.SkillManager,
            ["installer"] = MainPanelType.Installer,
            ["env-check"] = MainPanelType.EnvCheck
        };

    private readonly Func<Task<DashboardSummary>> _dashboardProvider;
    private readonly Func<ThemeSnapshot> _themeProvider;
    private readonly Action<MainPanelType> _navigate;
    private readonly Action _showNativeShell;

    public WebUiBridge(
        Func<Task<DashboardSummary>> dashboardProvider,
        Func<ThemeSnapshot> themeProvider,
        Action<MainPanelType> navigate,
        Action? showNativeShell = null)
    {
        _dashboardProvider = dashboardProvider;
        _themeProvider = themeProvider;
        _navigate = navigate;
        _showNativeShell = showNativeShell ?? (() => { });
    }

    public async Task<WebUiResponse> HandleAsync(string json)
    {
        WebUiMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<WebUiMessage>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            return WebUiResponse.Failure(null, "invalid_message", exception.Message);
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Type))
            return WebUiResponse.Failure(message?.Id, "invalid_message", "Message type is required.");

        return message.Type switch
        {
            "app.ready" => await CreateBootstrapResponseAsync(message.Id),
            "dashboard.get" => await CreateDashboardResponseAsync(message.Id),
            "theme.get" => WebUiResponse.Success(message.Id, _themeProvider()),
            "navigation.select" => HandleNavigation(message),
            "shell.native" => ShowNativeShell(message.Id),
            _ => WebUiResponse.Failure(
                message.Id,
                "unknown_command",
                $"Unsupported command: {message.Type}")
        };
    }

    private WebUiResponse ShowNativeShell(string? id)
    {
        _showNativeShell();
        return WebUiResponse.Success(id, new NativeShellSnapshot(true));
    }

    private async Task<WebUiResponse> CreateBootstrapResponseAsync(string? id)
    {
        var dashboard = DashboardSnapshot.FromSummary(await _dashboardProvider());
        return WebUiResponse.Success(id, new AppBootstrapSnapshot(dashboard, _themeProvider()));
    }

    private async Task<WebUiResponse> CreateDashboardResponseAsync(string? id)
    {
        var dashboard = DashboardSnapshot.FromSummary(await _dashboardProvider());
        return WebUiResponse.Success(id, dashboard);
    }

    private WebUiResponse HandleNavigation(WebUiMessage message)
    {
        if (message.Payload is not { ValueKind: JsonValueKind.Object } payload ||
            !payload.TryGetProperty("panel", out var panelElement) ||
            panelElement.GetString() is not { Length: > 0 } panelKey)
        {
            return WebUiResponse.Failure(
                message.Id,
                "invalid_payload",
                "navigation.select requires a panel key.");
        }

        if (!PanelMap.TryGetValue(panelKey, out var panel))
        {
            return WebUiResponse.Failure(
                message.Id,
                "unknown_panel",
                $"Unknown panel: {panelKey}");
        }

        _navigate(panel);
        return WebUiResponse.Success(
            message.Id,
            new NavigationSnapshot(panelKey, false));
    }
}
