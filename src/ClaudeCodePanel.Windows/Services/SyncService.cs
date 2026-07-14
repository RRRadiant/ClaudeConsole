using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Services;

// ─── SyncedConfig record ───────────────────────────────────

public sealed class SyncedConfig
{
    public APIProvider Provider { get; set; } = APIProvider.Anthropic;
    public string ApiKey { get; set; } = "";
    public string BaseURL { get; set; } = "";
    public string SelectedModel { get; set; } = "";
    public List<string> EnabledModels { get; set; } = new();
    public List<MCPServerConfig> McpServers { get; set; } = new();
    public List<string> SkillIds { get; set; } = new();
    public bool DidSync { get; set; }
}

// ─── SyncService singleton ─────────────────────────────────

public sealed class SyncService : ISyncService
{
    public static SyncService Instance { get; } = new();

    private readonly ConfigFileService _configService = ConfigFileService.Instance;

    private SyncService() { }

    // ── Public API ─────────────────────────────────────────

    /// <summary>
    /// Perform a full configuration sync across all sources:
    /// settings.json -> settings.local.json -> ~/.claude.json -> ~/.claude/.mcp.json
    /// </summary>
    public SyncedConfig SyncAll()
    {
        var result = new SyncedConfig();

        // Read settings.json (primary) and settings.local.json (overrides)
        var settingsDict = ReadJSONSafe(_configService.SettingsPath);
        var localDict = ReadJSONSafe(_configService.SettingsLocalPath);

        // --- Extract from settings.json env ---
        if (settingsDict.TryGetValue("env", out var envElement) &&
            envElement.ValueKind == JsonValueKind.Object)
        {
            var env = EnumerateStringObject(envElement);
            if (env != null)
                result = ApplyEnv(env, result);
        }

        // --- Override with settings.local.json env if present ---
        if (localDict.TryGetValue("env", out var localEnvElement) &&
            localEnvElement.ValueKind == JsonValueKind.Object)
        {
            var localEnv = EnumerateStringObject(localEnvElement);
            if (localEnv != null)
                result = ApplyEnv(localEnv, result);
        }

        // --- Extract enabledPlugins → enabled skill ids (strip @source suffix) ---
        result.SkillIds = ExtractEnabledSkillIds(settingsDict, localDict);

        // --- Extract MCP servers from ~/.claude.json (primary source) ---
        result.McpServers = ExtractMCPFromClaudeGlobalJSON();

        // Fallback: settings.json mcpServers key
        if (result.McpServers.Count == 0)
            result.McpServers = ExtractMCPServers(settingsDict);

        // Fallback: settings.local.json mcpServers key
        if (result.McpServers.Count == 0)
        {
            var localServers = ExtractMCPServers(localDict);
            if (localServers.Count > 0)
                result.McpServers = localServers;
        }

        // Fallback: ~/.claude/.mcp.json file
        if (result.McpServers.Count == 0)
        {
            var settingsDir = Path.GetDirectoryName(_configService.SettingsPath);
            var dotMcpPath = Path.Combine(settingsDir ?? "", ".mcp.json");
            var dotMcpDict = ReadJSONSafe(dotMcpPath);
            if (dotMcpDict.Count > 0)
                result.McpServers = ExtractMCPServers(dotMcpDict);
        }

        result.DidSync = true;
        return result;
    }

    // ── Helpers ────────────────────────────────────────────

