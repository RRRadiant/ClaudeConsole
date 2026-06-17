using System;
using System.Collections.Concurrent;
using System.IO;
using System.Timers;
using System.Windows;

namespace ClaudeCodePanel.Windows.Services;

public sealed class FileWatcherService
{
    public static FileWatcherService Instance { get; } = new();

    private readonly ConcurrentDictionary<string, WatcherEntry> _entries = new();

    /// <summary>Debounce window in milliseconds. Prevents duplicate firings from
    /// FileSystemWatcher raising multiple Changed events per save.</summary>
    private const int DebounceMilliseconds = 500;

    /// <summary>Fires on the UI thread after a watched file changes (debounced).</summary>
    public event Action<string>? OnChange;

    private FileWatcherService() { }

    // ── Public API ──────────────────────────────────────────

    /// <summary>Start watching <paramref name="filePath"/>. Stops any prior
    /// watcher for the same path first.</summary>
    public void Watch(string filePath)
    {
        StopWatching(filePath);

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;
        if (string.IsNullOrEmpty(fileName))
            return;

        var fsw = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite
                         | NotifyFilters.FileName
                         | NotifyFilters.Size,
            EnableRaisingEvents = false
        };

        var entry = new WatcherEntry(fsw, filePath, path =>
        {
            Application.Current?.Dispatcher.Invoke(() => OnChange?.Invoke(path));
        });

        _entries[filePath] = entry;
    }

    /// <summary>Stop watching a single file and release its watcher.</summary>
    public void StopWatching(string filePath)
    {
        if (_entries.TryRemove(filePath, out var entry))
        {
            entry.Dispose();
        }
    }

    /// <summary>Stop all watchers and release every resource.</summary>
    public void StopAll()
    {
        foreach (var kvp in _entries)
        {
            kvp.Value.Dispose();
        }
        _entries.Clear();
    }

    /// <summary>Convenience: watch settings.json, settings.local.json,
    /// and mcp.json from the configured Claude directory.</summary>
    public void WatchAllConfigFiles()
    {
        var paths = new[]
        {
            ConfigFileService.Instance.SettingsPath,
            ConfigFileService.Instance.SettingsLocalPath,
            ConfigFileService.Instance.McpPath,
        };
        foreach (var path in paths)
        {
            Watch(path);
        }
    }

    // ── Nested: WatcherEntry ────────────────────────────────

    /// <summary>Pairs a FileSystemWatcher with a debounce timer for a
    /// single file path. Disposing the entry cleanly tears down both.</summary>
    private sealed class WatcherEntry : IDisposable
    {
        private readonly object _lock = new();
        private readonly FileSystemWatcher _watcher;
        private readonly string _filePath;
        private readonly Action<string> _notify;

        private Timer? _debounceTimer;
        private bool _disposed;

        public WatcherEntry(
            FileSystemWatcher watcher,
            string filePath,
            Action<string> notify)
        {
            _watcher = watcher;
            _filePath = filePath;
            _notify = notify;

            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnFileEvent;

            watcher.EnableRaisingEvents = true;
        }

        // ── Event handler (thread-pool thread) ──────────────

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                if (_debounceTimer == null)
                {
                    _debounceTimer = new Timer(DebounceMilliseconds)
                    {
                        AutoReset = false
                    };
                    _debounceTimer.Elapsed += OnDebounceElapsed;
                }

                // Reset the clock — stop and restart
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void OnDebounceElapsed(object? sender, ElapsedEventArgs e)
        {
            lock (_lock)
            {
                if (_disposed) return;
            }
            _notify(_filePath);
        }

        // ── Dispose ─────────────────────────────────────────

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                _disposed = true;

                _watcher.EnableRaisingEvents = false;

                _watcher.Changed -= OnFileEvent;
                _watcher.Created -= OnFileEvent;
                _watcher.Deleted -= OnFileEvent;
                _watcher.Renamed -= OnFileEvent;

                _watcher.Dispose();

                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }
        }
    }
}
