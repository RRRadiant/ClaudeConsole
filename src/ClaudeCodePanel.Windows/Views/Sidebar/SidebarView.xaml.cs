using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;
using ThemeMode = ClaudeCodePanel.Windows.Services.ThemeMode;
using Forms = System.Windows.Forms;

namespace ClaudeCodePanel.Windows.Views.Sidebar;

public partial class SidebarView : UserControl
{
    private MainViewModel? _viewModel;
    private readonly List<Button> _itemButtons = new();
    private readonly Dictionary<Button, MainPanelType> _buttonToPanel = new();
    private readonly Dictionary<Button, Ellipse> _buttonToActiveDot = new();
    private readonly Dictionary<Button, Border> _buttonToAccentBar = new();
    private bool _isLoaded;
    private bool _hasPlayedEntrance;
    private bool _isViewModelAttached;

    public SidebarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ThemeService.Instance.PropertyChanged -= OnThemeServicePropertyChanged;
        ThemeService.Instance.PropertyChanged += OnThemeServicePropertyChanged;
        UpdateAppearanceSection();
        UpdateLanguageToggleLabel();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            CacheItemButtons();
            AttachViewModel();
            RefreshSelectionVisuals();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        ThemeService.Instance.PropertyChanged -= OnThemeServicePropertyChanged;
        DetachViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();
        _viewModel = e.NewValue as MainViewModel;
        AttachViewModel();