    /// <summary>
    /// Apply an environment dictionary to a SyncedConfig, returning the updated config.
    /// Extracts: base URL (with provider detection), auth token, selected model, enabled models.
    /// </summary>
    internal static SyncedConfig ApplyEnv(Dictionary<string, string> env, SyncedConfig config)
    {
        // ANTHROPIC_BASE_URL → baseURL + provider detection
        if (env.TryGetValue("ANTHROPIC_BASE_URL", out var baseURL) && !string.IsNullOrEmpty(baseURL))
        {
            config.BaseURL = baseURL;

            var lowercased = baseURL.ToLowerInvariant();
            if (lowercased.Contains("deepseek"))
                config.Provider = APIProvider.DeepSeek;
            else if (lowercased.Contains("openai"))
                config.Provider = APIProvider.OpenAI;
            else
                config.Provider = APIProvider.Anthropic;
        }

        // ANTHROPIC_AUTH_TOKEN → apiKey
        if (env.TryGetValue("ANTHROPIC_AUTH_TOKEN", out var authToken) && !string.IsNullOrEmpty(authToken))
            config.ApiKey = authToken;

        // ANTHROPIC_MODEL → selectedModel
        if (env.TryGetValue("ANTHROPIC_MODEL", out var model) && !string.IsNullOrEmpty(model))
            config.SelectedModel = model;

        // ANTHROPIC_DEFAULT_OPUS_MODEL, _SONNET_MODEL, _HAIKU_MODEL → enabledModels
        var modelKeys = new[]
        {
            "ANTHROPIC_DEFAULT_OPUS_MODEL",
            "ANTHROPIC_DEFAULT_SONNET_MODEL",
            "ANTHROPIC_DEFAULT_HAIKU_MODEL",
        };

        var models = new List<string>();
        foreach (var key in modelKeys)
        {
            if (env.TryGetValue(key, out var m) && !string.IsNullOrEmpty(m))
            {
                // Strip [1M] suffix if present (case-insensitive)
                var cleaned = m
                    .Replace("[1M]", "")
                    .Replace("[1m]", "")
                    .Trim();
                if (!string.IsNullOrEmpty(cleaned))
                    models.Add(cleaned);
            }
        }

        if (models.Count > 0)
            config.EnabledModels = models;

        return config;
    }

