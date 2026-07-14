using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;
using System.Text.Json;

namespace ClaudeCodePanel.Windows.Tests.Services;

public class SyncServiceApplyEnvTests
{
    [Fact]
    public void ApplyEnv_AnthropicBaseURL_SetsBaseURLAndProvider()
    {
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_BASE_URL"] = "https://api.anthropic.com"
        };

        var result = SyncService.ApplyEnv(env, new SyncedConfig());

        Assert.Equal("https://api.anthropic.com", result.BaseURL);
        Assert.Equal(APIProvider.Anthropic, result.Provider);
    }

    [Fact]
    public void ApplyEnv_DeepSeekURL_DetectsProvider()
    {
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_BASE_URL"] = "https://api.deepseek.com/anthropic"
        };

        var result = SyncService.ApplyEnv(env, new SyncedConfig());

        Assert.Equal(APIProvider.DeepSeek, result.Provider);
    }

    [Fact]
    public void ApplyEnv_OpenAIURL_DetectsProvider()
    {
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_BASE_URL"] = "https://api.openai.com"
        };

        var result = SyncService.ApplyEnv(env, new SyncedConfig());

        Assert.Equal(APIProvider.OpenAI, result.Provider);
    }

    [Fact]
    public void ApplyEnv_AuthToken_SetsApiKey()
    {
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_AUTH_TOKEN"] = "sk-test123"
        };

        var result = SyncService.ApplyEnv(env, new SyncedConfig());

        Assert.Equal("sk-test123", result.ApiKey);
    }

    [Fact]
    public void ApplyEnv_Model_SetsSelectedModel()
    {
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_MODEL"] = "claude-sonnet-4-6"
        };

        var result = SyncService.ApplyEnv(env, new SyncedConfig());

        Assert.Equal("claude-sonnet-4-6", result.SelectedModel);
    }

    [Fact]
    public void ApplyEnv_ModelKeys_StripsBracketSuffix()
    {
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_DEFAULT_OPUS_MODEL"] = "claude-opus-4-8 [1M]",
            ["ANTHROPIC_DEFAULT_SONNET_MODEL"] = "claude-sonnet-4-6",
            ["ANTHROPIC_DEFAULT_HAIKU_MODEL"] = "claude-haiku-4-5"
        };

        var result = SyncService.ApplyEnv(env, new SyncedConfig());

        Assert.Equal(3, result.EnabledModels.Count);
        Assert.Contains("claude-opus-4-8", result.EnabledModels);
        Assert.Contains("claude-sonnet-4-6", result.EnabledModels);
        Assert.Contains("claude-haiku-4-5", result.EnabledModels);
    }

    [Fact]
    public void ApplyEnv_EmptyEnv_DoesNotCrash()
    {
        var env = new Dictionary<string, string>();
        var result = SyncService.ApplyEnv(env, new SyncedConfig());

        Assert.Equal(string.Empty, result.BaseURL);
        Assert.Equal(string.Empty, result.ApiKey);
    }

    [Fact]
    public void ApplyEnv_MergesWithExistingConfig_PreservesOldValues()
    {
        var baseConfig = new SyncedConfig
        {
            Provider = APIProvider.OpenAI,
            ApiKey = "existing-key"
        };
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_BASE_URL"] = "https://api.anthropic.com"
        };

        var result = SyncService.ApplyEnv(env, baseConfig);

        Assert.Equal(APIProvider.Anthropic, result.Provider); // overridden
        Assert.Equal("existing-key", result.ApiKey);          // preserved
    }

    [Fact]
    public void ExtractEnabledSkillIds_MergesSettingsAndLocal_WithLocalOverride()
    {
        var settings = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["enabledPlugins"] = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, bool>
            {
                ["review@marketplace"] = true,
                ["test@marketplace"] = true
            })
        };
        var local = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["enabledPlugins"] = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, bool>
            {
                ["test@marketplace"] = false,
                ["qa@marketplace"] = true
            })
        };

        var result = SyncService.ExtractEnabledSkillIds(settings, local);

        Assert.Contains("review", result);
        Assert.Contains("qa", result);
        Assert.DoesNotContain("test", result);
    }

    [Fact]
    public void MergeProjectScopedServerStates_AddsEnabledAndDisabledEntriesForEachProject()
    {
        var servers = new List<MCPServerConfig>();
        var projectData = new Dictionary<string, JsonElement>
        {
            ["enabledMcpjsonServers"] = JsonSerializer.SerializeToElement(new[] { "plugin:lint", "builtin-a" }),
            ["disabledMcpjsonServers"] = JsonSerializer.SerializeToElement(new[] { "plugin:scan" })
        };

        SyncService.MergeProjectScopedServerStates(servers, "/workspace/demo", projectData);

        Assert.Contains(servers, server =>
            server.Name == "plugin:lint" &&
            server.ServerType == MCPServerType.Plugin &&
            server.Enabled &&
            server.ProjectPath == "/workspace/demo");
        Assert.Contains(servers, server =>
            server.Name == "builtin-a" &&
            server.ServerType == MCPServerType.Builtin &&
            server.Enabled &&
            server.ProjectPath == "/workspace/demo");
        Assert.Contains(servers, server =>
            server.Name == "plugin:scan" &&
            server.ServerType == MCPServerType.Plugin &&
            !server.Enabled &&
            server.ProjectPath == "/workspace/demo");
    }
}
