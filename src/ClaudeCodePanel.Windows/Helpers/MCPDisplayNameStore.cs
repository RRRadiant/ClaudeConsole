using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Purely local display-name storage for MCP servers — never touches config files.
/// Stores display names in a JSON file at %LOCALAPPDATA%/ClaudeCodePanel/mcp_display_names.json.
/// Writes are debounced: at most one flush every 2 seconds.
/// </summary>
public static class MCPDisplayNameStore
{
    private static readonly ConcurrentDictionary<string, string> _displayNames = new(StringComparer.Ordinal);
    private static readonly string _storePath;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // Debounce: use a timer to coalesce rapid writes
    private static System.Timers.Timer? _saveTimer;
    private static readonly object _saveLock = new();
    private const int SaveDebounceMs = 2000;

    static MCPDisplayNameStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "ClaudeCodePanel");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "mcp_display_names.json");
        LoadFromDisk();
    }

    /// <summary>
    /// Returns the stored display name for the given stable server key, or null if none is set.
    /// </summary>
    public static string? DisplayName(string serverKey)
    {
        return _displayNames.TryGetValue(serverKey, out var name) ? name : null;
    }

    /// <summary>
    /// Sets or removes a display name for the given stable server key.
    /// Pass null or whitespace to remove the entry.
    /// </summary>
    public static void SetDisplayName(string? name, string serverKey)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _displayNames[serverKey] = name.Trim();
        }
        else
        {
            _displayNames.TryRemove(serverKey, out _);
        }
        SaveToDisk();
    }

    /// <summary>
    /// Returns the display name if one has been set, otherwise falls back to the server's Name.
    /// </summary>
    public static string EffectiveName(MCPServerConfig server)
    {
        return DisplayName(server.PersistentKey) ?? server.Name;
    }

    private static void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                var json = File.ReadAllText(_storePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    var migrated = false;
                    foreach (var kvp in dict)
                    {
                        var normalizedKey = MCPServerConfig.NormalizePersistentKey(kvp.Key);
                        _displayNames[normalizedKey] = kvp.Value;
                        migrated |= !string.Equals(normalizedKey, kvp.Key, StringComparison.Ordinal);
                    }

                    // Rewrite immediately so legacy keys containing arguments or environment
                    // values do not remain on disk until the user happens to rename a server.
                    if (migrated)
                        FlushToDisk();
                }
            }
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("MCPDisplayNameStore.LoadFromDisk", ex);
        }
    }

    private static void SaveToDisk()
    {
        lock (_saveLock)
        {
            if (_saveTimer == null)
            {
                _saveTimer = new System.Timers.Timer(SaveDebounceMs)
                {
                    AutoReset = false
                };
                _saveTimer.Elapsed += (_, _) =>
                {
                    FlushToDisk();
                };
            }

            // Reset the timer — each call postpones the flush
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    private static void FlushToDisk()
    {
        string? tempPath = null;
        try
        {
            var snapshot = new Dictionary<string, string>(_displayNames, StringComparer.Ordinal);
            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            tempPath = $"{_storePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _storePath, overwrite: true);
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("MCPDisplayNameStore.FlushToDisk", ex);
        }
        finally
        {
            if (tempPath != null && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    SharedHelpers.SafeLog("MCPDisplayNameStore.FlushToDisk.Cleanup", ex);
                }
            }
        }
    }
}
