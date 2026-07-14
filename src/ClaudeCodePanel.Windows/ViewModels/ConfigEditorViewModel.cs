using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeCodePanel.Windows.ViewModels;

/// <summary>
/// Config Editor ViewModel — port of ConfigEditorViewModel.swift.
/// Manages the list of config files, file selection, content editing,
/// saving with mtime-based conflict detection, and conflict resolution.
/// </summary>
public partial class ConfigEditorViewModel : ObservableObject
{
    private readonly IConfigFileService _configFileService;
    private DateTime? _lastSavedMtime;

    // ──────────────────────────────────────────────
    //  Observable bindable properties
    // ──────────────────────────────────────────────

    [ObservableProperty]
    private List<ConfigFileInfo> _files = new();

    [ObservableProperty]
    private ConfigFileInfo? _selectedFile;

    [ObservableProperty]
    private string _fileContent = "";

    [ObservableProperty]
    private string _originalContent = "";

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private string _conflictRemoteContent = "";

    // ──────────────────────────────────────────────
    //  Constructor
    // ──────────────────────────────────────────────

    public ConfigEditorViewModel(IConfigFileService? configFileService = null)
    {
        _configFileService = configFileService ?? ConfigFileService.Instance;
    }

    // ──────────────────────────────────────────────
    //  File list loading
    // ──────────────────────────────────────────────

    /// <summary>
    /// Load the list of config files from the service.
    /// Auto-selects the first file if nothing is currently selected.
    /// </summary>
    [RelayCommand]
    public void LoadFileList()
    {
        Files = _configFileService.ListConfigFiles();
        if (SelectedFile == null && Files.Count > 0)
        {
            SelectFile(Files[0]);
        }
    }

    // ──────────────────────────────────────────────
    //  File selection
    // ──────────────────────────────────────────────

    /// <summary>
    /// Select a config file and load its content.
    /// </summary>
    /// <param name="file">The file to select and load.</param>
    [RelayCommand]
    public void SelectFile(ConfigFileInfo file)
    {
        SelectedFile = file;
        LoadContent(file);
    }

    // ──────────────────────────────────────────────
    //  Content loading
    // ──────────────────────────────────────────────

    /// <summary>
    /// Read the content of the selected file, record original content
    /// and last-modified time for modification tracking and conflict detection.
    /// </summary>
    /// <param name="file">The file whose content to load.</param>
    private void LoadContent(ConfigFileInfo file)
    {
        IsLoading = true;
        try
        {
            var content = ConfigFileService.ReadFile(file.Path);
            FileContent = content;
            OriginalContent = content;
            IsModified = false;
            HasConflict = false;

            var fileInfo = new FileInfo(file.Path);
            _lastSavedMtime = fileInfo.LastWriteTimeUtc;

            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            FileContent = "";
            OriginalContent = "";
        }
        IsLoading = false;
    }

    // ──────────────────────────────────────────────
    //  Saving
    // ──────────────────────────────────────────────

    /// <summary>
    /// Save changes to the selected file.
    /// For .json files: validates JSON and writes via the config service
    /// with mtime-based conflict detection.
    /// For other files: writes content directly.
    /// </summary>
    [RelayCommand]
    public Task SaveChangesAsync()
    {
        if (SelectedFile == null) return Task.CompletedTask;

        try
        {
            var isJsonFile = SelectedFile.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

            if (isJsonFile)
            {
                // Validate JSON and ensure it is an object
                using var doc = JsonDocument.Parse(FileContent);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ConfigFileException(
                        ConfigFileError.InvalidJSON,
                        "Expected JSON object");
                }

                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(FileContent);
                if (dict is null)
                {
                    throw new ConfigFileException(
                        ConfigFileError.InvalidJSON,
                        "Expected JSON object");
                }

                _configFileService.WriteJSON(dict, SelectedFile.Path, _lastSavedMtime);
            }
            else
            {
                _configFileService.WriteText(FileContent, SelectedFile.Path, _lastSavedMtime);
            }

            // Update tracking state after successful save
            OriginalContent = FileContent;
            IsModified = false;
            ErrorMessage = null;
            HasConflict = false;

            var fileInfo = new FileInfo(SelectedFile.Path);
            _lastSavedMtime = fileInfo.LastWriteTimeUtc;
        }
        catch (ConfigFileException ex) when (ex.Error == ConfigFileError.ConflictDetected)
        {
            HasConflict = true;
            try
            {
                ConflictRemoteContent = ConfigFileService.ReadFile(SelectedFile.Path);
            }
            catch
            {
                ConflictRemoteContent = "";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    //  Conflict resolution
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resolve a detected file conflict.
    /// </summary>
    /// <param name="useRemote">
    /// If true, accept the remote (on-disk) content, overwriting local edits.
    /// If false, keep local content and reset the mtime to bypass the conflict check.
    /// </param>
    [RelayCommand]
    public void ResolveConflict(bool useRemote)
    {
        if (!useRemote)
        {
            // Keep local changes — adopt the current on-disk mtime so the next
            // save overwrites the version we just reviewed, not the stale one.
            _lastSavedMtime = SelectedFile != null && File.Exists(SelectedFile.Path)
                ? new FileInfo(SelectedFile.Path).LastWriteTimeUtc
                : null;
        }
        else
        {
            // Accept the on-disk version, discarding local edits.
            FileContent = ConflictRemoteContent;
            OriginalContent = ConflictRemoteContent;
            IsModified = false;
            _lastSavedMtime = SelectedFile != null && File.Exists(SelectedFile.Path)
                ? new FileInfo(SelectedFile.Path).LastWriteTimeUtc
                : null;
        }
        HasConflict = false;
        ErrorMessage = null;
    }

    // ──────────────────────────────────────────────
    //  Content change tracking
    // ──────────────────────────────────────────────

    /// <summary>
    /// Called automatically by the source generator whenever
    /// <see cref="FileContent"/> changes. Updates the modified flag
    /// by comparing against original content.
    /// </summary>
    partial void OnFileContentChanged(string value)
    {
        IsModified = value != OriginalContent;
    }
}
