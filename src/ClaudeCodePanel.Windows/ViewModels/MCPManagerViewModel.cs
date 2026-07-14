using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeCodePanel.Windows.ViewModels;

/// <summary>
/// MCP Manager ViewModel — port of MCPManagerViewModel.swift.
/// Manages MCP server listing, CRUD, connection testing, and
/// persistence to ~/.claude.json.
/// </summary>
public partial class MCPManagerViewModel : ObservableObject
{
    private readonly IConfigFileService _configFileService;
    private readonly IMCPService _mcpService;

    // ── Server list ──────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<MCPServerConfig> _servers = new();

    // ── Form state ───────────────────────────────────────────

    [ObservableProperty]
    private bool _isAddingServer;

    [ObservableProperty]
    private MCPServerConfig? _editingServer;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _didAttemptSync;

    // ── Connection results ───────────────────────────────────

    /// <summary>Per-server connection test results. Thread-safe.</summary>
    private readonly ConcurrentDictionary<Guid, MCPConnectionResult> _connectionResults = new();

    // ── Form fields ──────────────────────────────────────────

    [ObservableProperty]
    private string _newName = "";

    [ObservableProperty]
    private string _newCommand = "";

    [ObservableProperty]
    private string _newUrl = "";

    [ObservableProperty]
    private MCPServerType _newServerType = MCPServerType.Stdio;

    [ObservableProperty]
    private bool _newEnabled = true;

    [ObservableProperty]
    private List<string> _newArgs = new();

    [ObservableProperty]
    private List<(string Key, string Value)> _newEnv = new();

    [ObservableProperty]
    private string _newArgInput = "";

    [ObservableProperty]
    private string _newEnvKeyInput = "";

    [ObservableProperty]
    private string _newEnvValueInput = "";

    // ── Constructor ──────────────────────────────────────────

    public MCPManagerViewModel(
        IConfigFileService? configFileService = null,
        IMCPService? mcpService = null)
    {
        _configFileService = configFileService ?? ConfigFileService.Instance;
        _mcpService = mcpService ?? MCPService.Instance;
    }

    // ── Load ─────────────────────────────────────────────────

