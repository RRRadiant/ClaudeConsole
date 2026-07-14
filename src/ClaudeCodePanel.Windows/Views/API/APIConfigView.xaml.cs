using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Views.API;

/// <summary>
/// API Configuration panel — the most complex panel in the application
/// (matching the 337-line APIConfigView.swift).
///
/// Sections:
///   1. Back button (chevron left + "概览") dispatching Navigate(Dashboard)
///   2. Provider selection card — 4 providers with icons, checkmarks, accent highlight
///   3. API Key card — secure field + copy + Credential Manager indicator
///   4. Advanced options — expander with Base URL, Max Tokens, Timeout
///   5. Models card — enabled models, detection, recommended models
///   6. Connection status indicator
///   7. Action buttons — "测试连接" (secondary) + "保存配置" (primary)
/// </summary>
public partial class APIConfigView : UserControl
{
    // ── Constants ──────────────────────────────────────────────────────────

    private static readonly Color DangerColor = Color.FromRgb(0xcf, 0x6b, 0x6b);   // #cf6b6b

    /// <summary>Resolve a theme resource brush at runtime (theme-aware).</summary>
    private static SolidColorBrush T(string key)
    {
        return (Application.Current?.TryFindResource(key) as SolidColorBrush)
               ?? new SolidColorBrush(Colors.Gray);
    }

    // ── State ──────────────────────────────────────────────────────────────

    private APIConfigViewModel? _vm;

    // ── Constructor ────────────────────────────────────────────────────────

    public APIConfigView()
    {
        InitializeComponent();
    }

    // ── Loaded ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetService(typeof(APIConfigViewModel)) as APIConfigViewModel;
        if (_vm == null) return;

