using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Views.Skills;

/// <summary>
/// Display mode for the <see cref="SkillCard"/> control.
/// Matches SkillCardMode in SkillCard.swift.
/// </summary>
public enum SkillCardMode
{
    /// <summary>Compact horizontal row with status badge and hover menu.</summary>
    Installed,

    /// <summary>Grid card with icon, name, stars, description, and install button.</summary>
    Marketplace
}

/// <summary>
/// A skill card UserControl — port of SkillCard.swift.
///
/// Two visual modes driven by the <see cref="Mode"/> property:
/// - Installed: compact horizontal row (puzzle icon + name + description
///   + enabled/disabled badge + hover menu with toggle/delete).
/// - Marketplace: card with icon, name, stars (if any), description,
///   and an install button or "已安装" badge.
/// </summary>
public partial class SkillCard : UserControl
{
    // ── Dependency Properties ──────────────────────────────────────────

    public static readonly DependencyProperty SkillProperty =
        DependencyProperty.Register(
            nameof(Skill),
            typeof(SkillItem),
            typeof(SkillCard),
            new PropertyMetadata(null, OnSkillChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(SkillCardMode),
            typeof(SkillCard),
            new PropertyMetadata(SkillCardMode.Installed, OnModeChanged));

    // ── CLR Wrappers ──────────────────────────────────────────────────

    public SkillItem? Skill
    {
        get => (SkillItem?)GetValue(SkillProperty);
        set => SetValue(SkillProperty, value);
    }

    public SkillCardMode Mode
    {
        get => (SkillCardMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    // ── Routed Events ─────────────────────────────────────────────────

    public static readonly RoutedEvent ToggleClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ToggleClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SkillCard));

    public static readonly RoutedEvent DeleteClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(DeleteClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SkillCard));

