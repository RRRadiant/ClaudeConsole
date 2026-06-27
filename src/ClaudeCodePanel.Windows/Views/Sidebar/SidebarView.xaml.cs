using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Views.Sidebar;

public partial class SidebarView : UserControl
{
    private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);
    private static readonly Color AccentBgColor = Color.FromArgb(0x1E, 0x64, 0xb4, 0xff);

    private static readonly SolidColorBrush AccentTextBrush = new(AccentColor);
    private static readonly SolidColorBrush SecondaryTextBrush =
        new(Color.FromRgb(0x99, 0x99, 0x99));
    private static readonly SolidColorBrush AccentBackgroundBrush = new(AccentBgColor);
    private static readonly SolidColorBrush HoverBackgroundBrush =
        new(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));

    private MainViewModel? _viewModel;
    private readonly List<Button> _itemButtons = new();
    private readonly Dictionary<Button, MainPanelType> _buttonToPanel = new();
    private readonly Dictionary<Button, Ellipse> _buttonToActiveDot = new();
    private readonly Dictionary<Button, Border> _buttonToAccentBar = new();
    private bool _isLoaded;
    private bool _hasPlayedEntrance;

    // Toggle button content elements (built in code to avoid template FindName issues)
    private TextBlock? _themeIcon, _themeLabel, _themeSublabel;
    private TextBlock? _langIcon, _langLabel, _langSublabel;

    public SidebarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        BuildToggleButtonContent();
        UpdateThemeToggleLabel();
        UpdateLanguageToggleLabel();
        Dispatcher.BeginInvoke(
            new System.Action(() =>
            {
                CacheItemButtons();
                AttachViewModel();
                RefreshSelectionVisuals();
            }),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Builds theme/language toggle button content in code (avoids Template FindName issues).</summary>
    private void BuildToggleButtonContent()
    {
        // ── Theme toggle button ──
        var themeGrid = new Grid();
        themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _themeIcon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 10, 0)
        };
        Grid.SetColumn(_themeIcon, 0);
        themeGrid.Children.Add(_themeIcon);

        var themeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _themeLabel = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold };
        _themeSublabel = new TextBlock { FontSize = 10 };
        themeStack.Children.Add(_themeLabel);
        themeStack.Children.Add(_themeSublabel);
        Grid.SetColumn(themeStack, 1);
        themeGrid.Children.Add(themeStack);

        ThemeToggleButton.Content = themeGrid;
        ThemeToggleButton.Padding = new Thickness(0, 8, 16, 8);

        // ── Hover: background → SurfaceStrongBrush (200 ms) ──
        ThemeToggleButton.MouseEnter += ThemeToggleButton_MouseEnter;
        ThemeToggleButton.MouseLeave += ThemeToggleButton_MouseLeave;

        // ── Language toggle button ──
        var langGrid = new Grid();
        langGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        langGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _langIcon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 15,
            Text = "\uE8B4",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 10, 0)
        };
        Grid.SetColumn(_langIcon, 0);
        langGrid.Children.Add(_langIcon);

        var langStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _langLabel = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold };
        _langSublabel = new TextBlock { FontSize = 10 };
        langStack.Children.Add(_langLabel);
        langStack.Children.Add(_langSublabel);
        Grid.SetColumn(langStack, 1);
        langGrid.Children.Add(langStack);

        LangToggleButton.Content = langGrid;
        LangToggleButton.Padding = new Thickness(0, 8, 16, 8);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = e.NewValue as MainViewModel;

        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (_isLoaded)
            RefreshSelectionVisuals();
    }

    private void AttachViewModel()
    {
        if (_viewModel != null) return;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedPanel))
            Dispatcher.Invoke(RefreshSelectionVisuals);
    }

    private void CacheItemButtons()
    {
        _itemButtons.Clear();
        _buttonToPanel.Clear();
        _buttonToActiveDot.Clear();
        _buttonToAccentBar.Clear();

        if (_viewModel == null) return;

        for (int i = 0; i < _viewModel.SidebarItems.Count; i++)
        {
            var container = ItemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (container == null) continue;

            var button = FindVisualChild<Button>(container);
            if (button == null) continue;

            _itemButtons.Add(button);
            var item = _viewModel.SidebarItems[i];
            _buttonToPanel[button] = item.PanelType;

            var dot = FindVisualChild<Ellipse>(button);
            if (dot != null)
                _buttonToActiveDot[button] = dot;

            // Cache the accent bar from the control template
            if (button.Template.FindName("accentBar", button) is Border accentBar)
                _buttonToAccentBar[button] = accentBar;
        }

        // Play staggered entrance on first load
        if (!_hasPlayedEntrance)
        {
            _hasPlayedEntrance = true;
            PlayStaggeredEntrance();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var descendant = FindVisualChild<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
    }

    private void RefreshSelectionVisuals()
    {
        if (_viewModel == null) return;
        var selectedPanel = _viewModel.SelectedPanel;

        foreach (var button in _itemButtons)
        {
            if (button == null) continue;
            var isSelected = _buttonToPanel.TryGetValue(button, out var panel)
                && panel == selectedPanel;

            // Text color: accent when selected, secondary when not
            button.Foreground = isSelected ? AccentTextBrush : SecondaryTextBrush;

            // Background: subtle accent when selected, transparent when not
            button.Background = isSelected ? AccentBackgroundBrush : Brushes.Transparent;

            // Active dot
            if (_buttonToActiveDot.TryGetValue(button, out var dot) && dot != null)
            {
                dot.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
            }

            // Accent bar: permanent when selected, hidden when not
            if (_buttonToAccentBar.TryGetValue(button, out var accentBar) && accentBar != null)
            {
                accentBar.Height = isSelected ? 28 : 0;
                if (accentBar.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
                    shadow.Opacity = isSelected ? 0.6 : 0;
            }
        }
    }

    // ── Event Handlers ──────────────────────────────────────────

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || !_buttonToPanel.TryGetValue(btn, out var panel)) return;
        _viewModel?.NavigateCommand.Execute(panel);
    }

    private void OnItemMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Hover visuals are handled by XAML EventTriggers in the ControlTemplate.
        // No code-behind hover needed — the Storyboard animates background,
        // accent bar height, and icon rotation.
    }

    private void OnItemMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // XAML MouseLeave EventTrigger handles reversing hover animations.
        // Re-apply active state visuals if this item is the selected one,
        // because the leave animation resets the accent bar to 0.
        if (sender is not Button btn) return;
        var isSelected = _buttonToPanel.TryGetValue(btn, out var panel)
            && panel == _viewModel?.SelectedPanel;

        if (isSelected)
            ApplyActiveState(btn);
    }

    /// <summary>Re-applies active-state visuals to a button (accent bar + glow).</summary>
    private void ApplyActiveState(Button btn)
    {
        btn.Background = AccentBackgroundBrush;
        btn.Foreground = AccentTextBrush;

        if (_buttonToAccentBar.TryGetValue(btn, out var accentBar) && accentBar != null)
        {
            accentBar.Height = 28;
            if (accentBar.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
                shadow.Opacity = 0.6;
        }
    }

    // ── Staggered entrance animation ──────────────────────────

    /// <summary>
    /// Plays a staggered slide-in entrance for all nav items.
    /// Each item slides from translateX -10px with 50 ms delay increments
    /// (400 ms ease-out per item).
    /// </summary>
    private void PlayStaggeredEntrance()
    {
        for (int i = 0; i < _itemButtons.Count; i++)
        {
            var button = _itemButtons[i];
            if (button == null) continue;

            // Set initial state: shifted left, invisible
            button.RenderTransform = new TranslateTransform(-10, 0);
            button.Opacity = 0;

            var sb = new System.Windows.Media.Animation.Storyboard();
            sb.BeginTime = TimeSpan.FromMilliseconds(i * 50);

            // Slide in: X -10 → 0
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(-10, 0,
                TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(slideIn, button);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(slideIn,
                new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            sb.Children.Add(slideIn);

            // Fade in: 0 → 1
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
                TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(fadeIn, button);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeIn,
                new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fadeIn);

            sb.Completed += (_, _) =>
            {
                button.RenderTransform = Transform.Identity;
            };

            sb.Begin();
        }
    }

    // ── Theme & Language Toggles ─────────────────────────────

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        // Animate icon rotation before toggling theme
        if (_themeIcon != null)
        {
            var rotate = new RotateTransform(0);
            _themeIcon.RenderTransform = rotate;
            _themeIcon.RenderTransformOrigin = new Point(0.5, 0.5);

            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 360,
                TimeSpan.FromMilliseconds(500))
            {
                EasingFunction = new System.Windows.Media.Animation.BackEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
                    Amplitude = 0.5
                }
            };
            anim.Completed += (_, _) =>
            {
                _themeIcon.RenderTransform = Transform.Identity;
            };
            rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        // Fade theme label text out, then back in after theme swap
        AnimateThemeLabelFade();

        ThemeService.Instance.ToggleTheme();
        UpdateThemeToggleLabel();
        UpdateLanguageToggleLabel(); // refresh colors too
    }

    /// <summary>Fades the theme label text color between old and new theme.</summary>
    private async void AnimateThemeLabelFade()
    {
        if (_themeLabel == null || _themeSublabel == null) return;

        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0,
            TimeSpan.FromMilliseconds(100));
        _themeLabel.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        _themeSublabel.BeginAnimation(UIElement.OpacityProperty, fadeOut);

        await System.Threading.Tasks.Task.Delay(150);

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0,
            TimeSpan.FromMilliseconds(150));
        _themeLabel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        _themeSublabel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.ToggleLanguage();
        UpdateThemeToggleLabel();
        UpdateLanguageToggleLabel();
        _viewModel?.RefreshSidebarLabels();
        // Re-cache buttons after ItemsControl re-generates containers
        Dispatcher.BeginInvoke(new Action(() =>
        {
            CacheItemButtons();
            RefreshSelectionVisuals();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateThemeToggleLabel()
    {
        if (_themeIcon == null || _themeLabel == null || _themeSublabel == null) return;

        if (ThemeService.Instance.IsDarkTheme)
        {
            _themeIcon.Text = "\uE706";
            _themeLabel.Text = LocalizationService.Instance.Get("Theme.Dark");
            _themeSublabel.Text = "深色模式";
        }
        else
        {
            _themeIcon.Text = "\uE708";
            _themeLabel.Text = LocalizationService.Instance.Get("Theme.Light");
            _themeSublabel.Text = "浅色模式";
        }

        // Refresh text colors from current theme
        var primaryBrush = FindResource("TextPrimaryBrush") as Brush;
        var secondaryBrush = FindResource("TextSecondaryBrush") as Brush;
        var tertiaryBrush = FindResource("TextTertiaryBrush") as Brush;

        if (primaryBrush != null) _themeLabel.Foreground = primaryBrush;
        if (secondaryBrush != null) _themeIcon.Foreground = secondaryBrush;
        if (tertiaryBrush != null) _themeSublabel.Foreground = tertiaryBrush;
    }

    // ── Theme toggle hover animations ─────────────────────

    private void ThemeToggleButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var bgAnim = new System.Windows.Media.Animation.ColorAnimation(
            Colors.Transparent,
            Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), // SurfaceStrongBrush
            TimeSpan.FromMilliseconds(200));
        ThemeToggleButton.Background = new SolidColorBrush(Colors.Transparent);
        ThemeToggleButton.Background.BeginAnimation(SolidColorBrush.ColorProperty, bgAnim);
    }

    private void ThemeToggleButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (ThemeToggleButton.Background is SolidColorBrush bg)
        {
            var bgAnim = new System.Windows.Media.Animation.ColorAnimation(
                bg.Color,
                Colors.Transparent,
                TimeSpan.FromMilliseconds(200));
            bg.BeginAnimation(SolidColorBrush.ColorProperty, bgAnim);
        }
    }

    private void UpdateLanguageToggleLabel()
    {
        if (_langLabel == null || _langSublabel == null) return;

        if (LocalizationService.Instance.IsChinese)
        {
            _langLabel.Text = "中文";
            _langSublabel.Text = "Switch to English";
        }
        else
        {
            _langLabel.Text = "English";
            _langSublabel.Text = "切换到中文";
        }

        var primaryBrush = FindResource("TextPrimaryBrush") as Brush;
        var secondaryBrush = FindResource("TextSecondaryBrush") as Brush;
        var tertiaryBrush = FindResource("TextTertiaryBrush") as Brush;

        if (primaryBrush != null) _langLabel.Foreground = primaryBrush;
        if (secondaryBrush != null && _langIcon != null) _langIcon.Foreground = secondaryBrush;
        if (tertiaryBrush != null) _langSublabel.Foreground = tertiaryBrush;
    }
}
