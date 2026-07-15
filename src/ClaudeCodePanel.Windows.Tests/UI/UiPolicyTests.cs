using ClaudeCodePanel.Windows.Design;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Tests.UI;

public class UiPolicyTests
{
    [Theory]
    [InlineData(1440, WindowLayoutMode.Wide, 272, 36)]
    [InlineData(1100, WindowLayoutMode.Laptop, 232, 28)]
    [InlineData(920, WindowLayoutMode.Compact, 76, 20)]
    [InlineData(760, WindowLayoutMode.Compact, 76, 16)]
    public void WindowLayoutProfile_UsesExpectedBreakpoint(
        double width,
        WindowLayoutMode expectedMode,
        double expectedSidebarWidth,
        double expectedPagePadding)
    {
        var profile = WindowLayoutProfile.ForWidth(width);

        Assert.Equal(expectedMode, profile.Mode);
        Assert.Equal(expectedSidebarWidth, profile.SidebarWidth);
        Assert.Equal(expectedPagePadding, profile.PagePadding);
    }

    [Fact]
    public void GlassPresetCatalog_UsesLowCostSubtlePreset()
    {
        var preset = GlassPresetCatalog.Get(GlassPresetKind.Subtle);

        Assert.Equal(18, preset.CornerRadius);
        Assert.Equal(0.06, preset.SurfaceOpacity);
        Assert.False(preset.UsesElasticity);
        Assert.False(preset.UsesChromaticEdge);
    }

    [Fact]
    public void GlassPresetCatalog_UsesElasticInteractivePreset()
    {
        var preset = GlassPresetCatalog.Get(GlassPresetKind.Interactive);

        Assert.True(preset.UsesElasticity);
        Assert.True(preset.UsesChromaticEdge);
        Assert.True(preset.HoverLift > 0);
    }

    [Theory]
    [InlineData(true, false, 2, true)]
    [InlineData(false, true, 2, true)]
    [InlineData(false, false, 0, true)]
    [InlineData(false, false, 1, false)]
    [InlineData(false, false, 2, false)]
    public void UiPerformancePolicy_DetectsReducedEffects(
        bool reduceMotion,
        bool remoteSession,
        int renderTier,
        bool expected)
    {
        Assert.Equal(expected, UiPerformancePolicy.ShouldReduceEffects(
            reduceMotion,
            remoteSession,
            renderTier));
    }

    [Theory]
    [InlineData(false, false, 2, false)]
    [InlineData(true, false, 2, false)]
    [InlineData(false, true, 2, false)]
    [InlineData(false, false, 0, false)]
    public void UiPerformancePolicy_DisablesContinuousDecorativeAnimation(
        bool reduceMotion,
        bool remoteSession,
        int renderTier,
        bool expected)
    {
        Assert.Equal(expected, UiPerformancePolicy.ShouldUseContinuousDecorativeAnimation(
            reduceMotion,
            remoteSession,
            renderTier));
    }

    [Fact]
    public void AppearancePanelState_TogglesIndependentlyFromNavigation()
    {
        var state = AppearancePanelState.Collapsed;

        state = state.Toggle();
        Assert.True(state.IsExpanded);

        state = state.Toggle();
        Assert.False(state.IsExpanded);
    }

    [Theory]
    [InlineData(MainPanelType.Dashboard)]
    [InlineData(MainPanelType.ApiConfig)]
    [InlineData(MainPanelType.ConfigEditor)]
    [InlineData(MainPanelType.McpManager)]
    [InlineData(MainPanelType.SkillManager)]
    [InlineData(MainPanelType.Installer)]
    [InlineData(MainPanelType.EnvCheck)]
    public void SidebarItems_ProvideTitleAndDescriptionForEveryPanel(MainPanelType panel)
    {
        var items = MainViewModel.CreateSidebarItemsForCurrentLanguage();
        var item = items.Single(candidate => candidate.PanelType == panel);

        Assert.False(string.IsNullOrWhiteSpace(item.Title));
        Assert.False(string.IsNullOrWhiteSpace(item.Description));
        Assert.Equal(items.Count, items.Select(candidate => candidate.Title).Distinct().Count());
    }
}
