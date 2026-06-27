using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.ViewModels;
using ClaudeCodePanel.Windows.Views.Shared;

namespace ClaudeCodePanel.Windows.Views.Skills;

/// <summary>
/// Skills list view — port of SkillsListView.swift.
/// Header with back button, tab bar (我的技能 / 浏览), search bar,
/// installed list or marketplace grid, and install dialog trigger.
/// </summary>
public partial class SkillsListView : UserControl
{
    private SkillManagerViewModel? _vm;
    private SkillManagerViewModel.SkillTab _currentTab = SkillManagerViewModel.SkillTab.Installed;

    public SkillsListView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetService(typeof(SkillManagerViewModel)) as SkillManagerViewModel;
        if (_vm == null) return;

        DataContext = _vm;

        // Bind SearchBar text to ViewModel SearchQuery (two-way)
        var searchBinding = new Binding(nameof(SkillManagerViewModel.SearchQuery))
        {
            Source = _vm,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        SearchBar.SetBinding(SearchField.TextProperty, searchBinding);

        // Initial data load
        _vm.LoadInstalledSkills();
        RefreshInstalledList();

        try
        {
            await _vm.LoadMarketplaceSkillsAsync();
            RefreshMarketplaceGrid();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkillsListView] LoadMarketplaceSkillsAsync failed: {ex.Message}");
        }

