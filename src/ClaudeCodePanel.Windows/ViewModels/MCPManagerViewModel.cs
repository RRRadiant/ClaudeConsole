using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
    private readonly ConfigFileService _configFileService;
    private readonly MCPService _mcpService;

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

    /// <summary>Per-server connection test results.</summary>
    [ObservableProperty]
    private Dictionary<Guid, MCPConnectionResult> _connectionResults = new();

    // ── Form fields ──────────────────────────────────────────

    [ObservableProperty]
    private string _newName = "";

    [ObservableProperty]
    private string _newCommand = "";

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

    public MCPManagerViewModel()
    {
        _configFileService = ConfigFileService.Instance;
        _mcpService = MCPService.Instance;
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

                        var serverDict = EnumerateJsonObject(serverElement);
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
        catch
        {
            // Ignore errors reading mcp.json
        }

        Servers = new ObservableCollection<MCPServerConfig>(mergedServers);
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
        NewCommand = server.Command;
        NewArgs = new List<string>(server.Args);
        NewEnv = server.Env.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        IsAddingServer = true;
    }

    /// <summary>
    /// Save the current form as either an edited existing server or a new server,
    /// then persist everything to ~/.claude.json.
    /// </summary>
    [RelayCommand]
    public Task SaveServerAsync()
    {
        MCPServerConfig server;
        if (EditingServer != null)
        {
            server = EditingServer;
            server.Name = NewName;
            server.Command = NewCommand;
            server.Args = NewArgs.Where(a => !string.IsNullOrEmpty(a)).ToList();
            server.Env = NewEnv
                .Where(e => !string.IsNullOrEmpty(e.Key))
                .ToDictionary(e => e.Key, e => e.Value);
        }
        else
        {
            server = new MCPServerConfig
            {
                Name = NewName,
                Command = NewCommand,
                Args = NewArgs.Where(a => !string.IsNullOrEmpty(a)).ToList(),
                Env = NewEnv
                    .Where(e => !string.IsNullOrEmpty(e.Key))
                    .ToDictionary(e => e.Key, e => e.Value),
            };
            Servers.Add(server);
        }

        PersistToClaudeJSON();
        ResetForm();
        return Task.CompletedTask;
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

        ConnectionResults.Remove(server.Id);

        // Fire-and-forget persist (Swift: Task { await persistToClaudeJSON() })
        PersistToClaudeJSON();
    }

    // ── Test Connection ──────────────────────────────────────

    /// <summary>
    /// Test whether an MCP server is reachable. Sets the connection
    /// result dictionary entry to Testing, then updates to Success / Failure.
    /// </summary>
    [RelayCommand]
    public async Task TestServerConnectionAsync(MCPServerConfig server)
    {
        ConnectionResults[server.Id] = MCPConnectionResult.Testing();
        var result = await _mcpService.TestConnectionAsync(server);
        ConnectionResults[server.Id] = result;
    }

    /// <summary>
    /// Look up the cached connection-test result for a server.
    /// Returns <see cref="MCPConnectionResult.Unknown"/> when no test has been run.
    /// </summary>
    public MCPConnectionResult ConnectionResultFor(MCPServerConfig server)
    {
        return ConnectionResults.TryGetValue(server.Id, out var result)
            ? result
            : MCPConnectionResult.Unknown();
    }

    // ── Persist to ~/.claude.json ────────────────────────────

    /// <summary>
    /// Write all servers to ~/.claude.json, separating top-level servers
    /// from project-level servers. Preserves all other existing keys in the file.
    /// </summary>
    private void PersistToClaudeJSON()
    {
        var claudePath = _configFileService.ClaudeGlobalConfigPath;

        // Read existing .claude.json — preserve every key we don't own
        Dictionary<string, JsonElement> rootDict;
        try
        {
            rootDict = _configFileService.ReadJSON(claudePath)
                       ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            rootDict = new Dictionary<string, JsonElement>();
        }

        // Separate top-level and project-level servers
        var topLevelServers = new Dictionary<string, object>();
        var projectServersByPath = new Dictionary<string, Dictionary<string, object>>();

        foreach (var server in Servers)
        {
            var serverDict = ServerToDictionary(server);
            if (!string.IsNullOrEmpty(server.ProjectPath))
            {
                if (!projectServersByPath.ContainsKey(server.ProjectPath))
                    projectServersByPath[server.ProjectPath] = new Dictionary<string, object>();
                projectServersByPath[server.ProjectPath][server.Name] = serverDict;
            }
            else
            {
                topLevelServers[server.Name] = serverDict;
            }
        }

        rootDict["mcpServers"] = JsonSerializer.SerializeToElement(topLevelServers);

        // Update project-level mcpServers inside the "projects" key
        if (projectServersByPath.Count > 0)
        {
            Dictionary<string, JsonElement> projects;
            if (rootDict.TryGetValue("projects", out var existingProjects) &&
                existingProjects.ValueKind == JsonValueKind.Object)
            {
                projects = EnumerateJsonObject(existingProjects) ?? new();
            }
            else
            {
                projects = new Dictionary<string, JsonElement>();
            }

            foreach (var (projectPath, servers) in projectServersByPath)
            {
                Dictionary<string, JsonElement> projectData;
                if (projects.TryGetValue(projectPath, out var existingProject) &&
                    existingProject.ValueKind == JsonValueKind.Object)
                {
                    projectData = EnumerateJsonObject(existingProject) ?? new();
                }
                else
                {
                    projectData = new Dictionary<string, JsonElement>();
                }

                projectData["mcpServers"] = JsonSerializer.SerializeToElement(servers);
                projects[projectPath] = JsonSerializer.SerializeToElement(projectData);
            }

            rootDict["projects"] = JsonSerializer.SerializeToElement(projects);
        }

        try
        {
            _configFileService.WriteJSON(rootDict, claudePath);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存到 .claude.json 失败: {ex.Message}";
        }
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

    /// <summary>
    /// Enumerate a JsonElement of kind Object into a Dictionary without
    /// a serialize-deserialize round-trip.
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
}
