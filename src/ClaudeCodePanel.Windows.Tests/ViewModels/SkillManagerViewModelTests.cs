using System.Collections.Generic;
using System.Text.Json;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Tests.ViewModels;

public class SkillManagerViewModelTests
{
    [Fact]
    public void ReadPluginStates_ParsesMarketplaceKeysAndFalseValues()
    {
        var settings = new Dictionary<string, JsonElement>
        {
            ["enabledPlugins"] = JsonSerializer.SerializeToElement(new Dictionary<string, bool>
            {
                ["formatter@acme-tools"] = true,
                ["analyzer@security-plugins"] = false
            })
        };

        var states = SkillManagerViewModel.ReadPluginStates(settings);

        Assert.True(states["formatter"]);
        Assert.False(states["analyzer"]);
    }

    [Fact]
    public void WritePluginState_PreservesExistingMarketplaceSuffix()
    {
        var settings = new Dictionary<string, JsonElement>
        {
            ["enabledPlugins"] = JsonSerializer.SerializeToElement(new Dictionary<string, bool>
            {
                ["formatter@acme-tools"] = true
            })
        };

        SkillManagerViewModel.WritePluginState(settings, "formatter", false);

        var enabledPlugins = settings["enabledPlugins"];
        Assert.True(enabledPlugins.TryGetProperty("formatter@acme-tools", out var formatterState));
        Assert.False(formatterState.GetBoolean());
        Assert.False(enabledPlugins.TryGetProperty("formatter", out _));
    }

    [Fact]
    public void WritePluginState_AddsBareSkillIdWhenNoExistingKeyMatches()
    {
        var settings = new Dictionary<string, JsonElement>();

        SkillManagerViewModel.WritePluginState(settings, "review", true);

        var enabledPlugins = settings["enabledPlugins"];
        Assert.True(enabledPlugins.TryGetProperty("review", out var reviewState));
        Assert.True(reviewState.GetBoolean());
    }
}
