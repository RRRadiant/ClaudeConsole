using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Design;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeCodePanel.Windows.Views.Dashboard;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _vm;
    private bool _subscriptionsAttached;
    private bool _hasAnimatedEvents;
    private bool _refreshQueued;
    private string _lastEventsSignature = string.Empty;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
        Unloaded += OnUnloaded;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
        if (_vm == null) return;

        DataContext = _vm;
        AttachSubscriptions();

        await ReloadSummaryAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachSubscriptions();
    }

    private void AttachSubscriptions()
    {
        if (_vm == null || _subscriptionsAttached)
            return;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.Summary.PropertyChanged += OnSummaryPropertyChanged;
        FileWatcherService.Instance.OnChange += OnConfigFileChanged;
        _subscriptionsAttached = true;
    }

    private void DetachSubscriptions()
    {
        if (_vm == null || !_subscriptionsAttached)
            return;

        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm.Summary.PropertyChanged -= OnSummaryPropertyChanged;
        FileWatcherService.Instance.OnChange -= OnConfigFileChanged;
        _subscriptionsAttached = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(DashboardViewModel.Summary) || _vm == null)
            return;

        _vm.Summary.PropertyChanged -= OnSummaryPropertyChanged;
        _vm.Summary.PropertyChanged += OnSummaryPropertyChanged;
        RefreshSummaryUi();
    }

    private void OnSummaryPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        QueueSummaryRefresh();
    }

    private void QueueSummaryRefresh()
    {
        if (_refreshQueued)
            return;

        _refreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _refreshQueued = false;
            RefreshSummaryUi();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnConfigFileChanged(string path)
    {
        var configService = ConfigFileService.Instance;
        if (!path.Equals(configService.SettingsPath, StringComparison.OrdinalIgnoreCase) &&
            !path.Equals(configService.SettingsLocalPath, StringComparison.OrdinalIgnoreCase) &&
            !path.Equals(configService.ClaudeGlobalConfigPath, StringComparison.OrdinalIgnoreCase) &&
            !path.Equals(configService.McpPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = ReloadSummaryAsync();
    }

    private async Task ReloadSummaryAsync()
    {
        if (_vm == null)
            return;

        try
        {
            _hasAnimatedEvents = false;
            await _vm.LoadSummaryAsync();
            RefreshSummaryUi();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DashboardView] LoadSummaryAsync failed: {ex.Message}");
        }
    }

    private void RefreshSummaryUi()
    {
        ApplyCardData();
        AnimateProgressBars();
        RefreshEventsList();
    }

    /// <summary>Fills card values from the ViewModel summary.</summary>
    private void ApplyCardData()
    {
        if (_vm?.Summary == null) return;

        var s = _vm.Summary;

        // ── Card 1: Claude CLI ──
        ClaudeStatusDot.Fill = s.IsClaudeInstalled
            ? (Brush)FindResource("StatusSuccessBrush")
            : (Brush)FindResource("StatusErrorBrush");

        ClaudeValueText.Text = s.IsClaudeInstalled
            ? s.ClaudeVersion
            : "未安装";

        // ── Card 2: API ──
        ApiStatusDot.Fill = s.ApiConnected
            ? (Brush)FindResource("StatusSuccessBrush")
            : (Brush)FindResource("StatusErrorBrush");

        ApiValueText.Text = s.ApiConnected
            ? s.ApiProvider
            : "未配置";

        // ── Card 3: Skills ──
        SkillsStatusDot.Fill = s.InstalledSkillsCount > 0
            ? (Brush)FindResource("StatusSuccessBrush")
            : (Brush)FindResource("TextTertiaryBrush");

        SkillsValueText.Text = s.InstalledSkillsCount.ToString(CultureInfo.InvariantCulture);

        // ── Card 4: MCP ──
        McpStatusDot.Fill = s.ActiveMCPServersCount > 0
            ? (Brush)FindResource("StatusSuccessBrush")
            : (Brush)FindResource("TextTertiaryBrush");

        McpValueText.Text = $"{s.ActiveMCPServersCount}/{s.TotalMCPServersCount}";
    }

    /// <summary>Uses a short transition from the current width to avoid repeated full animations.</summary>
    private void AnimateProgressBars()
    {
        if (_vm?.Summary == null) return;

        var s = _vm.Summary;

        // Determine a reasonable max width based on the card's content area
        double maxWidth = ActualWidth > 0 ? (ActualWidth - 96) / 2 - 40 : 200;

        AnimateBar(ClaudeProgressBar, s.IsClaudeInstalled ? 100 : 0, maxWidth);
        AnimateBar(ApiProgressBar, s.ApiConnected ? 100 : 0, maxWidth);
        AnimateBar(SkillsProgressBar, Math.Min(s.InstalledSkillsCount * 10, 100), maxWidth);
        AnimateBar(McpProgressBar,
            s.TotalMCPServersCount > 0
                ? (double)s.ActiveMCPServersCount / s.TotalMCPServersCount * 100
                : 0,
            maxWidth);
    }

    private static void AnimateBar(Rectangle bar, double percent, double maxWidth)
    {
        if (bar == null) return;

        double targetWidth = maxWidth * (percent / 100.0);

        var reduceEffects = UiPerformancePolicy.ShouldReduceEffects(
            SystemParameters.ClientAreaAnimation == false,
            SystemParameters.IsRemoteSession,
            RenderCapability.Tier >> 16);

        bar.BeginAnimation(WidthProperty, null);
        if (reduceEffects)
        {
            bar.Width = targetWidth;
            return;
        }

        var anim = new DoubleAnimation(bar.ActualWidth, targetWidth, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        bar.BeginAnimation(WidthProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>Rebinds the events list and triggers staggered entrance animation.</summary>
    private void RefreshEventsList()
    {
        if (_vm?.Summary?.RecentEvents == null) return;

        var events = _vm.Summary.RecentEvents;

        // Toggle empty state vs. list
        if (events.Count == 0)
        {
            EventsItemsControl.Visibility = Visibility.Collapsed;
            EventsEmptyState.Visibility = Visibility.Visible;
            return;
        }

        EventsEmptyState.Visibility = Visibility.Collapsed;
        EventsItemsControl.Visibility = Visibility.Visible;

        var signature = string.Join('|', events.Select(static item =>
            $"{item.Timestamp.Ticks}:{item.Type}:{item.Message}"));
        if (string.Equals(signature, _lastEventsSignature, StringComparison.Ordinal))
            return;

        _lastEventsSignature = signature;
        EventsItemsControl.ItemsSource = null;
        EventsItemsControl.ItemsSource = events;

        // Trigger staggered entrance after items are generated
        Dispatcher.BeginInvoke(new Action(PlayEventStaggeredEntrance),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Staggered slide-in entrance for event list items:
    /// each item Y +10→0, opacity 0→1, 50 ms delay increments, 300 ms ease-out.
    /// Also applies breathing pulse to each status dot.
    /// </summary>
    private void PlayEventStaggeredEntrance()
    {
        if (_hasAnimatedEvents) return;
        if (_vm?.Summary?.RecentEvents == null || _vm.Summary.RecentEvents.Count == 0)
            return;

        _hasAnimatedEvents = true;

        var items = _vm.Summary.RecentEvents;
        var containers = new List<FrameworkElement>();

        for (int i = 0; i < items.Count; i++)
        {
            var container = EventsItemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (container is FrameworkElement fe)
                containers.Add(fe);
        }

        for (int i = 0; i < containers.Count; i++)
        {
            var element = containers[i];
            var delay = i * 50;

            // Start state
            element.RenderTransform = new TranslateTransform(0, 10);
            element.Opacity = 0;

            var sb = new Storyboard { BeginTime = TimeSpan.FromMilliseconds(delay) };

            var slideIn = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slideIn, element);
            Storyboard.SetTargetProperty(slideIn,
                new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            sb.Children.Add(slideIn);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, element);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fadeIn);

            sb.Completed += (_, _) =>
            {
                element.RenderTransform = Transform.Identity;
            };

            sb.Begin();

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
}
