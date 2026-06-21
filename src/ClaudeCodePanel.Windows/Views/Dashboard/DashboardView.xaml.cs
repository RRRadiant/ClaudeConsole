using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ClaudeCodePanel.Windows.ViewModels;
using ClaudeCodePanel.Windows.Views.Shared;

namespace ClaudeCodePanel.Windows.Views.Dashboard;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _vm;
    private StatusIndicator? _apiStatus;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private void OnApiStatusLoaded(object sender, RoutedEventArgs e)
    {
        _apiStatus = sender as StatusIndicator;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        // Guard against multiple Loaded firings (e.g. tab switches)
        Loaded -= OnLoadedAsync;

        _vm = App.Services.GetService(typeof(DashboardViewModel)) as DashboardViewModel;
        if (_vm == null) return;

        DataContext = _vm;
        try
        {
            await _vm.LoadSummaryAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DashboardView] LoadSummaryAsync failed: {ex.Message}");
        }

        ApplyStatus();

        // React to future summary changes (e.g. external sync updates)
        _vm.PropertyChanged += (_, args) =>
        {
            Dispatcher.Invoke(ApplyStatus);
        };
    }

    private void ApplyStatus()
    {
        if (_vm == null) return;

        // -- Claude version indicator --
        if (_vm.Summary.IsClaudeInstalled)
        {
            ClaudeStatus.Status = "Running";
            ClaudeStatus.Label = _vm.Summary.ClaudeVersion;
        }
        else
        {
            ClaudeStatus.Status = "Stopped";
            ClaudeStatus.Label = "未安装";
        }

        // -- API connection indicator --
        if (_vm.Summary.ApiConnected)
        {
            _apiStatus!.Status = "Running";
            _apiStatus!.Label = "已连接";
        }
        else
        {
            _apiStatus!.Status = "Stopped";
            _apiStatus!.Label = "未配置";
        }
    }
}
