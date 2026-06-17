using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Views.MCP;

/// <summary>
/// Editor form for adding or editing an MCP server.
/// Matches MCPServerEditorView.swift exactly:
/// name, command, args (dynamic add/remove), env vars (dynamic key-value), save/cancel.
/// </summary>
public partial class MCPServerEditorView : UserControl
{
    private Action<MCPManagerViewModel>? _onSave;
    private Action? _onCancel;

    private MCPManagerViewModel? _vm;
    private readonly List<(string Key, string Value)> _formEnv = new();

    public MCPServerEditorView()
    {
        InitializeComponent();
    }

    // ── Initialisation ──────────────────────────────────────────────────

    /// <summary>
    /// Wire up the editor to a ViewModel and configure callbacks.
    /// Call this before showing the editor.
    /// </summary>
    public void Configure(
        MCPManagerViewModel vm,
        Action<MCPManagerViewModel> onSave,
        Action onCancel,
        bool isEditing = false)
    {
        _vm = vm;
        _onSave = onSave;
        _onCancel = onCancel;

        // Update title
        if (EditorCard != null)
            EditorCard.Title = isEditing ? "编辑 MCP 服务器" : "添加 MCP 服务器";

        // Populate fields from ViewModel
        if (ServerName != null)
            ServerName.Text = vm.NewName;
        if (ServerCommand != null)
            ServerCommand.Text = vm.NewCommand;
        if (ArgInput != null)
            ArgInput.Text = "";

        _formEnv.Clear();
        _formEnv.AddRange(vm.NewEnv);

        RefreshArgList();
        RefreshEnvList();
    }

    // ── Args ────────────────────────────────────────────────────────────

    private void OnAddArgClicked(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var val = ArgInput?.Text?.Trim();
        if (string.IsNullOrEmpty(val)) return;

        _vm.NewArgInput = val;
        _vm.AddArg();
        if (ArgInput != null) ArgInput.Text = "";
        RefreshArgList();
    }

    private void RefreshArgList()
    {
        if (ArgsPanel == null || _vm == null) return;
        ArgsPanel.Children.Clear();

        foreach (var (arg, idx) in _vm.NewArgs.Select((a, i) => (a, i)))
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };

            row.Children.Add(new TextBlock
            {
                Text = arg,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var removeBtn = new Button
            {
                Content = new TextBlock
                {
                    Text = "",  // Cancel / xmark
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 12
                },
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x59, 0x59, 0x59)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = idx,
                Margin = new Thickness(8, 0, 0, 0)
            };
            removeBtn.Click += (s, e) =>
            {
                if (s is Button b && b.Tag is int i)
                {
                    _vm?.RemoveArg(i);
                    RefreshArgList();
                }
            };
            row.Children.Add(removeBtn);
            ArgsPanel.Children.Add(row);
        }
    }

    // ── Env Vars ────────────────────────────────────────────────────────

    private void OnAddEnvClicked(object sender, RoutedEventArgs e)
    {
        var key = EnvKeyInput?.Text?.Trim();
        var val = EnvValueInput?.Text?.Trim();
        if (string.IsNullOrEmpty(key)) return;

        _formEnv.Add((key, val ?? ""));
        if (EnvKeyInput != null) EnvKeyInput.Text = "";
        if (EnvValueInput != null) EnvValueInput.Text = "";
        RefreshEnvList();
    }

    private void RefreshEnvList()
    {
        if (EnvPanel == null) return;
        EnvPanel.Children.Clear();

        foreach (var (pair, idx) in _formEnv.Select((p, i) => (p, i)))
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };

            row.Children.Add(new TextBlock
            {
                Text = $"{pair.Key}={pair.Value}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var removeBtn = new Button
            {
                Content = new TextBlock
                {
                    Text = "",  // Cancel / xmark
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 12
                },
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x59, 0x59, 0x59)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = idx,
                Margin = new Thickness(8, 0, 0, 0)
            };
            removeBtn.Click += (s, e) =>
            {
                if (s is Button b && b.Tag is int i && i < _formEnv.Count)
                {
                    _formEnv.RemoveAt(i);
                    RefreshEnvList();
                }
            };
            row.Children.Add(removeBtn);
            EnvPanel.Children.Add(row);
        }
    }

    // ── Save / Cancel ───────────────────────────────────────────────────

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.NewName = ServerName?.Text ?? "";
        _vm.NewCommand = ServerCommand?.Text ?? "";
        _vm.NewEnv = new List<(string, string)>(_formEnv);
        _onSave?.Invoke(_vm);
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        _onCancel?.Invoke();
    }

    /// <summary>
    /// Clear all input fields (used when hiding the editor).
    /// </summary>
    public void ClearFields()
    {
        if (ServerName != null) ServerName.Text = "";
        if (ServerCommand != null) ServerCommand.Text = "";
        if (ArgInput != null) ArgInput.Text = "";
        if (EnvKeyInput != null) EnvKeyInput.Text = "";
        if (EnvValueInput != null) EnvValueInput.Text = "";
        _formEnv.Clear();
        RefreshArgList();
        RefreshEnvList();
    }
}
