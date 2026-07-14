using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeCodePanel.Windows.Views.Config;

/// <summary>
/// Config editor view — WPF port of ConfigEditorView.swift.
/// Displays a list of config file tabs, a monospace code editor with line numbers,
/// save/conflict-resolution flows, and empty states.
/// </summary>
public partial class ConfigEditorView : UserControl
{
    private ConfigEditorViewModel? _vm;

    // ── Color constants ─────────────────────────────────────────────────

    private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);  // #6faadd
    private static readonly Color ModifiedBorderYellow = Color.FromArgb(0x4D, 0xFF, 0xD6, 0x00);
    private static readonly Color CleanBorderWhite = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

    // ── Commands wired to GlassButton instances ──────────────────────────

    private ICommand? _saveCommand;
    private ICommand? _acceptLocalCommand;
    private ICommand? _acceptRemoteCommand;

    // ── Constructor ──────────────────────────────────────────────────────

    public ConfigEditorView()
    {
        InitializeComponent();

        // GlassButton does not expose a Click event — it executes its Command
        // property on MouseLeftButtonUp.  Wire each button to a code-behind
        // command that calls the appropriate handler.
        _saveCommand = new AsyncRelayCommand(OnSaveChangesAsync);
        _acceptLocalCommand = new RelayCommand(OnAcceptLocal);
        _acceptRemoteCommand = new RelayCommand(OnAcceptRemote);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Loaded ───────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetService(typeof(ConfigEditorViewModel)) as ConfigEditorViewModel;
        if (_vm == null) return;

        DataContext = _vm;
        _vm.LoadFileList();

        if (_vm.Files.Count == 0)
        {
            ShowNoFilesState();
            return;
        }

        BuildFileTabs();

        if (_vm.SelectedFile != null)
            ShowEditor();
        else
            ShowNoSelectionState();

        FileWatcherService.Instance.OnChange -= OnConfigFileChanged;
        FileWatcherService.Instance.OnChange += OnConfigFileChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        FileWatcherService.Instance.OnChange -= OnConfigFileChanged;
    }

    // ── File tabs ────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the horizontal file-tab strip from the ViewModel's file list.
    /// Each tab shows the file-type icon glyph and the file name.
    /// The selected tab receives an accent-tinted background and border.
    /// </summary>
    private void BuildFileTabs()
    {
        FileTabsPanel.Children.Clear();
        if (_vm == null) return;

        foreach (var file in _vm.Files)
        {
            var isSelected = _vm.SelectedFile?.Id == file.Id;

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(12, 7, 12, 7),
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(0x1E, AccentColor.R, AccentColor.G, AccentColor.B))
                    : Brushes.Transparent,
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromArgb(0x4D, AccentColor.R, AccentColor.G, AccentColor.B))
                    : new SolidColorBrush(Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = file
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            // Icon glyph
            stack.Children.Add(new TextBlock
            {
                Text = file.Type.IconGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = isSelected
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                VerticalAlignment = VerticalAlignment.Center
            });

            // File name
            stack.Children.Add(new TextBlock
            {
                Text = file.Name,
                FontSize = 15,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Regular,
                Foreground = isSelected
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            border.Child = stack;

            // Click handler — select the file and reload editor
            border.MouseLeftButtonDown += (s, args) =>
            {
                if (s is Border b && b.Tag is ConfigFileInfo f)
                {
                    _vm?.SelectFile(f);
                    BuildFileTabs();
                    ShowEditor();
                }
            };

            FileTabsPanel.Children.Add(border);
        }
    }

    // ── Editor content ───────────────────────────────────────────────────

    /// <summary>
    /// Populates the editor, line-number gutter, file-info bar, and
    /// save-status indicator from the currently selected file.
    /// </summary>
    private void ShowEditor()
    {
        if (_vm == null || _vm.SelectedFile == null) return;

        // Wire GlassButton commands (must be done after InitializeComponent)
        SaveButton.Command = _saveCommand!;
        AcceptLocalButton.Command = _acceptLocalCommand!;
        AcceptRemoteButton.Command = _acceptRemoteCommand!;

        // File info bar
        FileIcon.Text = _vm.SelectedFile.Type.IconGlyph;
        FilePath.Text = _vm.SelectedFile.Path;
        UpdateSaveStatus();

        // Editor content
        Editor.Text = _vm.FileContent;
        UpdateLineNumbers();
        Editor.IsReadOnly = false;

        // Visibility
        FileInfoBar.Visibility = Visibility.Visible;
        EditorScroller.Visibility = Visibility.Visible;
        NoFilesState.Visibility = Visibility.Collapsed;
        NoSelectionState.Visibility = Visibility.Collapsed;
        SaveButton.Visibility = _vm.IsModified ? Visibility.Visible : Visibility.Collapsed;

        ApplyEditorBorder();
        UpdateErrorDisplay();
    }

    private void OnConfigFileChanged(string path)
    {
        if (_vm == null)
            return;

        var selectedPath = _vm.SelectedFile?.Path;
        var selectedWasModified = _vm.IsModified;

        _vm.LoadFileList();

        if (!string.IsNullOrEmpty(selectedPath))
        {
            var refreshedFile = _vm.Files.FirstOrDefault(file =>
                file.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
            if (refreshedFile != null)
            {
                if (!selectedWasModified &&
                    path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    _vm.SelectFile(refreshedFile);
                    ShowEditor();
                }
                else
                {
                    _vm.SelectedFile = refreshedFile;
                }
            }
        }

        BuildFileTabs();
    }

    // ── Line numbers ─────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the line-number gutter so it always matches the number of
    /// logical lines (split by newline) in the editor text.
    /// </summary>
    private void UpdateLineNumbers()
    {
        LineNumbers.Items.Clear();
        var lineCount = (Editor.Text ?? "").Split('\n').Length;
        for (int i = 1; i <= lineCount; i++)
            LineNumbers.Items.Add(i.ToString(CultureInfo.InvariantCulture));
    }

    // ── Editor text changed ──────────────────────────────────────────────

    /// <summary>
    /// Handles every text change in the editor: syncs line numbers, pushes
    /// content to the ViewModel, and updates save/modified UI indicators.
    /// </summary>
    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLineNumbers();

        if (_vm == null) return;

        // Push content into the ViewModel so IsModified stays accurate
        _vm.FileContent = Editor.Text;

        SaveButton.Visibility = _vm.IsModified ? Visibility.Visible : Visibility.Collapsed;
        UpdateSaveStatus();
        ApplyEditorBorder();
    }

    // ── Save ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists changes via the ViewModel.  On conflict the conflict overlay
    /// is shown; on success the saved status is refreshed.
    /// </summary>
    private async Task OnSaveChangesAsync()
    {
        if (_vm == null) return;

        await _vm.SaveChangesAsync();

        if (_vm.HasConflict)
        {
            ShowConflictOverlay();
        }
        else
        {
            SaveButton.Visibility = Visibility.Collapsed;
            UpdateSaveStatus();
            UpdateErrorDisplay();
        }
    }

    // ── Save status ──────────────────────────────────────────────────────

    private void UpdateSaveStatus()
    {
        if (_vm == null) return;

        if (_vm.IsModified)
        {
            SaveStatus.Label = "未保存";
            SaveStatus.Status = "Error";
        }
        else
        {
            SaveStatus.Label = "已保存";
            SaveStatus.Status = "Stopped";
        }
    }

    // ── Editor border ────────────────────────────────────────────────────

    /// <summary>
    /// Applies a yellow-tinted border when content is modified, or a subtle
    /// white border when the content matches the saved version.
    /// </summary>
    private void ApplyEditorBorder()
    {
        EditorBorder.BorderBrush = _vm?.IsModified == true
            ? new SolidColorBrush(ModifiedBorderYellow)
            : new SolidColorBrush(CleanBorderWhite);
    }

    // ── Error display ────────────────────────────────────────────────────

    private void UpdateErrorDisplay()
    {
        if (_vm == null) return;

        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorMsg.Text = _vm.ErrorMessage;
            ErrorPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
        }
    }

    // ── Empty states ─────────────────────────────────────────────────────

    private void ShowNoFilesState()
    {
        FileInfoBar.Visibility = Visibility.Collapsed;
        EditorScroller.Visibility = Visibility.Collapsed;
        NoSelectionState.Visibility = Visibility.Collapsed;
        NoFilesState.Visibility = Visibility.Visible;
    }

    private void ShowNoSelectionState()
    {
        FileInfoBar.Visibility = Visibility.Collapsed;
        EditorScroller.Visibility = Visibility.Collapsed;
        NoFilesState.Visibility = Visibility.Collapsed;
        NoSelectionState.Visibility = Visibility.Visible;
    }

    // ── Conflict overlay ─────────────────────────────────────────────────

    private void ShowConflictOverlay()
    {
        if (_vm == null) return;

        LocalContent.Text = _vm.FileContent;
        RemoteContent.Text = _vm.ConflictRemoteContent;
        ConflictOverlay.Visibility = Visibility.Visible;
    }

    private void OnAcceptLocal()
    {
        _vm?.ResolveConflict(useRemote: false);
        ConflictOverlay.Visibility = Visibility.Collapsed;

        if (_vm == null) return;
        Editor.Text = _vm.FileContent;
        SaveButton.Visibility = _vm.IsModified ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAcceptRemote()
    {
        _vm?.ResolveConflict(useRemote: true);
        ConflictOverlay.Visibility = Visibility.Collapsed;

        if (_vm == null) return;
        Editor.Text = _vm.FileContent;
        SaveButton.Visibility = Visibility.Collapsed;
        UpdateSaveStatus();
    }

    private void OnDismissConflict(object sender, RoutedEventArgs e)
    {
        ConflictOverlay.Visibility = Visibility.Collapsed;
    }

    // ── Back navigation ──────────────────────────────────────────────────

    private void OnBack(object sender, RoutedEventArgs e)
    {
        var mainVm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel;
        if (mainVm != null) mainVm.SelectedPanel = MainPanelType.Dashboard;
    }
}
