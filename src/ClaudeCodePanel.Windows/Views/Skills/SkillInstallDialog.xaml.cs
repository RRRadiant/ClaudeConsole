using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Views.Skills;

/// <summary>
/// Install skill modal dialog — port of SkillInstallSheet.swift.
///
/// Renders a modal overlay with:
/// - Title "安装技能"
/// - Source picker (本地路径 / Git URL)
/// - Text input for the path or URL
/// - Cancel and Install action buttons
/// </summary>
public partial class SkillInstallDialog : UserControl
{
    // ── Dependency Properties ──────────────────────────────────────────

    public static readonly DependencyProperty PathOrURLProperty =
        DependencyProperty.Register(
            nameof(PathOrURL),
            typeof(string),
            typeof(SkillInstallDialog),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPathOrURLChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(SkillInstallDialog),
            new PropertyMetadata(false, OnIsLoadingChanged));

    // ── CLR Wrappers ──────────────────────────────────────────────────

    /// <summary>
    /// The path or URL text entered by the user. Two-way bindable.
    /// </summary>
    public string PathOrURL
    {
        get => (string)GetValue(PathOrURLProperty);
        set => SetValue(PathOrURLProperty, value);
    }

    /// <summary>
    /// When true, the install button shows a loading state and is disabled.
    /// </summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// The currently selected source type (LocalPath or GitURL).
    /// Updated as the user toggles the source picker buttons.
    /// </summary>
    public SkillSource CurrentSource { get; private set; } = SkillSource.LocalPath;

    // ── Routed Events ─────────────────────────────────────────────────

    public static readonly RoutedEvent InstallClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(InstallClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SkillInstallDialog));

    public static readonly RoutedEvent CancelClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(CancelClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SkillInstallDialog));

    public event RoutedEventHandler InstallClicked
    {
        add => AddHandler(InstallClickedEvent, value);
        remove => RemoveHandler(InstallClickedEvent, value);
    }

    public event RoutedEventHandler CancelClicked
    {
        add => AddHandler(CancelClickedEvent, value);
        remove => RemoveHandler(CancelClickedEvent, value);
    }

    // ── Constructor ───────────────────────────────────────────────────

    public SkillInstallDialog()
    {
        InitializeComponent();
    }

    // ── Property Changed Callbacks ────────────────────────────────────

    private static void OnPathOrURLChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var dialog = (SkillInstallDialog)d;
        if (dialog.InstallPath != null)
            dialog.InstallPath.Text = (e.NewValue as string) ?? "";
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var dialog = (SkillInstallDialog)d;
        var loading = (bool)e.NewValue;

        if (dialog.InstallConfirmButton != null)
        {
            dialog.InstallConfirmButton.IsEnabled = !loading;
            // Dim the button text slightly when loading
            dialog.InstallConfirmButton.Opacity = loading ? 0.6 : 1.0;
        }
    }

    // ── Source Picker ─────────────────────────────────────────────────

    /// <summary>
    /// Toggles between LocalPath and GitURL source types.
    /// Updates the placeholder text to match the selected source.
    /// </summary>
    private void OnSourceTab(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string tag) return;

        if (tag == "LocalPath")
        {
            LocalPathTab.IsChecked = true;
            GitUrlTab.IsChecked = false;
            CurrentSource = SkillSource.LocalPath;
            if (InstallPath != null)
                InstallPath.Placeholder = "/path/to/skill";
        }
        else
        {
            LocalPathTab.IsChecked = false;
            GitUrlTab.IsChecked = true;
            CurrentSource = SkillSource.GitURL;
            if (InstallPath != null)
                InstallPath.Placeholder = "https://github.com/user/skill.git";
        }
    }

    // ── Action Buttons ────────────────────────────────────────────────

    /// <summary>
    /// Fires the <see cref="InstallClicked"/> event to notify the parent view.
    /// The parent reads <see cref="PathOrURL"/> and <see cref="CurrentSource"/>
    /// to perform the installation.
    /// </summary>
    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        // Sync the text field back to the PathOrURL property before raising
        if (InstallPath != null)
            PathOrURL = InstallPath.Text ?? "";

        RaiseEvent(new RoutedEventArgs(InstallClickedEvent));
    }

    /// <summary>
    /// Fires the <see cref="CancelClicked"/> event and resets the input field.
    /// </summary>
    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (InstallPath != null)
            InstallPath.Text = "";

        RaiseEvent(new RoutedEventArgs(CancelClickedEvent));
    }
}
