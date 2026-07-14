using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeCodePanel.Windows.Models;

public partial class MCPServerConfig : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public string PersistentKey => BuildPersistentKey(Name, ServerType, Command, Url, Args, Env, ProjectPath);

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private MCPServerType _serverType = MCPServerType.Stdio;

    [ObservableProperty]
    private string _command = "";

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private List<string> _args = new();

    [ObservableProperty]
    private Dictionary<string, string> _env = new();

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private MCPServerStatus _status = MCPServerStatus.Stopped;

    [ObservableProperty]
    private string? _projectPath;

    public static MCPServerConfig? FromJson(Dictionary<string, JsonElement> dict)
    {
        if (!dict.TryGetValue("name", out var nameElement))
            return null;

        var name = nameElement.GetString() ?? "";
        var typeStr = "stdio";
        if (dict.TryGetValue("type", out var typeElement))
            typeStr = typeElement.GetString() ?? "stdio";

        bool enabled = true;
        if (dict.TryGetValue("enabled", out var enElement))
            enabled = enElement.GetBoolean();

        if (typeStr == "sse")
        {
            if (!dict.TryGetValue("url", out var urlElement))
                return null;
            return new MCPServerConfig
            {
                Name = name,
                ServerType = MCPServerType.Sse,
                Command = "sse",
                Url = urlElement.GetString() ?? "",
                Enabled = enabled
            };
        }

        if (!dict.TryGetValue("command", out var cmdElement))
            return null;

        var config = new MCPServerConfig
        {
            Name = name,
            ServerType = MCPServerType.Stdio,
            Command = cmdElement.GetString() ?? "",
            Enabled = enabled
        };

        if (dict.TryGetValue("args", out var argsElement))
        {
            foreach (var arg in argsElement.EnumerateArray())
            {
                var a = arg.GetString();
                if (a != null) config.Args.Add(a);
            }
        }

        if (dict.TryGetValue("env", out var envElement))
        {
            foreach (var kvp in envElement.EnumerateObject())
            {
                config.Env[kvp.Name] = kvp.Value.GetString() ?? "";
            }
        }

        return config;
    }

    public Dictionary<string, object> ToDictionary()
    {
        if (ServerType == MCPServerType.Sse)
        {
            return new Dictionary<string, object>
            {
                ["name"] = Name,
                ["type"] = "sse",
                ["url"] = Url,
                ["enabled"] = Enabled
            };
        }

        return new Dictionary<string, object>
        {
            ["name"] = Name,
            ["type"] = "stdio",
            ["command"] = Command,
            ["args"] = Args,
            ["env"] = Env,
            ["enabled"] = Enabled
        };
    }

    public override bool Equals(object? obj) =>
        obj is MCPServerConfig other && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();

    internal static string BuildPersistentKey(
        string? name,
        MCPServerType serverType,
        string? command,
        string? url,
        IEnumerable<string>? args,
        IReadOnlyDictionary<string, string>? env,
        string? projectPath)
    {
        static string Normalize(string? value) => (value ?? "").Trim();

        var normalizedArgs = string.Join("\u001f", (args ?? Array.Empty<string>()).Select(Normalize));
        var normalizedEnv = string.Join(
            "\u001f",
            (env ?? new Dictionary<string, string>())
                .OrderBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static kvp => kvp.Value, StringComparer.Ordinal)
                .Select(static kvp => $"{kvp.Key.Trim()}={kvp.Value.Trim()}"));

        return string.Join(
            "\u001e",
            Normalize(projectPath),
            serverType.ToString(),
            Normalize(name),
            Normalize(command),
            Normalize(url),
            normalizedArgs,
            normalizedEnv);
    }
}

public enum MCPServerType
{
    Stdio,
    Sse,
    Builtin,
    Plugin
}

public static class MCPServerTypeExtensions
{
    public static string Label(this MCPServerType type) => type switch
    {
        MCPServerType.Stdio => "STDIO",
        MCPServerType.Sse => "SSE",
        MCPServerType.Builtin => "内置",
        MCPServerType.Plugin => "插件",
        _ => ""
    };
}

public enum MCPServerStatus
{
    Running,
    Stopped,
    Error,
    Starting,
    Stopping
}

public static class MCPServerStatusExtensions
{
    public static string Label(this MCPServerStatus status) => status switch
    {
        MCPServerStatus.Running => "运行中",
        MCPServerStatus.Starting => "启动中...",
        MCPServerStatus.Stopping => "停止中...",
        MCPServerStatus.Stopped => "已停止",
        MCPServerStatus.Error => "错误",
        _ => ""
    };

    public static IndicatorStatus ToIndicatorStatus(this MCPServerStatus status) => status switch
    {
        MCPServerStatus.Running => IndicatorStatus.Running,
        MCPServerStatus.Starting => IndicatorStatus.Running,
        MCPServerStatus.Stopping => IndicatorStatus.Stopped,
        MCPServerStatus.Stopped => IndicatorStatus.Stopped,
        MCPServerStatus.Error => IndicatorStatus.Error,
        _ => IndicatorStatus.Stopped
    };
}

// MCPConnectionResult — equivalent to the Swift enum with associated values
public record MCPConnectionResult
{
    public MCPConnectionState State { get; init; }
    public string Message { get; init; } = "";

    public static MCPConnectionResult Unknown() => new() { State = MCPConnectionState.Unknown };
    public static MCPConnectionResult Testing() => new() { State = MCPConnectionState.Testing };
    public static MCPConnectionResult Success(string msg) => new() { State = MCPConnectionState.Success, Message = msg };
    public static MCPConnectionResult Failure(string msg) => new() { State = MCPConnectionState.Failure, Message = msg };

    public string Label => State switch
    {
        MCPConnectionState.Unknown => "未测试",
        MCPConnectionState.Testing => "测试中…",
        MCPConnectionState.Success => Message,
        MCPConnectionState.Failure => Message,
        _ => ""
    };

    public IndicatorStatus IndicatorStatus => State switch
    {
        MCPConnectionState.Unknown => IndicatorStatus.Stopped,
        MCPConnectionState.Testing => IndicatorStatus.Running,
        MCPConnectionState.Success => IndicatorStatus.Running,
        MCPConnectionState.Failure => IndicatorStatus.Error,
        _ => IndicatorStatus.Stopped
    };
}

public enum MCPConnectionState
{
    Unknown,
    Testing,
    Success,
    Failure
}