    /// <summary>
    /// Load MCP servers from SyncService (primary: ~/.claude.json),
    /// then fill in any missing servers from mcp.json. Deduplicates by name.
    /// </summary>
    [RelayCommand]
    public void LoadServers()
    {
        ErrorMessage = null;
        var mergedServers = new List<MCPServerConfig>();
        var seenNames = new HashSet<string>();

        // Primary source: ~/.claude.json (via SyncService)
        var synced = SyncService.Instance.SyncAll();
        if (synced.DidSync)
        {
            foreach (var server in synced.McpServers)
            {
                seenNames.Add(server.Name);
                mergedServers.Add(server);
            }
            DidAttemptSync = true;
        }

        // Fallback: mcp.json — only for servers NOT already loaded
        try
        {
            var mcpPath = _configFileService.McpPath;
            if (File.Exists(mcpPath))
            {
                var mcpJson = File.ReadAllText(mcpPath);
                using var doc = JsonDocument.Parse(mcpJson);
                if (doc.RootElement.TryGetProperty("servers", out var serversElement) &&
                    serversElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var serverElement in serversElement.EnumerateArray())
                    {
                        if (serverElement.ValueKind != JsonValueKind.Object)
                            continue;

                        var serverDict = SharedHelpers.EnumerateJsonObject(serverElement);
                        if (serverDict == null)
                            continue;

                        var server = MCPServerConfig.FromJson(serverDict);
                        if (server != null && !seenNames.Contains(server.Name))
                        {
                            seenNames.Add(server.Name);
                            mergedServers.Add(server);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("MCPManagerViewModel.LoadServers", ex);
        }

        Servers.Clear();
        foreach (var s in mergedServers)
            Servers.Add(s);
    }

    // ── Edit ─────────────────────────────────────────────────

    /// <summary>
    /// Populate the form with the given server's values for editing.
    /// </summary>
    [RelayCommand]
    public void StartEditing(MCPServerConfig server)
    {
        EditingServer = server;
        NewName = server.Name;
        NewServerType = server.ServerType;
        NewCommand = server.Command;
        NewUrl = server.Url;
        NewArgs = new List<string>(server.Args);
        NewEnv = server.Env.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        NewEnabled = server.Enabled;
        IsAddingServer = true;
    }

    /// <summary>
    /// Save the current form as either an edited existing server or a new server,
    /// then persist everything to ~/.claude.json.
    /// </summary>
    [RelayCommand]
    public void SaveServer()
    {
        MCPServerConfig server;
        if (EditingServer != null)
        {
            server = EditingServer;
            server.Name = NewName;
            server.ServerType = NewServerType;
            server.Enabled = NewEnabled;
            server.Url = NewServerType == MCPServerType.Sse ? NewUrl : "";
            server.Command = NewServerType == MCPServerType.Sse ? "" : NewCommand;
            server.Args = NewServerType is MCPServerType.Stdio
                ? NewArgs.Where(a => !string.IsNullOrEmpty(a)).ToList()
                : new List<string>();
            server.Env = NewServerType is MCPServerType.Stdio
                ? NewEnv
                    .Where(e => !string.IsNullOrEmpty(e.Key))
                    .ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string>();

            if (server.ServerType is MCPServerType.Builtin or MCPServerType.Plugin &&
                string.IsNullOrEmpty(server.ProjectPath))
            {
                server.ProjectPath = Directory.GetCurrentDirectory();
            }
        }
        else
        {
            server = new MCPServerConfig
            {
                Name = NewName,
                ServerType = NewServerType,
                Enabled = NewEnabled,
                Url = NewServerType == MCPServerType.Sse ? NewUrl : "",
                Command = NewServerType == MCPServerType.Sse ? "" : NewCommand,
                Args = NewServerType is MCPServerType.Stdio
                    ? NewArgs.Where(a => !string.IsNullOrEmpty(a)).ToList()
                    : new List<string>(),
                Env = NewServerType is MCPServerType.Stdio
                    ? NewEnv
                        .Where(e => !string.IsNullOrEmpty(e.Key))
                        .ToDictionary(e => e.Key, e => e.Value)
                    : new Dictionary<string, string>(),
                ProjectPath = NewServerType is MCPServerType.Builtin or MCPServerType.Plugin
                    ? Directory.GetCurrentDirectory()
                    : null
            };
            Servers.Add(server);
        }

        PersistToClaudeJSONAsync().SafeFireAndForget("MCPManagerViewModel.SaveServer");
        ResetForm();
    }

    /// <summary>
    /// Remove a server from the collection and clear its connection result.
    /// Persists the change to ~/.claude.json asynchronously.
    /// </summary>
    [RelayCommand]
    public void DeleteServer(MCPServerConfig server)
    {
        var toRemove = Servers.Where(s => s.Id == server.Id).ToList();
        foreach (var s in toRemove)
            Servers.Remove(s);

        _connectionResults.TryRemove(server.Id, out _);

        // Fire-and-forget persist (Swift: Task { await persistToClaudeJSON() })
        PersistToClaudeJSONAsync().SafeFireAndForget("MCPManagerViewModel.DeleteServer");
    }

    // ── Test Connection ──────────────────────────────────────

    /// <summary>
    /// Test whether an MCP server is reachable. Sets the connection
    /// result dictionary entry to Testing, then updates to Success / Failure.
    /// </summary>
    [RelayCommand]
    public async Task TestServerConnectionAsync(MCPServerConfig server)
    {
        _connectionResults[server.Id] = MCPConnectionResult.Testing();
        var result = await _mcpService.TestConnectionAsync(server).ConfigureAwait(true);
        _connectionResults[server.Id] = result;
    }

    /// <summary>
    /// Look up the cached connection-test result for a server.
    /// Returns <see cref="MCPConnectionResult.Unknown"/> when no test has been run.
    /// </summary>
    public MCPConnectionResult ConnectionResultFor(MCPServerConfig server)
    {
        return _connectionResults.TryGetValue(server.Id, out var result)
            ? result
            : MCPConnectionResult.Unknown();
    }

    // ── Persist to ~/.claude.json ────────────────────────────

    /// <summary>
    /// Write all servers to ~/.claude.json, separating top-level servers
    /// from project-level servers. Preserves all other existing keys in the file.
    /// </summary>
    private Task PersistToClaudeJSONAsync()
    {
        var claudePath = _configFileService.ClaudeGlobalConfigPath;
        var currentProjectPath = Directory.GetCurrentDirectory();

        // Read existing .claude.json — preserve every key we don't own
        Dictionary<string, JsonElement> rootDict;
        try
        {
            rootDict = _configFileService.ReadJSON(claudePath)
                       ?? new Dictionary<string, JsonElement>();
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("MCPManagerViewModel.PersistToClaudeJSON", ex);
            rootDict = new Dictionary<string, JsonElement>();
        }

        Dictionary<string, JsonElement> projects;
        if (rootDict.TryGetValue("projects", out var existingProjects) &&
            existingProjects.ValueKind == JsonValueKind.Object)
        {
            projects = SharedHelpers.EnumerateJsonObject(existingProjects) ?? new();
        }
        else
        {
            projects = new Dictionary<string, JsonElement>();
        }

        // Separate top-level and project-level servers
        var topLevelServers = new Dictionary<string, object>();
        var projectServersByPath = new Dictionary<string, Dictionary<string, object>>();
        var projectPluginStatesByPath = new Dictionary<string, Dictionary<string, bool>>();
        var touchedProjectPaths = new HashSet<string>(projects.Keys, StringComparer.OrdinalIgnoreCase)
        {
            currentProjectPath
        };

        foreach (var server in Servers)
        {
            if (!string.IsNullOrEmpty(server.ProjectPath) ||
                server.ServerType is MCPServerType.Builtin or MCPServerType.Plugin)
            {
                var projectPath = string.IsNullOrEmpty(server.ProjectPath)
                    ? currentProjectPath
                    : server.ProjectPath;
                touchedProjectPaths.Add(projectPath);

                if (server.ServerType is MCPServerType.Builtin or MCPServerType.Plugin)
                {
                    if (!projectPluginStatesByPath.TryGetValue(projectPath, out var pluginStates))
                    {
                        pluginStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                        projectPluginStatesByPath[projectPath] = pluginStates;
                    }

                    pluginStates[server.Name] = server.Enabled;
                }
                else
                {
                    var serverDict = ServerToDictionary(server);
                    if (!projectServersByPath.ContainsKey(projectPath))
                        projectServersByPath[projectPath] = new Dictionary<string, object>();
                    projectServersByPath[projectPath][server.Name] = serverDict;
                }
            }
            else
            {
                topLevelServers[server.Name] = ServerToDictionary(server);
            }
        }

        rootDict["mcpServers"] = JsonSerializer.SerializeToElement(topLevelServers);

        foreach (var projectPath in touchedProjectPaths)
        {
            Dictionary<string, JsonElement> projectData;
            if (projects.TryGetValue(projectPath, out var existingProject) &&
                existingProject.ValueKind == JsonValueKind.Object)
            {
                projectData = SharedHelpers.EnumerateJsonObject(existingProject) ?? new();
            }
            else
            {
                projectData = new Dictionary<string, JsonElement>();
            }

            if (projectServersByPath.TryGetValue(projectPath, out var servers) && servers.Count > 0)
            {
                projectData["mcpServers"] = JsonSerializer.SerializeToElement(servers);
            }
            else
            {
                projectData.Remove("mcpServers");
            }

            if (projectPluginStatesByPath.TryGetValue(projectPath, out var pluginStates) &&
                pluginStates.Count > 0)
            {
                var enabledServers = pluginStates.Where(static kvp => kvp.Value).Select(static kvp => kvp.Key).ToList();
                var disabledServers = pluginStates.Where(static kvp => !kvp.Value).Select(static kvp => kvp.Key).ToList();

                if (enabledServers.Count > 0)
                    projectData["enabledMcpjsonServers"] = JsonSerializer.SerializeToElement(enabledServers);
                else
                    projectData.Remove("enabledMcpjsonServers");

                if (disabledServers.Count > 0)
                    projectData["disabledMcpjsonServers"] = JsonSerializer.SerializeToElement(disabledServers);
                else
                    projectData.Remove("disabledMcpjsonServers");
            }
            else if (projectPath.Equals(currentProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                projectData.Remove("enabledMcpjsonServers");
                projectData.Remove("disabledMcpjsonServers");
            }

            if (projectData.Count > 0)
                projects[projectPath] = JsonSerializer.SerializeToElement(projectData);
            else
                projects.Remove(projectPath);
        }

        if (projects.Count > 0)
            rootDict["projects"] = JsonSerializer.SerializeToElement(projects);
        else
            rootDict.Remove("projects");

        try
        {
            _configFileService.WriteJSON(rootDict, claudePath);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存到 .claude.json 失败: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Convert a server config to a JSON-compatible dictionary, matching the
    /// mcpServers entry schema used by Claude Code (type, command/url, args, env).
    /// </summary>
    private static Dictionary<string, object> ServerToDictionary(MCPServerConfig server)
    {
        var dict = new Dictionary<string, object>
        {
            ["type"] = server.ServerType == MCPServerType.Sse ? "sse" : "stdio",
            ["enabled"] = server.Enabled
        };

        if (server.ServerType == MCPServerType.Sse)
        {
            dict["url"] = server.Url;
        }
        else
        {
            dict["command"] = server.Command;
            if (server.Args.Count > 0)
                dict["args"] = server.Args;
            if (server.Env.Count > 0)
                dict["env"] = server.Env;
        }

        return dict;
    }

    // ── Form helpers ─────────────────────────────────────────

    /// <summary>Clear all form fields and exit add/edit mode.</summary>
    [RelayCommand]
    public void ResetForm()
    {
        NewName = "";
        NewCommand = "";
        NewUrl = "";
        NewServerType = MCPServerType.Stdio;
        NewEnabled = true;
        NewArgs = new List<string>();
        NewEnv = new List<(string, string)>();
        NewArgInput = "";
        NewEnvKeyInput = "";
        NewEnvValueInput = "";
        EditingServer = null;
        IsAddingServer = false;
    }

    /// <summary>Remove an argument at the given index from the new-args list.</summary>
    [RelayCommand]
    public void RemoveArg(int index)
    {
        if (index >= 0 && index < NewArgs.Count)
            NewArgs.RemoveAt(index);
    }

    /// <summary>Remove an environment variable pair at the given index.</summary>
    [RelayCommand]
    public void RemoveEnv(int index)
    {
        if (index >= 0 && index < NewEnv.Count)
            NewEnv.RemoveAt(index);
    }

    /// <summary>
    /// Append the current arg-input text to the new-args list, then clear the input.
    /// Empty / whitespace-only input is ignored.
    /// </summary>
    [RelayCommand]
    public void AddArg()
    {
        var trimmed = NewArgInput.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            NewArgs.Add(trimmed);
            NewArgInput = "";
        }
    }

    /// <summary>
    /// Append the current env key/value inputs as a new pair, then clear both inputs.
    /// Pairs with an empty key are ignored.
    /// </summary>
    [RelayCommand]
    public void AddEnvPair()
    {
        var key = NewEnvKeyInput.Trim();
        var value = NewEnvValueInput.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            NewEnv.Add((key, value));
            NewEnvKeyInput = "";
            NewEnvValueInput = "";
        }
    }
}