        DataContext = _vm;
        ApplyLoadedConfig();
    }

    /// <summary>
    /// Applies the ViewModel's loaded configuration to all UI controls.
    /// Called once on Loaded and again after provider changes.
    /// </summary>
    private void ApplyLoadedConfig()
    {
        if (_vm == null) return;

        _vm.LoadConfig();

        // Sync ViewModel -> UI fields
        ApiKeyField.Text = _vm.ApiKey;
        BaseUrlField.Text = _vm.BaseURL;
        MaxTokensField.Text = _vm.MaxTokens.ToString();
        TimeoutField.Text = _vm.Timeout.ToString();

        // Build provider row buttons
        BuildProviderRows();

        // Update API key status indicator
        UpdateKeyStatus();

        // Refresh model lists
        RefreshModelLists();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 1: Back Button
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigates back to the Dashboard panel via MainViewModel.
    /// Matches Swift: onBack?() dispatches Navigate(Dashboard).
    /// </summary>
    private void OnBack(object sender, RoutedEventArgs e)
    {
        var mainVm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel;
        mainVm?.Navigate(MainPanelType.Dashboard);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 2: Provider Selection
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds the four provider selection rows programmatically, matching the
    /// Swift ForEach(APIProvider.allCases) loop.
    ///
    /// Each row: icon glyph + provider display name + default base URL subtitle
    /// + checkmark if selected + accent background highlight when selected.
    /// </summary>
    private void BuildProviderRows()
    {
        if (_vm == null) return;

        ProviderList.Children.Clear();

        var providers = APIProviderExtensions.AllCases;
        for (int i = 0; i < providers.Length; i++)
        {
            var provider = providers[i];
            var isSelected = _vm.SelectedProvider == provider;

            // ── Row button ───────────────────────────────────────────────
            var rowBtn = new Button
            {
                Tag = provider,
                Padding = new Thickness(12),
                Background = isSelected ? T("SelectionBackgroundBrush") : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            rowBtn.Click += OnProviderClick;

            // ── Row layout grid ──────────────────────────────────────────
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // icon
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name+url
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // checkmark

            // Icon
            var icon = new TextBlock
            {
                Text = provider.IconGlyph(),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = isSelected ? T("AccentBrush") : T("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);
            rowGrid.Children.Add(icon);

            // Name + Base URL stack
            var textStack = new StackPanel
            {
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            textStack.Children.Add(new TextBlock
            {
                Text = provider.DisplayName(),
                FontSize = 17,
                Foreground = T("TextPrimaryBrush")
            });
            var baseURL = provider.DefaultBaseURL();
            if (string.IsNullOrEmpty(baseURL))
                baseURL = "自定义 API 端点";
            textStack.Children.Add(new TextBlock
            {
                Text = baseURL,
                FontSize = 12,
                Foreground = T("TextSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(textStack, 1);
            rowGrid.Children.Add(textStack);

            // Checkmark (visible only when selected)
            var checkmark = new TextBlock
            {
                Text = "\xE10B", // CheckMark glyph
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = T("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed
            };
            Grid.SetColumn(checkmark, 2);
            rowGrid.Children.Add(checkmark);

            rowBtn.Content = rowGrid;
            ProviderList.Children.Add(rowBtn);

            // Divider between rows (except after last)
            if (i < providers.Length - 1)
            {
                var divider = new ClaudeCodePanel.Windows.Views.Shared.GlassDivider();
                ProviderList.Children.Add(divider);
            }
        }
    }

    /// <summary>
    /// Handles provider row click. Updates the ViewModel, rebuilds rows
    /// (for checkmark + highlight), and refreshes dependent UI sections.
    /// Matches Swift: viewModel.selectedProvider = provider; viewModel.providerChanged()
    /// </summary>
    private void OnProviderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not APIProvider provider || _vm == null) return;

        _vm.SelectedProvider = provider;
        _vm.ProviderChanged();

        // Rebuild provider rows to update highlights and checkmarks
        BuildProviderRows();

        // Refresh dependent fields
        BaseUrlField.Text = _vm.BaseURL;
        ApiKeyField.Text = _vm.ApiKey;
        UpdateKeyStatus();
        RefreshModelLists();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 3: API Key
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the key status indicator and copy button visibility.
    /// Shows "已保存到 Credential Manager，并同步到 Claude Code 配置" when a key is saved,
    /// or "输入后会保存并同步到 Claude Code 配置" otherwise.
    /// </summary>
    private void UpdateKeyStatus()
    {
        if (_vm == null) return;

        if (_vm.IsKeySaved)
        {
            KeyStatus.Label = "已保存到 Credential Manager，并同步到 Claude Code 配置";
            KeyStatus.Status = "Running";
        }
        else
        {
            KeyStatus.Label = "输入后会保存并同步到 Claude Code 配置";
            KeyStatus.Status = "Stopped";
        }

        // Show copy button only when there is key content
        CopyKeyBtn.Visibility = string.IsNullOrEmpty(ApiKeyField.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Copies the current API key value to the Windows clipboard.
    /// </summary>
    private void OnCopyKey(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ApiKeyField.Text))
        {
            Clipboard.SetText(ApiKeyField.Text);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 4: Advanced Options (Expander)
    // ═══════════════════════════════════════════════════════════════════════

    private void OnAdvancedExpanded(object sender, RoutedEventArgs e)
    {
        if (_vm != null) _vm.ShowAdvancedOptions = true;
    }

    private void OnAdvancedCollapsed(object sender, RoutedEventArgs e)
    {
        if (_vm != null) _vm.ShowAdvancedOptions = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 5: Models
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Detects available models from the provider API.
    /// Syncs fields from UI to ViewModel first, then calls DetectModelsAsync.
    /// Matches Swift: Task { await viewModel.detectModels() }
    /// </summary>
    private async void OnDetectModels(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;

        SyncFieldsToViewModel();

        // Show detecting state
        DetectionMsg.Text = "正在检测可用模型...";
        DetectingPanel.Visibility = Visibility.Visible;
        DetectionActionsPanel.Visibility = Visibility.Collapsed;

        await _vm.DetectModelsAsync();
        // Errors are surfaced via _vm.DetectionMessage — no need to catch here

        // Restore normal state
        DetectingPanel.Visibility = Visibility.Collapsed;
        DetectionActionsPanel.Visibility = Visibility.Visible;
        DetectionMsg.Text = _vm.DetectionMessage ?? "";

        RefreshModelLists();
    }

    /// <summary>
    /// Enables all detected models at once.
    /// Matches Swift: viewModel.enableAllDetectedModels()
    /// </summary>
    private void OnEnableAllModels(object sender, RoutedEventArgs e)
    {
        _vm?.EnableAllDetectedModels();
        RefreshModelLists();
    }

    /// <summary>
    /// Clears all enabled models (but keeps the detected list intact).
    /// Matches Swift: viewModel.clearEnabledModels()
    /// </summary>
    private void OnClearModels(object sender, RoutedEventArgs e)
    {
        _vm?.ClearEnabledModels();
        RefreshModelLists();
    }

    /// <summary>
    /// Rebuilds all model-related UI sections: enabled models list,
    /// detected models list, recommended models list, button visibility,
    /// and the count label.
    /// </summary>
    private void RefreshModelLists()
    {
        if (_vm == null) return;

        EnabledModelsPanel.Children.Clear();
        DetectedModelsPanel.Children.Clear();
        RecommendedModelsPanel.Children.Clear();

        var enabledCount = _vm.EnabledModels.Count;
        EnabledModelCountLabel.Text = $"{enabledCount} 个已启用";
        NoModelsHint.Visibility = enabledCount == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Toggle action button visibility
        EnableAllBtn.Visibility = _vm.DetectedModels.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearModelsBtn.Visibility = enabledCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        // ── Enabled models (with remove button) ────────────────────────
        foreach (var model in _vm.EnabledModels.OrderBy(m => m))
        {
            EnabledModelsPanel.Children.Add(CreateEnabledModelCard(model));
        }

        // ── Detected models (not yet enabled, with add button) ─────────
        bool hasDetectedSection = false;
        foreach (var model in _vm.DetectedModels)
        {
            if (!_vm.EnabledModels.Contains(model))
            {
                if (!hasDetectedSection)
                {
                    // Section header: "检测到的模型"
                    DetectedModelsPanel.Children.Add(new TextBlock
                    {
                        Text = "检测到的模型",
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = T("TextSecondaryBrush"),
                        Margin = new Thickness(0, 4, 0, 4)
                    });
                    hasDetectedSection = true;
                }
                DetectedModelsPanel.Children.Add(CreateAddModelRow(model, isDetected: true));
            }
        }

        // ── Recommended models (when no detection result yet) ──────────
        if (_vm.DetectedModels.Count == 0 && _vm.AvailableModels.Count > 0)
        {
            RecommendedModelsPanel.Children.Add(new TextBlock
            {
                Text = "推荐模型",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = T("TextSecondaryBrush"),
                Margin = new Thickness(0, 4, 0, 4)
            });

            foreach (var model in _vm.AvailableModels)
            {
                if (!_vm.EnabledModels.Contains(model))
                {
                    RecommendedModelsPanel.Children.Add(CreateAddModelRow(model, isDetected: false));
                }
            }
        }
    }

    /// <summary>
    /// Creates a compact GlassCard-style border for an enabled model,
    /// showing the model name + description + a red minus-circle remove button.
    /// Matches Swift: GlassCard(variant: .compact) with minus.circle.fill
    /// </summary>
    private Border CreateEnabledModelCard(string model)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 0),
            Background = T("GlassCardBgBrush"),
            BorderBrush = T("BorderCardBrush"),
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Model name + description
        var textStack = new StackPanel();
        textStack.Children.Add(new TextBlock
        {
            Text = model,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = T("TextPrimaryBrush")
        });

        var desc = ModelDescription(model);
        if (!string.IsNullOrEmpty(desc))
        {
            textStack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Foreground = T("TextSecondaryBrush"),
                Margin = new Thickness(0, 3, 0, 0)
            });
        }

        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        // Red minus-circle remove button
        var removeBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "\xE15B", // MinusCircle glyph
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(DangerColor),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = model,
            VerticalAlignment = VerticalAlignment.Center
        };
        removeBtn.Click += (s, e) =>
        {
            if (s is Button b && b.Tag is string m)
            {
                _vm?.EnabledModels.Remove(m);
                RefreshModelLists();
            }
        };

        Grid.SetColumn(removeBtn, 1);
        grid.Children.Add(removeBtn);

        border.Child = grid;
        return border;
    }

    /// <summary>
    /// Creates a model row with an accent-colored plus-circle add button.
    /// Used for both detected models and recommended models.
    /// Matches Swift: plus.circle with accentColor foreground
    /// </summary>
    private Grid CreateAddModelRow(string model, bool isDetected)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, 2, 0, 2)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Model name
        var nameBlock = new TextBlock
        {
            Text = model,
            FontSize = 16,
            Foreground = T("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Description subtitle for detected/recommended models (lighter)
        var desc = ModelDescription(model);
        if (!string.IsNullOrEmpty(desc))
        {
            var modelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            modelStack.Children.Add(nameBlock);
            modelStack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Foreground = T("TextSecondaryBrush")
            });
            Grid.SetColumn(modelStack, 0);
            grid.Children.Add(modelStack);
        }
        else
        {
            Grid.SetColumn(nameBlock, 0);
            grid.Children.Add(nameBlock);
        }

        // Green/Accent plus-circle add button
        var addBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "\xE109", // PlusCircle glyph
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = T("AccentBrush"),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = model,
            VerticalAlignment = VerticalAlignment.Center
        };
        addBtn.Click += (s, e) =>
        {
            if (s is Button b && b.Tag is string m)
            {
                _vm?.EnabledModels.Add(m);
                RefreshModelLists();
            }
        };

        Grid.SetColumn(addBtn, 1);
        grid.Children.Add(addBtn);

        return grid;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 6: Connection Status
    // ═══════════════════════════════════════════════════════════════════════
    //  (ConnStatus is updated inline in OnTestConnection / OnSaveConfig)
    // ═══════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════
    //  Section 7: Action Buttons — Test Connection + Save Config
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests the API connection using the configured provider, base URL, and API key.
    /// Shows "测试中…" while in progress, then "连接成功" or the error message.
    /// Matches Swift: AsyncButton calling viewModel.testConnection()
    /// </summary>
    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;

        SyncFieldsToViewModel();

        // Show testing state
        ConnStatus.Label = "测试中…";
        ConnStatus.Status = "Running";

        await _vm.TestConnectionAsync();

        // Reflect result
        if (_vm.ConnectionStatus == ConnectionStatus.Success)
        {
            ConnStatus.Label = "连接成功";
            ConnStatus.Status = "Running";
        }
        else
        {
            ConnStatus.Label = _vm.ConnectionStatusMessage ?? "连接失败";
            ConnStatus.Status = "Error";
        }
    }

    /// <summary>
    /// Saves the current configuration to settings.json and Credential Manager.
    /// Matches Swift: AsyncButton calling viewModel.saveConfig()
    /// </summary>
    private async void OnSaveConfig(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;

        SyncFieldsToViewModel();
        await _vm.SaveConfigAsync();

        // Update key status after save
        UpdateKeyStatus();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Syncs current UI field values into the ViewModel before performing
    /// operations (test connection, save config, model detection).
    /// </summary>
    private void SyncFieldsToViewModel()
    {
        if (_vm == null) return;

        _vm.ApiKey = ApiKeyField.Text;
        _vm.BaseURL = BaseUrlField.Text;

        if (int.TryParse(MaxTokensField.Text, out var mt))
            _vm.MaxTokens = mt;

        if (int.TryParse(TimeoutField.Text, out var to))
            _vm.Timeout = to;
    }

    /// <summary>
    /// Returns a human-readable Chinese description for a model based on its name.
    /// Matches Swift:
    ///   opus   -> "旗舰推理 · 最高精度"
    ///   sonnet -> "推荐平衡 · 日常使用"
    ///   haiku  -> "轻量快速 · 简单任务"
    /// </summary>
    private static string ModelDescription(string model)
    {
        var lower = model.ToLowerInvariant();
        if (lower.Contains("opus"))   return "旗舰推理 · 最高精度";
        if (lower.Contains("sonnet")) return "推荐平衡 · 日常使用";
        if (lower.Contains("haiku"))  return "轻量快速 · 简单任务";
        return "";
    }
}
