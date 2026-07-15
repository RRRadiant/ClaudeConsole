using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.ViewModels;
using ClaudeCodePanel.Windows.WebUI;

namespace ClaudeCodePanel.Windows.Tests.WebUI;

public class WebUiBridgeTests
{
    private static readonly DateTime Timestamp = new(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_ReturnsStructuredErrorForMalformedJson()
    {
        var bridge = CreateBridge();

        var response = await bridge.HandleAsync("{not-json");

        Assert.False(response.Ok);
        Assert.Equal("invalid_message", response.Error?.Code);
        Assert.False(string.IsNullOrWhiteSpace(response.Error?.Message));
    }

    [Fact]
    public async Task HandleAsync_ReturnsStructuredErrorForUnknownCommand()
    {
        var bridge = CreateBridge();

        var response = await bridge.HandleAsync("""
            { "id": "request-1", "type": "missing.command" }
            """);

        Assert.False(response.Ok);
        Assert.Equal("request-1", response.Id);
        Assert.Equal("unknown_command", response.Error?.Code);
    }

    [Fact]
    public async Task HandleAsync_ReturnsMappedDashboardSnapshot()
    {
        var summary = CreateSummary();
        var bridge = CreateBridge(summary: summary);

        var response = await bridge.HandleAsync("""
            { "id": "dashboard-1", "type": "dashboard.get" }
            """);

        var snapshot = Assert.IsType<DashboardSnapshot>(response.Data);
        Assert.True(response.Ok);
        Assert.Equal("dashboard-1", response.Id);
        Assert.Equal("1.2.3", snapshot.ClaudeVersion);
        Assert.Equal(3, snapshot.EnabledModelsCount);
        Assert.Equal(4, snapshot.InstalledSkillsCount);
        Assert.Equal(2, snapshot.ActiveMcpServersCount);
        Assert.Single(snapshot.RecentEvents);
        Assert.Equal("success", snapshot.RecentEvents[0].Type);
    }

    [Fact]
    public async Task HandleAsync_ReturnsThemeSnapshot()
    {
        var bridge = CreateBridge(theme: new ThemeSnapshot("system", true, "#6FAADD"));

        var response = await bridge.HandleAsync("""
            { "id": "theme-1", "type": "theme.get" }
            """);

        var theme = Assert.IsType<ThemeSnapshot>(response.Data);
        Assert.True(response.Ok);
        Assert.Equal("system", theme.Mode);
        Assert.True(theme.IsDark);
        Assert.Equal("#6FAADD", theme.AccentColor);
    }

    [Theory]
    [InlineData("dashboard", MainPanelType.Dashboard, false)]
    [InlineData("api-config", MainPanelType.ApiConfig, false)]
    [InlineData("config-editor", MainPanelType.ConfigEditor, false)]
    [InlineData("mcp-manager", MainPanelType.McpManager, false)]
    [InlineData("skill-manager", MainPanelType.SkillManager, false)]
    [InlineData("installer", MainPanelType.Installer, false)]
    [InlineData("env-check", MainPanelType.EnvCheck, false)]
    public async Task HandleAsync_NavigatesKnownPanel(
        string panelKey,
        MainPanelType expectedPanel,
        bool expectedNativeShell)
    {
        MainPanelType? navigatedPanel = null;
        var bridge = CreateBridge(navigate: panel => navigatedPanel = panel);

        var response = await bridge.HandleAsync($$"""
            { "id": "nav-1", "type": "navigation.select", "payload": { "panel": "{{panelKey}}" } }
            """);

        var result = Assert.IsType<NavigationSnapshot>(response.Data);
        Assert.True(response.Ok);
        Assert.Equal(expectedPanel, navigatedPanel);
        Assert.Equal(panelKey, result.Panel);
        Assert.Equal(expectedNativeShell, result.UseNativeShell);
    }

    [Fact]
    public async Task HandleAsync_AppReadyReturnsBootstrapState()
    {
        var bridge = CreateBridge(
            summary: CreateSummary(),
            theme: new ThemeSnapshot("dark", true, "#6FAADD"));

        var response = await bridge.HandleAsync("""
            { "id": "ready-1", "type": "app.ready" }
            """);

        var bootstrap = Assert.IsType<AppBootstrapSnapshot>(response.Data);
        Assert.True(response.Ok);
        Assert.Equal("1.2.3", bootstrap.Dashboard.ClaudeVersion);
        Assert.Equal("dark", bootstrap.Theme.Mode);
    }

    [Fact]
    public async Task HandleAsync_ShellNativeRequestsNativeFallback()
    {
        var nativeShellRequested = false;
        var bridge = CreateBridge(showNativeShell: () => nativeShellRequested = true);

        var response = await bridge.HandleAsync("""
            { "id": "native-1", "type": "shell.native" }
            """);

        var result = Assert.IsType<NativeShellSnapshot>(response.Data);
        Assert.True(response.Ok);
        Assert.True(nativeShellRequested);
        Assert.True(result.UseNativeShell);
    }

    private static WebUiBridge CreateBridge(
        DashboardSummary? summary = null,
        ThemeSnapshot? theme = null,
        Action<MainPanelType>? navigate = null,
        Action? showNativeShell = null) =>
        new(
            () => Task.FromResult(summary ?? CreateSummary()),
            () => theme ?? new ThemeSnapshot("dark", true, "#6FAADD"),
            navigate ?? (_ => { }),
            showNativeShell);

    private static DashboardSummary CreateSummary() => new()
    {
        ClaudeVersion = "1.2.3",
        IsClaudeInstalled = true,
        ApiConnected = true,
        ApiProvider = "Anthropic",
        EnabledModelsCount = 3,
        InstalledSkillsCount = 4,
        ActiveMCPServersCount = 2,
        TotalMCPServersCount = 5,
        LastUpdated = Timestamp,
        RecentEvents =
        [
            new DashboardEvent(Timestamp, "Workspace ready", DashboardEventType.Success)
        ]
    };
}
