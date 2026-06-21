using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClaudeCodePanel.Windows.Services;

// ─── Enums ───────────────────────────────────────────────

public enum ConfigFileError
{
    FileNotFound,
    InvalidJSON,
    ConflictDetected
}

// ─── Error type ──────────────────────────────────────────

public sealed class ConfigFileException : Exception
{
    public ConfigFileError Error { get; }

    public ConfigFileException(ConfigFileError error, string message)
        : base(message)
    {
        Error = error;
    }
}

// ─── Config file type ────────────────────────────────────

public enum ConfigFileTypeKind
{
    Config,
    SpecificConfig
}

public sealed class ConfigFileType
{
    public ConfigFileTypeKind Kind { get; }
    public string? Identifier { get; }

    private ConfigFileType(ConfigFileTypeKind kind, string? identifier = null)
    {
        Kind = kind;
        Identifier = identifier;
    }

    public static ConfigFileType Config { get; } = new(ConfigFileTypeKind.Config);
    public static ConfigFileType SpecificConfig(string name) => new(ConfigFileTypeKind.SpecificConfig, name);

    public string IconGlyph =>
        Kind switch
        {
            ConfigFileTypeKind.SpecificConfig => Identifier switch
            {
                "settings.json" => "",       // gearshape / Settings
                "settings.local.json" => "",  // gearshape / Settings
                "mcp.json" => "",             // server.rack
                _ => ""                       // doc.text
            },
            _ => ""                           // doc.text
        };

    public string DisplayName =>
        Kind switch
        {
            ConfigFileTypeKind.SpecificConfig => Identifier switch
            {
                "claude.json" => "Claude Global",
                "settings.json" => "Settings",
                "settings.local.json" => "Local Settings",
                "mcp.json" => "MCP Config",
                _ => Identifier ?? "Unknown"
            },
            _ => "Config"
        };

    public string RawValue =>
        Kind switch
        {
            ConfigFileTypeKind.SpecificConfig => Identifier ?? "config",
            _ => "config"
        };

    public override bool Equals(object? obj) =>
        obj is ConfigFileType other &&
        Kind == other.Kind &&
        Identifier == other.Identifier;

    public override int GetHashCode() =>
        HashCode.Combine(Kind, Identifier);

    public static bool operator ==(ConfigFileType? left, ConfigFileType? right) =>
        Equals(left, right);

    public static bool operator !=(ConfigFileType? left, ConfigFileType? right) =>
        !Equals(left, right);
}

// ─── Config file info record ─────────────────────────────

public sealed class ConfigFileInfo
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public string Path { get; }
    public ConfigFileType Type { get; }
    public DateTime LastModified { get; }
    public long SizeBytes { get; }

    public ConfigFileInfo(
        string name,
        string path,
        ConfigFileType type,
        DateTime lastModified,
        long sizeBytes)
    {
        Name = name;
        Path = path;
        Type = type;
        LastModified = lastModified;
        SizeBytes = sizeBytes;
    }
}

// ─── Config file service ─────────────────────────────────

public sealed class ConfigFileService
{
    public static ConfigFileService Instance { get; } = new();

    private ConfigFileService() { }

    // ── Paths ────────────────────────────────────────────

    private string UserProfileDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private string ClaudeDirectory =>
        System.IO.Path.Combine(UserProfileDirectory, ".claude");

    public string SettingsPath =>
        System.IO.Path.Combine(ClaudeDirectory, "settings.json");

    public string SettingsLocalPath =>
        System.IO.Path.Combine(ClaudeDirectory, "settings.local.json");

    public string ClaudeGlobalConfigPath =>
        System.IO.Path.Combine(UserProfileDirectory, ".claude.json");

    public string McpPath =>
        System.IO.Path.Combine(ClaudeDirectory, "mcp.json");

    public string SkillsDirectory =>
        System.IO.Path.Combine(ClaudeDirectory, "skills");

    public string AgentsDirectory =>
        System.IO.Path.Combine(ClaudeDirectory, "agents");

    public string CommandsDirectory =>
        System.IO.Path.Combine(ClaudeDirectory, "commands");

    // ── Read / Write ─────────────────────────────────────

    public string ReadFile(string path)
    {
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Reads and deserializes a JSON file as Dictionary&lt;string, JsonElement&gt;.
    /// Returns null if the file doesn't exist or isn't a valid JSON object.
    /// Does NOT throw — callers that need exceptions should use <see cref="ReadJSON"/>.
    /// </summary>
    public Dictionary<string, JsonElement>? TryReadJSON(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            return dict;
        }
        catch
        {
            return null;
        }
    }

    public Dictionary<string, JsonElement>? ReadJSON(string path)
    {
        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (dict is null)
        {
            throw new ConfigFileException(
                ConfigFileError.InvalidJSON,
                $"Expected JSON object at {path}");
        }
        return dict;
    }

    public void WriteJSON(
        Dictionary<string, JsonElement> dict,
        string path,
        DateTime? expectedMtime = null)
    {
        // Mtime conflict check
        if (expectedMtime.HasValue && File.Exists(path))
        {
            var currentMtime = File.GetLastWriteTimeUtc(path);
            if (Math.Abs((currentMtime - expectedMtime.Value).TotalSeconds) > 0.1)
            {
                throw new ConfigFileException(
                    ConfigFileError.ConflictDetected,
                    $"File was modified externally: {path}");
            }
        }

        // Atomic write: write .tmp, delete original, move .tmp
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(dict, options);
        var tempPath = path + ".tmp";

        File.WriteAllText(tempPath, json);

        File.Move(tempPath, path, overwrite: true);
    }

    // ── Directory ────────────────────────────────────────

    public void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    // ── Listing ──────────────────────────────────────────

    public List<ConfigFileInfo> ListConfigFiles()
    {
        var files = new List<ConfigFileInfo>();

        // Include ~/.claude.json (global Claude config with MCP servers)
        var claudeGlobalPath = ClaudeGlobalConfigPath;
        if (File.Exists(claudeGlobalPath))
        {
            files.Add(FileInfoFor(
                claudeGlobalPath,
                ConfigFileType.SpecificConfig("claude.json")));
        }

        // Scan for all JSON, TOML, YAML files directly in ~/.claude/
        var knownFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "settings.json",
            "settings.local.json",
            "mcp.json",
            "claude.json"
        };

        if (Directory.Exists(ClaudeDirectory))
        {
            foreach (var filePath in Directory.EnumerateFiles(ClaudeDirectory))
            {
                var name = System.IO.Path.GetFileName(filePath);
                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

                // Only include known config extensions
                if (ext is not (".json" or ".toml" or ".yaml" or ".yml"))
                    continue;

                // Exclude hidden files
                if (name.StartsWith('.'))
                    continue;

                var type = knownFileNames.Contains(name)
                    ? ConfigFileType.SpecificConfig(name)
                    : ConfigFileType.Config;

                files.Add(FileInfoFor(filePath, type));
            }
        }

        files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    private static ConfigFileInfo FileInfoFor(string path, ConfigFileType type)
    {
        var info = new FileInfo(path);
        return new ConfigFileInfo(
            name: System.IO.Path.GetFileName(path),
            path: path,
            type: type,
            lastModified: info.LastWriteTimeUtc,
            sizeBytes: info.Exists ? info.Length : 0L);
    }
}