    public static readonly RoutedEvent InstallClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(InstallClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SkillCard));

    public static readonly RoutedEvent UninstallClickedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(UninstallClicked),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SkillCard));

    public event RoutedEventHandler ToggleClicked
    {
        add => AddHandler(ToggleClickedEvent, value);
        remove => RemoveHandler(ToggleClickedEvent, value);
    }

    public event RoutedEventHandler DeleteClicked
    {
        add => AddHandler(DeleteClickedEvent, value);
        remove => RemoveHandler(DeleteClickedEvent, value);
    }

    public event RoutedEventHandler InstallClicked
    {
        add => AddHandler(InstallClickedEvent, value);
        remove => RemoveHandler(InstallClickedEvent, value);
    }

    public event RoutedEventHandler UninstallClicked
    {
        add => AddHandler(UninstallClickedEvent, value);
        remove => RemoveHandler(UninstallClickedEvent, value);
    }

    // ── Internal State ────────────────────────────────────────────────

    private readonly SolidColorBrush _installedBgBrush;
    private readonly SolidColorBrush _marketplaceBorderBrush;

    // ── Constructor ───────────────────────────────────────────────────

    public SkillCard()
    {
        InitializeComponent();

        _installedBgBrush = new SolidColorBrush(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
        if (InstalledRoot != null)
            InstalledRoot.Background = _installedBgBrush;

        _marketplaceBorderBrush = new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
        if (MarketplaceRoot != null)
            MarketplaceRoot.BorderBrush = _marketplaceBorderBrush;
    }

    // ── Loaded ────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyMode();
        ApplySkillData();
    }

    // ── Property Changed Callbacks ────────────────────────────────────

    private static void OnSkillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (SkillCard)d;
        card.ApplySkillData();
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (SkillCard)d;
        card.ApplyMode();
        card.ApplySkillData();
    }

    // ── Mode Visual Switch ────────────────────────────────────────────

    /// <summary>
    /// Shows the installed or marketplace visual tree based on the current Mode.
    /// </summary>
    private void ApplyMode()
    {
        if (InstalledRoot == null || MarketplaceRoot == null)
            return;

        if (Mode == SkillCardMode.Installed)
        {
            InstalledRoot.Visibility = Visibility.Visible;
            MarketplaceRoot.Visibility = Visibility.Collapsed;
        }
        else
        {
            InstalledRoot.Visibility = Visibility.Collapsed;
            MarketplaceRoot.Visibility = Visibility.Visible;
        }
    }

    // ── Data Binding ──────────────────────────────────────────────────

    /// <summary>
    /// Populates text blocks and badges from the current <see cref="Skill"/>.
    /// </summary>
    private void ApplySkillData()
    {
        var skill = Skill;
        if (skill == null) return;

        if (Mode == SkillCardMode.Installed)
        {
            if (InstalledName != null)
                InstalledName.Text = skill.Name;
            if (InstalledDesc != null)
                InstalledDesc.Text = skill.Description ?? "";

            if (StatusBadge != null)
            {
                StatusBadge.Text = skill.IsEnabled ? "已启用" : "已禁用";
                StatusBadge.Variant = skill.IsEnabled ? "Success" : "Neutral";
            }

            if (ToggleIcon != null)
                ToggleIcon.Text = skill.IsEnabled ? "" : ""; // Pause / Play
            if (ToggleLabel != null)
                ToggleLabel.Text = skill.IsEnabled ? "禁用" : "启用";
        }
        else
        {
            if (MarketplaceName != null)
                MarketplaceName.Text = skill.Name;
            if (MarketplaceDesc != null)
            {
                MarketplaceDesc.Text = skill.Description ?? "";
                MarketplaceDesc.Visibility = string.IsNullOrEmpty(skill.Description)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Stars
            if (StarsRow != null && StarsCount != null)
            {
                if (skill.StarCount.HasValue)
                {
                    StarsRow.Visibility = Visibility.Visible;
                    StarsCount.Text = FormatStarCount(skill.StarCount.Value);
                }
                else
                {
                    StarsRow.Visibility = Visibility.Collapsed;
                }
            }

            // Install button vs "已安装" badge
            if (InstallButton != null)
                InstallButton.Visibility = skill.IsInstalled
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            if (InstalledBadge != null)
                InstalledBadge.Visibility = skill.IsInstalled
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    // ── Installed mode: hover ─────────────────────────────────────────

    /// <summary>
    /// On mouse enter, lighten the row background and reveal the ellipsis menu button.
    /// </summary>
    private void OnInstalledMouseEnter(object sender, MouseEventArgs e)
    {
        if (MenuButton != null)
            MenuButton.Visibility = Visibility.Visible;

        AnimateInstalledBgTo(0x07); // white at ~3% opacity
    }

    /// <summary>
    /// On mouse leave, reset the row background and hide the ellipsis menu button.
    /// </summary>
    private void OnInstalledMouseLeave(object sender, MouseEventArgs e)
    {
        if (MenuButton != null)
            MenuButton.Visibility = Visibility.Collapsed;

        AnimateInstalledBgTo(0x00); // fully transparent
    }

    private void AnimateInstalledBgTo(byte targetAlpha)
    {
        if (_installedBgBrush == null) return;

        _installedBgBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        var from = _installedBgBrush.Color;
        var to = Color.FromArgb(targetAlpha, 0xFF, 0xFF, 0xFF);

        if (from == to) return;

        var anim = new ColorAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        _installedBgBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    // ── Menu button (Installed mode) ──────────────────────────────────

    /// <summary>
    /// Opens the context menu when the ellipsis button is clicked.
    /// </summary>
    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (MenuButton?.ContextMenu != null)
        {
            MenuButton.ContextMenu.IsOpen = true;
        }
    }

    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ToggleClickedEvent));
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(DeleteClickedEvent));
    }

    // ── Install button (Marketplace mode) ─────────────────────────────

    private void OnInstallButtonClick(object sender, RoutedEventArgs e)
    {
        var skill = Skill;
        if (skill == null) return;

        if (skill.IsInstalled)
            RaiseEvent(new RoutedEventArgs(UninstallClickedEvent));
        else
            RaiseEvent(new RoutedEventArgs(InstallClickedEvent));
    }

    // ── Marketplace hover (border glow) ───────────────────────────────

    private void OnMarketplaceMouseEnter(object sender, MouseEventArgs e)
    {
        AnimateMarketplaceBorderTo(0x1F); // white at ~12% opacity
    }

    private void OnMarketplaceMouseLeave(object sender, MouseEventArgs e)
    {
        AnimateMarketplaceBorderTo(0x0F); // white at ~6% opacity
    }

    private void AnimateMarketplaceBorderTo(byte targetAlpha)
    {
        if (_marketplaceBorderBrush == null) return;

        _marketplaceBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        var from = _marketplaceBorderBrush.Color;
        var to = Color.FromArgb(targetAlpha, 0xFF, 0xFF, 0xFF);

        if (from == to) return;

        var anim = new ColorAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        _marketplaceBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Formats a star count for display, e.g. 1234 becomes "1.2k".
    /// </summary>
    private static string FormatStarCount(int count)
    {
        if (count >= 1000)
        {
            var thousands = count / 1000;
            var remainder = (count % 1000) / 100;
            return $"{thousands}.{remainder}k";
        }
        return count.ToString(CultureInfo.InvariantCulture);
    }
}
