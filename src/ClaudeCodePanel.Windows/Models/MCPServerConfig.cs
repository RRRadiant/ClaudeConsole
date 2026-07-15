using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeCodePanel.Windows.Models;

public partial class MCPServerConfig : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public string PersistentKey => BuildPersistentKey(Name, ServerType, Command, Url, Args, Env, ProjectPath);
    public Dictionary<string, JsonElement> AdditionalProperties { get; } = new(StringComparer.Ordinal);

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
            var sseConfig = new MCPServerConfig
            {
                Name = name,
                ServerType = MCPServerType.Sse,
                Command = "sse",
                Url = urlElement.GetString() ?? "",
                Enabled = enabled
            };
            CopyAdditionalProperties(dict, sseConfig);
            return sseConfig;
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

        CopyAdditionalProperties(dict, config);
        return config;
    }

    public Dictionary<string, object> ToDictionary()
    {
        var result = AdditionalProperties.ToDictionary(
            static pair => pair.Key,
            static pair => (object)pair.Value.Clone(),
            StringComparer.Ordinal);

        if (ServerType == MCPServerType.Sse)
        {
            result["name"] = Name;
            result["type"] = "sse";
            result["url"] = Url;
            result["enabled"] = Enabled;
            return result;
        }

        result["name"] = Name;
        result["type"] = "stdio";
        result["command"] = Command;
        result["args"] = Args;
        result["env"] = Env;
        result["enabled"] = Enabled;
        return result;
    }

    private static void CopyAdditionalProperties(
        IReadOnlyDictionary<string, JsonElement> source,
        MCPServerConfig destination)
    {
        var knownProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name", "type", "command", "url", "args", "env", "enabled"
        };

        foreach (var pair in source)
        {
            if (!knownProperties.Contains(pair.Key))
                destination.AdditionalProperties[pair.Key] = pair.Value.Clone();
        }
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

        // A server name is unique within its configuration scope. Connection details are
        // deliberately excluded because arguments, URLs, and environment values may contain
        // credentials and should never be persisted as part of a display-name lookup key.
        var identity = string.Join(
            "\u001e",
            Normalize(projectPath),
            serverType.ToString(),
            Normalize(name));

        return $"v2:{Hash(identity)}";
    }

    internal static string NormalizePersistentKey(string persistedKey)
    {
        if (IsHashedKey(persistedKey, "v2:") || IsHashedKey(persistedKey, "legacy:"))
            return persistedKey;

        // Legacy keys used seven record-separator-delimited fields. Only the first three
        // (scope, type, and name) are needed to derive the new non-sensitive identity.
        var fields = persistedKey.Split('\u001e');
        if (fields.Length >= 3 &&
            Enum.TryParse<MCPServerType>(fields[1], ignoreCase: true, out var serverType))
        {
            return BuildPersistentKey(
                fields[2],
                serverType,
                command: null,
                url: null,
                args: null,
                env: null,
                fields[0]);
        }

        // Unknown historical formats cannot be mapped back to a server reliably. Hashing them
        // preserves the alias entry without carrying potentially sensitive plaintext forward.
        return $"legacy:{Hash(persistedKey)}";
    }

    private static bool IsHashedKey(string value, string prefix)
    {
        if (value.Length != prefix.Length + 64 ||
            !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = prefix.Length; index < value.Length; index++)
        {
            if (!char.IsAsciiHexDigit(value[index]))
                return false;
        }

        return true;
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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