    internal static List<string> ExtractEnabledSkillIds(
        Dictionary<string, JsonElement> settingsDict,
        Dictionary<string, JsonElement> localDict)
    {
        var mergedPlugins = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (settingsDict.TryGetValue("enabledPlugins", out var settingsPluginsElement) &&
            settingsPluginsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var plugin in settingsPluginsElement.EnumerateObject())
                mergedPlugins[plugin.Name] = plugin.Value.Clone();
        }
        if (localDict.TryGetValue("enabledPlugins", out var localPluginsElement) &&
            localPluginsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var plugin in localPluginsElement.EnumerateObject())
                mergedPlugins[plugin.Name] = plugin.Value.Clone();
        }

        return mergedPlugins
            .Where(static kvp => kvp.Value.ValueKind != JsonValueKind.False)
            .Select(static kvp =>
            {
                var key = kvp.Key;
                var atIndex = key.IndexOf('@');
                return atIndex >= 0 ? key[..atIndex] : key;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extract MCP servers from ~/.claude.json (Claude Code global config).
    /// Reads top-level mcpServers AND all projects' per-project mcpServers.
    /// </summary>
    private List<MCPServerConfig> ExtractMCPFromClaudeGlobalJSON()
    {
        var globalDict = ReadJSONSafe(_configService.ClaudeGlobalConfigPath);
        if (globalDict.Count == 0)
            return new List<MCPServerConfig>();

        var servers = new List<MCPServerConfig>();
        var seenNames = new HashSet<string>();

        // 1. Global mcpServers (top-level)
        if (globalDict.TryGetValue("mcpServers", out var globalServersElement) &&
            globalServersElement.ValueKind == JsonValueKind.Object)
        {
            var globalServers = EnumerateJsonObject(globalServersElement);
            if (globalServers != null)
            {
                foreach (var server in ParseMCPServerEntries(globalServers))
                {
                    seenNames.Add(server.Name);
                    servers.Add(server);
                }
            }
        }

        // 2. Scan ALL projects — collect their MCP servers (may override global)
        if (globalDict.TryGetValue("projects", out var projectsElement) &&
            projectsElement.ValueKind == JsonValueKind.Object)
        {
            var projects = EnumerateJsonObject(projectsElement);
            if (projects != null)
            {
                foreach (var (projectPath, projectDataElement) in projects)
                {
                    if (projectDataElement.ValueKind != JsonValueKind.Object)
                        continue;

                    var projectData = EnumerateJsonObject(projectDataElement);
                    if (projectData == null)
                        continue;

                    if (!projectData.TryGetValue("mcpServers", out var projectServersElement) ||
                        projectServersElement.ValueKind != JsonValueKind.Object)
                        continue;

                    var projectServers = EnumerateJsonObject(projectServersElement);
                    if (projectServers == null || projectServers.Count == 0)
                        continue;

                    var projectMCPs = ParseMCPServerEntries(projectServers);
                    foreach (var pmcp in projectMCPs)
                    {
                        pmcp.ProjectPath = projectPath;

                        if (servers.Any(s => s.Name == pmcp.Name && s.ProjectPath == null))
                        {
                            // Top-level already has this name — add project variant separately
                            servers.Add(pmcp);
                        }
                        else if (!seenNames.Contains(pmcp.Name + (pmcp.ProjectPath ?? "")))
                        {
                            seenNames.Add(pmcp.Name + (pmcp.ProjectPath ?? ""));
                            servers.Add(pmcp);
                        }
                    }
                }

                foreach (var (projectPath, projectDataElement) in projects)
                {
                    if (projectDataElement.ValueKind != JsonValueKind.Object)
                        continue;

                    var projectData = EnumerateJsonObject(projectDataElement);
                    if (projectData == null)
                        continue;

                    MergeProjectScopedServerStates(servers, projectPath, projectData);
                }
            }
        }

        return servers;
    }

    internal static void MergeProjectScopedServerStates(
        List<MCPServerConfig> servers,
        string projectPath,
        Dictionary<string, JsonElement> projectData)
    {
        if (projectData.TryGetValue("enabledMcpjsonServers", out var enabledElement) &&
            enabledElement.ValueKind == JsonValueKind.Array)
        {
            var enabledList = enabledElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!);

            foreach (var entry in enabledList)
            {
                var existing = servers.FirstOrDefault(server =>
                    server.Name == entry &&
                    string.Equals(server.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.Enabled = true;
                    continue;
                }

                var srvType = entry.StartsWith("plugin:", StringComparison.OrdinalIgnoreCase)
                    ? MCPServerType.Plugin
                    : MCPServerType.Builtin;
                servers.Add(new MCPServerConfig
                {
                    Name = entry,
                    ServerType = srvType,
                    Command = "",
                    Args = new(),
                    Env = new(),
                    Enabled = true,
                    Status = MCPServerStatus.Running,
                    ProjectPath = projectPath
                });
            }
        }

        if (projectData.TryGetValue("disabledMcpjsonServers", out var disabledElement) &&
            disabledElement.ValueKind == JsonValueKind.Array)
        {
            var disabledList = disabledElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!);

            foreach (var name in disabledList)
            {
                var existing = servers.FirstOrDefault(server =>
                    server.Name == name &&
                    string.Equals(server.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.Enabled = false;
                    continue;
                }

                var srvType = name.StartsWith("plugin:", StringComparison.OrdinalIgnoreCase)
                    ? MCPServerType.Plugin
                    : MCPServerType.Builtin;
                servers.Add(new MCPServerConfig
                {
                    Name = name,
                    ServerType = srvType,
                    Command = "",
                    Args = new(),
                    Env = new(),
                    Enabled = false,
                    Status = MCPServerStatus.Stopped,
                    ProjectPath = projectPath
                });
            }
        }
    }

    /// <summary>
    /// Parse a [name: config] MCP server dictionary into MCPServerConfig array.
    /// Handles both stdio (command+args) and sse (url) types.
    /// </summary>
    private static List<MCPServerConfig> ParseMCPServerEntries(Dictionary<string, JsonElement> entries)
    {
        var result = new List<MCPServerConfig>();

        foreach (var (name, configElement) in entries)
        {
            if (configElement.ValueKind != JsonValueKind.Object)
                continue;

            var configDict = EnumerateJsonObject(configElement);
            if (configDict == null)
                continue;

            // Inject the name into the config dict so FromJson can read it
            configDict["name"] = JsonSerializer.SerializeToElement(name);

            var server = MCPServerConfig.FromJson(configDict);
            if (server != null)
                result.Add(server);
        }

        return result;
    }

    /// <summary>
    /// Extract MCP server configs from a JSON dictionary.
    /// Handles both {"mcpServers": {"name": {...}}} and {"name": {...}} top-level formats.
    /// </summary>
    private static List<MCPServerConfig> ExtractMCPServers(Dictionary<string, JsonElement> dict)
    {
        // Try standard "mcpServers" key first
        if (dict.TryGetValue("mcpServers", out var mcpServersElement) &&
            mcpServersElement.ValueKind == JsonValueKind.Object)
        {
            var mcpServers = EnumerateJsonObject(mcpServersElement);
            if (mcpServers != null)
            {
                var result = new List<MCPServerConfig>();
                foreach (var (name, configElement) in mcpServers)
                {
                    if (configElement.ValueKind != JsonValueKind.Object)
                        continue;

                    var configDict = EnumerateJsonObject(configElement);
                    if (configDict == null)
                        continue;

                    configDict["name"] = JsonSerializer.SerializeToElement(name);

                    var server = MCPServerConfig.FromJson(configDict);
                    if (server != null)
                        result.Add(server);
                }
                return result;
            }
        }

        // Fallback: treat top-level dict entries as server configs
        // (skip known non-server keys)
        var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "env", "model", "enabledPlugins", "hooks", "provider",
            "baseURL", "maxTokens", "timeout", "enabledModels",
        };

        var possibleServers = dict
            .Where(kvp => !knownKeys.Contains(kvp.Key))
            .ToList();

        if (possibleServers.Count == 0)
            return new List<MCPServerConfig>();

        var fallbackResult = new List<MCPServerConfig>();
        foreach (var (key, valueElement) in possibleServers)
        {
            if (valueElement.ValueKind != JsonValueKind.Object)
                continue;

            var serverDict = EnumerateJsonObject(valueElement);
            if (serverDict == null)
                continue;

            serverDict["name"] = JsonSerializer.SerializeToElement(key);

            var server = MCPServerConfig.FromJson(serverDict);
            if (server != null)
                fallbackResult.Add(server);
        }

        return fallbackResult.Count > 0 ? fallbackResult : new List<MCPServerConfig>();
    }

    // ── Utility helpers ────────────────────────────────────

    /// <summary>
    /// Safely read a JSON file as Dictionary&lt;string, JsonElement&gt;.
    /// Returns an empty dictionary if the file doesn't exist or can't be parsed.
    /// </summary>
    private static Dictionary<string, JsonElement> ReadJSONSafe(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new Dictionary<string, JsonElement>();

            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            return dict ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    /// <summary>
    /// Enumerate a JsonElement of kind Object into a Dictionary&lt;string, JsonElement&gt;
    /// without a serialize-deserialize round-trip.
    /// </summary>
    private static Dictionary<string, JsonElement>? EnumerateJsonObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        var dict = new Dictionary<string, JsonElement>();
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = prop.Value.Clone();
        return dict;
    }

    /// <summary>
    /// Enumerate a JsonElement of kind Object into a Dictionary&lt;string, string&gt;
    /// without a serialize-deserialize round-trip.
    /// </summary>
    private static Dictionary<string, string>? EnumerateStringObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        var dict = new Dictionary<string, string>();
        foreach (var prop in element.EnumerateObject())
        {
            var str = prop.Value.GetString();
            if (str != null)
                dict[prop.Name] = str;
        }
        return dict;
    }
}
