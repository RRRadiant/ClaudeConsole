using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Views.MCP;

/// <summary>
/// MCP Server list view — matches MCPServerListView.swift.
///
/// Displays a scrollable list of MCPServerCard instances, an add button that
/// reveals the MCPServerEditorView, and an empty state when no servers exist.
/// Orchestrates loading from the MCPManagerViewModel and delegates card events.
/// </summary>
public partial class MCPServerListView : UserControl
{
    private MCPManagerViewModel? _vm;

    public MCPServerListView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = App.Services.GetService(typeof(MCPManagerViewModel)) as MCPManagerViewModel;
        if (_vm == null) return;

        DataContext = _vm;

        // Initial load (LoadServers is synchronous)
        _vm.LoadServers();
        RefreshServerCards();

        // Listen for server list changes
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MCPManagerViewModel.Servers))
                Dispatcher.Invoke(RefreshServerCards);
        };
    }

    // ── Server List Refresh ────────────────────────────────────────────

    /// <summary>
    /// Rebuild the server card list from the ViewModel.
    /// Shows the empty state when there are no servers and the editor is hidden.
    /// </summary>
    private void RefreshServerCards()
    {
        if (_vm == null) return;

        ServerCardsPanel.Children.Clear();

        if (_vm.Servers.Count == 0 && !_vm.IsAddingServer)
        {
            EmptyStateView.Visibility = Visibility.Visible;
            return;
        }

        EmptyStateView.Visibility = Visibility.Collapsed;

        foreach (var server in _vm.Servers)
        {
            var card = CreateServerCard(server);
            ServerCardsPanel.Children.Add(card);
        }
    }

    /// <summary>
    /// Create a single MCPServerCard for the given server config.
    /// Wires up all card events (test connection, edit, delete).
    /// </summary>
    private MCPServerCard CreateServerCard(MCPServerConfig server)
    {
        var card = new MCPServerCard
        {
            Server = server,
            ConnectionResult = _vm!.ConnectionResultFor(server)
        };

        // Test connection from card → delegate to ViewModel
        card.TestConnectionRequested += async (srv) =>
        {
            await _vm!.TestServerConnectionAsync(srv);
            // Refresh this specific card (the VM will update ConnectionResults)
            card.ConnectionResult = _vm.ConnectionResultFor(srv);
        };

        // Configure / edit → show editor pre-filled with this server
        card.ConfigureRequested += (srv) =>
        {
            ShowEditor(srv);
        };

        // Delete → remove from VM and refresh
        card.DeleteRequested += (srv) =>
        {
            _vm!.DeleteServer(srv);
            RefreshServerCards();
        };

        return card;
    }

    // ── Editor ─────────────────────────────────────────────────────────

    private void OnAddServer(object sender, RoutedEventArgs e)
    {
        ShowEditor(null);
    }

    /// <summary>
    /// Show the MCPServerEditorView. If <paramref name="server"/> is non-null,
    /// the editor is pre-populated for editing; otherwise it is a blank add form.
    /// </summary>
    private void ShowEditor(MCPServerConfig? server)
    {
        if (_vm == null) return;

        EditorView.Visibility = Visibility.Visible;
        EmptyStateView.Visibility = Visibility.Collapsed;

        if (server != null)
        {
            // Editing mode
            _vm.StartEditing(server);
            EditorView.Configure(_vm, OnEditorSave, OnEditorCancel, isEditing: true);
        }
        else
        {
            // Adding mode
            _vm.IsAddingServer = true;
            EditorView.Configure(_vm, OnEditorSave, OnEditorCancel, isEditing: false);
        }
    }

    /// <summary>
    /// Called by the editor when the user clicks Save.
    /// Persists the form data via the ViewModel and refreshes the card list.
    /// </summary>
    private void OnEditorSave(MCPManagerViewModel vm)
    {
        try
        {
            vm.SaveServer();
            EditorView.Visibility = Visibility.Collapsed;
            EditorView.ClearFields();
            vm.ResetForm();
            RefreshServerCards();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MCPServerListView] OnEditorSave failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Called by the editor when the user clicks Cancel.
    /// Resets the form and hides the editor.
    /// </summary>
    private void OnEditorCancel()
    {
        EditorView.Visibility = Visibility.Collapsed;
        EditorView.ClearFields();
        _vm?.ResetForm();
        RefreshServerCards();
    }

    // ── Navigation ─────────────────────────────────────────────────────

    private void OnBack(object sender, RoutedEventArgs e)
    {
        var mainVm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel;
        if (mainVm != null)
            mainVm.SelectedPanel = MainPanelType.Dashboard;
    }
}
