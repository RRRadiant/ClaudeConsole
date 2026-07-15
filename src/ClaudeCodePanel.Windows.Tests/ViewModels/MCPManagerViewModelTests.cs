using System.IO;
using System.Text.Json;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Tests.ViewModels;

public class MCPManagerViewModelTests
{
    [Fact]
    public void SaveServer_PluginWithoutProjectPath_StopsAndShowsError()
    {
        var viewModel = new MCPManagerViewModel
        {
            NewName = "plugin:test",
            NewServerType = MCPServerType.Plugin,
            NewEnabled = true
        };

        viewModel.SaveServer();

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Empty(viewModel.Servers);
    }

    [Fact]
    public void ResolveProjectPath_EmptyValue_ReturnsNull()
    {
        var result = MCPManagerViewModel.ResolveProjectPath("", MCPServerType.Plugin);

        Assert.Null(result);
    }

    [Fact]
    public void SaveServer_PreservesUnknownMcpProperties()
    {
        var tempPath = CreateTempClaudeConfig();
        try
        {
            var config = new FakeConfigFileService(tempPath);
            var viewModel = new MCPManagerViewModel(config, new FakeMcpService());
            var server = MCPServerConfig.FromJson(new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("remote"),
                ["type"] = JsonSerializer.SerializeToElement("sse"),
                ["url"] = JsonSerializer.SerializeToElement("https://example.test/sse"),
                ["enabled"] = JsonSerializer.SerializeToElement(true),
                ["headers"] = JsonSerializer.SerializeToElement(new Dictionary<string, string>
                {
                    ["X-Custom"] = "preserve-me"
                })
            })!;
            viewModel.Servers.Add(server);
            viewModel.StartEditing(server);

            viewModel.SaveServer();

            var mcpServers = config.LastWritten!["mcpServers"];
            var persistedServer = mcpServers.GetProperty("remote");
            Assert.Equal("preserve-me", persistedServer
                .GetProperty("headers")
                .GetProperty("X-Custom")
                .GetString());
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveServer_UsesReadMtimeForConflictProtection()
    {
        var tempPath = CreateTempClaudeConfig();
        try
        {
            var expectedMtime = File.GetLastWriteTimeUtc(tempPath);
            var config = new FakeConfigFileService(tempPath);
            var viewModel = new MCPManagerViewModel(config, new FakeMcpService())
            {
                NewName = "local",
                NewServerType = MCPServerType.Stdio,
                NewCommand = "node"
            };

            viewModel.SaveServer();

            Assert.Equal(expectedMtime, config.LastExpectedMtime);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static string CreateTempClaudeConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"claude-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{}");
        return path;
    }

    private sealed class FakeConfigFileService(string claudeGlobalConfigPath) : IConfigFileService
    {
        public string SettingsPath => "settings.json";
        public string SettingsLocalPath => "settings.local.json";
        public string McpPath => "mcp.json";
        public string SkillsDirectory => "skills";
        public string ClaudeGlobalConfigPath { get; } = claudeGlobalConfigPath;
        public Dictionary<string, JsonElement>? LastWritten { get; private set; }
        public DateTime? LastExpectedMtime { get; private set; }

        public List<ConfigFileInfo> ListConfigFiles() => new();
        public Dictionary<string, JsonElement>? TryReadJSON(string path) => ReadJSON(path);
        public Dictionary<string, JsonElement>? ReadJSON(string path) => ReadJSONOrEmpty(path);
        public Dictionary<string, JsonElement> ReadJSONOrEmpty(string path) => new();

        public void WriteJSON(
            Dictionary<string, JsonElement> dict,
            string path,
            DateTime? expectedMtime = null)
        {
            LastWritten = dict.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone());
            LastExpectedMtime = expectedMtime;
        }

        public void WriteText(string content, string path, DateTime? expectedMtime = null) =>
            throw new NotSupportedException();

        public void EnsureDirectoryExists(string path)
        {
        }
    }

    private sealed class FakeMcpService : IMCPService
    {
        public Task<MCPConnectionResult> TestConnectionAsync(MCPServerConfig config) =>
            Task.FromResult(MCPConnectionResult.Unknown());
    }
}