        if (_isLoaded)
            RefreshSelectionVisuals();
    }

    private void AttachViewModel()
    {
        if (_isViewModelAttached)
            return;

        _viewModel ??= DataContext as MainViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _isViewModelAttached = true;
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel == null || !_isViewModelAttached)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _isViewModelAttached = false;
    }

    private void OnThemeServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateAppearanceSection();
            UpdateLanguageToggleLabel();
            RefreshSelectionVisuals();
        });
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
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

        if (_viewModel == null)
            return;

        for (int i = 0; i < _viewModel.SidebarItems.Count; i++)
        {
            var container = ItemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (container == null)
                continue;

            var button = FindVisualChild<Button>(container);
            if (button == null)
                continue;

            _itemButtons.Add(button);
            _buttonToPanel[button] = _viewModel.SidebarItems[i].PanelType;

            if (FindVisualChild<Ellipse>(button) is { } dot)
                _buttonToActiveDot[button] = dot;

            if (button.Template.FindName("accentBar", button) is Border accentBar)
                _buttonToAccentBar[button] = accentBar;
        }

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
            if (child is T found)
                return found;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }

        return null;
    }

    private void RefreshSelectionVisuals()
    {
        if (_viewModel == null)
            return;

        var selectedPanel = _viewModel.SelectedPanel;
        var accentBrush = ThemeBrush("AccentBrush", Color.FromRgb(0x6F, 0xAA, 0xDD));
        var accentBg = ThemeBrush("AccentSubtleBrush", Color.FromArgb(0x24, 0x64, 0xB4, 0xFF));
        var accentBorder = ThemeBrush("BorderAccentBrush", Color.FromArgb(0x52, 0x64, 0xB4, 0xFF));
        var secondaryText = ThemeBrush("TextSecondaryBrush", Color.FromRgb(0x99, 0x99, 0x99));

        foreach (var button in _itemButtons)
        {
            var isSelected = _buttonToPanel.TryGetValue(button, out var panel) && panel == selectedPanel;
            button.Foreground = isSelected ? accentBrush : secondaryText;
            button.Background = isSelected ? accentBg : Brushes.Transparent;
            button.BorderBrush = isSelected ? accentBorder : Brushes.Transparent;

            if (_buttonToActiveDot.TryGetValue(button, out var dot))
                dot.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;

            if (_buttonToAccentBar.TryGetValue(button, out var accentBar))
                accentBar.Opacity = isSelected ? 1.0 : 0.0;
        }
    }

    private Brush ThemeBrush(string key, Color fallback) =>
        TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private void UpdateAppearanceSection()
    {
        var loc = LocalizationService.Instance;
        var currentMode = ThemeService.Instance.CurrentThemeMode;

        AppearanceHeadingText.Text = loc.Get("Theme.Appearance").ToUpperInvariant();

        ApplyModeButtonState(ThemeModeSystemButton, loc.Get("Theme.ModeSystem"), currentMode == ThemeMode.System);
        ApplyModeButtonState(ThemeModeLightButton, loc.Get("Theme.ModeLight"), currentMode == ThemeMode.Light);
        ApplyModeButtonState(ThemeModeDarkButton, loc.Get("Theme.ModeDark"), currentMode == ThemeMode.Dark);
        ApplyModeButtonState(ThemeModeCustomButton, loc.Get("Theme.ModeCustom"), currentMode == ThemeMode.Custom);

        var accentColor = ThemeService.Instance.ActiveAccentColor;
        AccentPreviewSwatch.Fill = new SolidColorBrush(accentColor);
        AccentLabelText.Text = loc.Get("Theme.Accent");
        AccentHexText.Text = $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}";
        AccentHintText.Text = currentMode == ThemeMode.Custom
            ? loc.Get("Theme.AccentActiveHint")
            : loc.Get("Theme.AccentHint");
        PickAccentButton.Content = loc.Get("Theme.AccentAction");
    }

    private void ApplyModeButtonState(Button button, string label, bool selected)
    {
        button.Content = label;
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Medium;
        button.Background = selected
            ? ThemeBrush("AccentSubtleBrush", Color.FromArgb(0x24, 0x64, 0xB4, 0xFF))
            : Brushes.Transparent;
        button.BorderBrush = selected
            ? ThemeBrush("BorderAccentBrush", Color.FromArgb(0x52, 0x64, 0xB4, 0xFF))
            : ThemeBrush("BorderDefaultBrush", Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
        button.Foreground = selected
            ? ThemeBrush("AccentBrush", Color.FromRgb(0x6F, 0xAA, 0xDD))
            : ThemeBrush("TextSecondaryBrush", Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
    }

    private void UpdateLanguageToggleLabel()
    {
        var loc = LocalizationService.Instance;

        LanguageLabelText.Text = loc.Get("Language.Label");
        LanguageHintText.Text = loc.IsChinese
            ? loc.Get("Language.English")
            : loc.Get("Language.Chinese");
    }

    private void PlayStaggeredEntrance()
    {
        for (int i = 0; i < _itemButtons.Count; i++)
        {
            var button = _itemButtons[i];
            button.RenderTransform = new TranslateTransform(-12, 0);
            button.Opacity = 0;

            var sb = new System.Windows.Media.Animation.Storyboard
            {
                BeginTime = TimeSpan.FromMilliseconds(i * 42)
            };

            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(-12, 0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(slideIn, button);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(slideIn,
                new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            System.Windows.Media.Animation.Storyboard.SetTarget(fadeIn, button);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeIn,
                new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(slideIn);
            sb.Children.Add(fadeIn);
            sb.Completed += (_, _) => button.RenderTransform = Transform.Identity;
            sb.Begin();
        }
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || !_buttonToPanel.TryGetValue(btn, out var panel))
            return;

        _viewModel?.NavigateCommand.Execute(panel);
    }

    private void OnItemMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Hover visuals are handled in the control template.
    }

    private void OnItemMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Button btn)
            return;

        var isSelected = _buttonToPanel.TryGetValue(btn, out var panel) && panel == _viewModel?.SelectedPanel;
        if (isSelected)
            RefreshSelectionVisuals();
    }

    private void OnThemeModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string modeName)
            return;

        if (!Enum.TryParse<ThemeMode>(modeName, true, out var mode))
            return;

        ThemeService.Instance.SetThemeMode(mode);
        UpdateAppearanceSection();
        RefreshSelectionVisuals();
    }

    private void OnPickAccentClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(
                ThemeService.Instance.CustomAccentColor.A,
                ThemeService.Instance.CustomAccentColor.R,
                ThemeService.Instance.CustomAccentColor.G,
                ThemeService.Instance.CustomAccentColor.B)
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
            return;

        ThemeService.Instance.SetCustomAccent(Color.FromArgb(
            dialog.Color.A,
            dialog.Color.R,
            dialog.Color.G,
            dialog.Color.B));

        UpdateAppearanceSection();
        RefreshSelectionVisuals();
    }

    private void OnLanguageToggleClick(object sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.ToggleLanguage();
        UpdateAppearanceSection();
        UpdateLanguageToggleLabel();
        _viewModel?.RefreshSidebarLabels();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            CacheItemButtons();
            RefreshSelectionVisuals();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
