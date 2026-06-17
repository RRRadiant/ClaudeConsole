using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Views.Sidebar;

/// <summary>
/// Sidebar navigation panel — 220px wide vertical list of navigation items.
/// Each item displays a Segoe MDL2 Assets icon, Chinese title, description,
/// and an active-dot indicator. Matches the Claude-Win Liquid Glass sidebar design.
///
/// Items are driven by MainViewModel.SidebarItems and bound via ItemsControl.
/// Selection state is driven by MainViewModel.SelectedPanel.
/// </summary>
public partial class SidebarView : UserControl
{
    // Liquid Glass accent colors
    private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);       // #6faadd
    private static readonly Color AccentBgColor = Color.FromArgb(0x1E, 0x64, 0xb4, 0xff); // rgba(100,180,255,0.12)

    private static readonly SolidColorBrush AccentTextBrush = new(AccentColor);
    private static readonly SolidColorBrush SecondaryTextBrush =
        new(Color.FromRgb(0x99, 0x99, 0x99));                                         // ~rgba(255,255,255,0.60)
    private static readonly SolidColorBrush AccentBackgroundBrush = new(AccentBgColor);
    private static readonly SolidColorBrush HoverBackgroundBrush =
        new(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));                                  // rgba(255,255,255,0.06)

    private MainViewModel? _viewModel;
    private readonly List<Button> _itemButtons = new();
    private readonly Dictionary<Button, MainPanelType> _buttonToPanel = new();
    private readonly Dictionary<Button, Ellipse> _buttonToActiveDot = new();
    private bool _isLoaded;

    public SidebarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        Dispatcher.BeginInvoke(
            new System.Action(() =>
            {
                CacheItemButtons();
                AttachViewModel();
                RefreshSelectionVisuals();
            }),
            System.Windows.Threading.DispatcherPriority.Loaded);
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

            // Find the active dot ellipse within the button template
            var dot = FindVisualChild<Ellipse>(button);
            if (dot != null)
                _buttonToActiveDot[button] = dot;
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

            // Update text/icon color
            button.Foreground = isSelected ? AccentTextBrush : SecondaryTextBrush;

            // Update background
            button.Background = isSelected ? AccentBackgroundBrush : Brushes.Transparent;

            // Show/hide active dot
            if (_buttonToActiveDot.TryGetValue(button, out var dot) && dot != null)
            {
                dot.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
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
        if (sender is not Button btn) return;
        var isSelected = _buttonToPanel.TryGetValue(btn, out var panel)
            && panel == _viewModel?.SelectedPanel;

        if (!isSelected)
            btn.Background = HoverBackgroundBrush;
    }

    private void OnItemMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Button btn) return;
        var isSelected = _buttonToPanel.TryGetValue(btn, out var panel)
            && panel == _viewModel?.SelectedPanel;

        if (!isSelected)
            btn.Background = Brushes.Transparent;
        else
            btn.Background = AccentBackgroundBrush;
    }
}