        // React to VM property changes
        _vm.PropertyChanged += (_, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                switch (args.PropertyName)
                {
                    case nameof(SkillManagerViewModel.InstalledSkills):
                    case nameof(SkillManagerViewModel.FilteredInstalledSkills):
                        RefreshInstalledList();
                        break;
                    case nameof(SkillManagerViewModel.MarketplaceSkills):
                    case nameof(SkillManagerViewModel.FilteredMarketplaceSkills):
                    case nameof(SkillManagerViewModel.IsGithubUrl):
                        RefreshMarketplaceGrid();
                        break;
                    case nameof(SkillManagerViewModel.IsLoadingMarketplace):
                        LoadingSpinner.Visibility = _vm.IsLoadingMarketplace
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                        break;
                    case nameof(SkillManagerViewModel.SearchQuery):
                        // Trigger server-side search when on marketplace tab
                        if (_currentTab == SkillManagerViewModel.SkillTab.Marketplace)
                            _ = _vm.LoadMarketplaceSkillsAsync();
                        break;
                }
            });
        };
    }

    // ── Tab switching ──────────────────────────────────────────────────

    private void OnSwitchTab(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string tag) return;

        // Update both toggle states
        if (tag == "Installed")
        {
            InstalledTab.IsChecked = true;
            MarketplaceTab.IsChecked = false;
            _currentTab = SkillManagerViewModel.SkillTab.Installed;
            InstalledList.Visibility = Visibility.Visible;
            MarketplaceGrid.Visibility = Visibility.Collapsed;
            InstalledEmpty.Visibility = Visibility.Collapsed; // will be set by refresh
        }
        else
        {
            InstalledTab.IsChecked = false;
            MarketplaceTab.IsChecked = true;
            _currentTab = SkillManagerViewModel.SkillTab.Marketplace;
            InstalledList.Visibility = Visibility.Collapsed;
            MarketplaceGrid.Visibility = Visibility.Visible;
            MarketplaceEmpty.Visibility = Visibility.Collapsed;
        }

        // Keep VM in sync
        if (_vm != null)
            _vm.SelectedTab = _currentTab;

        RefreshCurrentTab();
    }

    // ── List refresh ───────────────────────────────────────────────────

    private void RefreshCurrentTab()
    {
        if (_currentTab == SkillManagerViewModel.SkillTab.Installed)
            RefreshInstalledList();
        else
            RefreshMarketplaceGrid();
    }

    private void RefreshInstalledList()
    {
        InstalledList.Items.Clear();
        if (_vm == null) return;

        var skills = _vm.FilteredInstalledSkills;
        if (skills.Count == 0)
        {
            InstalledEmpty.Visibility = Visibility.Visible;
            return;
        }

        InstalledEmpty.Visibility = Visibility.Collapsed;

        foreach (var skill in skills)
        {
            var card = CreateSkillCard(skill, SkillCardMode.Installed);
            InstalledList.Items.Add(card);
        }
    }

    private void RefreshMarketplaceGrid()
    {
        MarketplaceGrid.Items.Clear();
        if (_vm == null) return;

        // If GitHub URL detected, show a special install card first
        if (_vm.IsGithubUrl && _vm.GithubUrlSkill != null)
        {
            var urlCard = CreateSkillCard(_vm.GithubUrlSkill, SkillCardMode.Marketplace);
            urlCard.InstallClicked += async (_, _) =>
            {
                await _vm.InstallGithubUrlSkillAsync();
                RefreshInstalledList();
                RefreshMarketplaceGrid();
            };
            MarketplaceGrid.Items.Add(urlCard);
        }

        var skills = _vm.FilteredMarketplaceSkills.Count > 0
            ? _vm.FilteredMarketplaceSkills
            : _vm.MarketplaceSkills;

        if (skills.Count == 0 && !_vm.IsLoadingMarketplace && !_vm.IsGithubUrl)
        {
            MarketplaceEmpty.Visibility = Visibility.Visible;
            MarketplaceEmpty.Message = string.IsNullOrEmpty(_vm.ErrorMessage)
                ? "未找到相关 Skill"
                : $"加载失败: {_vm.ErrorMessage}";
            return;
        }

        MarketplaceEmpty.Visibility = Visibility.Collapsed;

        foreach (var skill in skills)
        {
            var card = CreateSkillCard(skill, SkillCardMode.Marketplace);
            MarketplaceGrid.Items.Add(card);
        }
    }

    // ── SkillCard factory ──────────────────────────────────────────────

    private SkillCard CreateSkillCard(SkillItem skill, SkillCardMode mode)
    {
        var card = new SkillCard
        {
            Skill = skill,
            Mode = mode
        };

        // Wire up callbacks
        card.ToggleClicked += (_, _) =>
        {
            _vm?.ToggleSkill(skill);
            RefreshInstalledList();
        };

        card.DeleteClicked += async (_, _) =>
        {
            if (_vm == null) return;
            await _vm.UninstallSkillAsync(skill);
            RefreshInstalledList();
            RefreshMarketplaceGrid();
        };

        card.InstallClicked += async (_, _) =>
        {
            if (_vm == null) return;
            await _vm.InstallSkillAsync(skill);
            RefreshInstalledList();
            RefreshMarketplaceGrid();
        };

        card.UninstallClicked += async (_, _) =>
        {
            if (_vm == null) return;
            await _vm.UninstallSkillAsync(skill);
            RefreshInstalledList();
            RefreshMarketplaceGrid();
        };

        return card;
    }

    // ── Install dialog ─────────────────────────────────────────────────

    private void OnShowInstallDialog(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.InstallSource = SkillSource.LocalPath;
            _vm.InstallPathOrURL = "";
        }
        InstallDialog.PathOrURL = "";
        InstallDialog.Visibility = Visibility.Visible;
    }

    private async void OnInstallSkill(object sender, RoutedEventArgs e)
    {
        if (_vm == null || string.IsNullOrWhiteSpace(InstallDialog.PathOrURL))
            return;

        _vm.InstallPathOrURL = InstallDialog.PathOrURL;
        _vm.InstallSource = InstallDialog.CurrentSource;
        try
        {
            await _vm.InstallFromSourceAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkillsListView] InstallFromSourceAsync failed: {ex.Message}");
            return;
        }
        InstallDialog.Visibility = Visibility.Collapsed;
        RefreshInstalledList();
    }

    private void OnCancelInstall(object sender, RoutedEventArgs e)
    {
        InstallDialog.Visibility = Visibility.Collapsed;
    }

    // ── Navigation ─────────────────────────────────────────────────────

    private void OnBack(object sender, RoutedEventArgs e)
    {
        var mainVm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel;
        if (mainVm != null)
            mainVm.SelectedPanel = MainPanelType.Dashboard;
    }
}
