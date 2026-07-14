using System.Collections.Generic;
using System.Text.Json;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Tests.ViewModels;

public class APIConfigViewModelTests
{
    [Fact]
    public async Task SaveConfigAsync_WritesApiKeyOnlyToSettingsLocal()
    {
        var config = new FakeConfigFileService();
        var credentials = new FakeCredentialService();
        config.Seed(
            config.SettingsPath,
            new Dictionary<string, JsonElement>
            {
                ["env"] = JsonSerializer.SerializeToElement(new Dictionary<string, string>
                {
                    ["ANTHROPIC_AUTH_TOKEN"] = "legacy-token",
                    ["ANTHROPIC_MODEL"] = "old-model"
                })
            });

        var viewModel = new APIConfigViewModel(config, credentials)
        {
            SelectedProvider = APIProvider.OpenAI,
            ApiKey = "sk-secret",
            BaseURL = "https://api.openai.com",
            EnabledModels = new HashSet<string> { "gpt-4o" },
            MaxTokens = 4096,
            Timeout = 30
        };

        await viewModel.SaveConfigAsync();

        var settings = config.ReadJSONOrEmpty(config.SettingsPath);
        var settingsEnv = settings["env"];
        Assert.False(settingsEnv.TryGetProperty("ANTHROPIC_AUTH_TOKEN", out _));
        Assert.Equal("https://api.openai.com", settingsEnv.GetProperty("ANTHROPIC_BASE_URL").GetString());
        Assert.Equal("gpt-4o", settingsEnv.GetProperty("ANTHROPIC_MODEL").GetString());

        var localSettings = config.ReadJSONOrEmpty(config.SettingsLocalPath);
        var localEnv = localSettings["env"];
        Assert.Equal("sk-secret", localEnv.GetProperty("ANTHROPIC_AUTH_TOKEN").GetString());

        Assert.True(credentials.Exists(APIProvider.OpenAI.CredentialKey()));
        Assert.Null(viewModel.ErrorMessage);
    }

    private sealed class FakeConfigFileService : IConfigFileService
    {
        private readonly Dictionary<string, Dictionary<string, JsonElement>> _files =
            new(StringComparer.OrdinalIgnoreCase);

        public string SettingsPath { get; } = "settings.json";
        public string SettingsLocalPath { get; } = "settings.local.json";
        public string McpPath { get; } = "mcp.json";
        public string SkillsDirectory { get; } = "skills";
        public string ClaudeGlobalConfigPath { get; } = ".claude.json";

        public List<ConfigFileInfo> ListConfigFiles() => new();

        public Dictionary<string, JsonElement>? TryReadJSON(string path) => ReadJSON(path);

        public Dictionary<string, JsonElement>? ReadJSON(string path)
        {
            return _files.TryGetValue(path, out var dict) ? Clone(dict) : null;
        }

        public Dictionary<string, JsonElement> ReadJSONOrEmpty(string path)
        {
            return ReadJSON(path) ?? new Dictionary<string, JsonElement>();
        }

        public void WriteJSON(Dictionary<string, JsonElement> dict, string path, DateTime? expectedMtime = null)
        {
            _files[path] = Clone(dict);
        }

        public void WriteText(string content, string path, DateTime? expectedMtime = null)
        {
            throw new NotSupportedException();
        }

        public void EnsureDirectoryExists(string path)
        {
        }

        public void Seed(string path, Dictionary<string, JsonElement> dict)
        {
            _files[path] = Clone(dict);
        }

        private static Dictionary<string, JsonElement> Clone(Dictionary<string, JsonElement> dict)
        {
            var clone = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dict)
                clone[kvp.Key] = kvp.Value.Clone();
            return clone;
        }
    }

    private sealed class FakeCredentialService : ICredentialService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public bool Exists(string key) => _values.ContainsKey(key);

        public bool TryRead(string key, out string value) => _values.TryGetValue(key, out value!);

        public void Save(string key, string value)
        {
            _values[key] = value;
        }

        public void Delete(string key)
        {
            _values.Remove(key);
        }
    }
}
