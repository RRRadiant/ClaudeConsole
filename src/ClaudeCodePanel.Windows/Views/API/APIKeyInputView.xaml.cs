using System.Windows;
using System.Windows.Controls;

namespace ClaudeCodePanel.Windows.Views.API;

/// <summary>
/// A reusable UserControl for entering a provider API key.
/// Matches the Swift APIKeyInputView design.
///
/// Header row shows either:
///   - A green status indicator with "已加密存储于 Credential Manager" when IsKeySaved is true
///   - A tertiary hint "输入后自动加密存储" when no key is saved
///
/// A copy-to-clipboard button appears on the right when the ApiKey text is non-empty.
/// The input field is a GlassTextField with Variant="Secure" for password-style entry.
///
/// Usage from XAML:
///   &lt;api:APIKeyInputView ApiKey="{Binding SomeKey}" IsKeySaved="True"
///                          ProviderName="Anthropic"/&gt;
/// </summary>
public partial class APIKeyInputView : UserControl
{
    // ── Dependency Properties ──────────────────────────────────────────────

    public static readonly DependencyProperty ApiKeyProperty =
        DependencyProperty.Register(
            nameof(ApiKey),
            typeof(string),
            typeof(APIKeyInputView),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnApiKeyChanged));

    public static readonly DependencyProperty IsKeySavedProperty =
        DependencyProperty.Register(
            nameof(IsKeySaved),
            typeof(bool),
            typeof(APIKeyInputView),
            new PropertyMetadata(false, OnIsKeySavedChanged));

    public static readonly DependencyProperty ProviderNameProperty =
        DependencyProperty.Register(
            nameof(ProviderName),
            typeof(string),
            typeof(APIKeyInputView),
            new PropertyMetadata(string.Empty, OnProviderNameChanged));

    // ── CLR Wrappers ───────────────────────────────────────────────────────

    public string ApiKey
    {
        get => (string)GetValue(ApiKeyProperty);
        set => SetValue(ApiKeyProperty, value);
    }

    public bool IsKeySaved
    {
        get => (bool)GetValue(IsKeySavedProperty);
        set => SetValue(IsKeySavedProperty, value);
    }

    public string ProviderName
    {
        get => (string)GetValue(ProviderNameProperty);
        set => SetValue(ProviderNameProperty, value);
    }

    // ── Constructor ────────────────────────────────────────────────────────

    public APIKeyInputView()
    {
        InitializeComponent();
    }

    // ── Loaded ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies initial dependency-property values to the child controls once
    /// the visual tree is ready.  This catches values that were set before
    /// InitializeComponent completed.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Sync the GlassTextField with the current ApiKey DP value
        SyncTextFieldFromProperty();

        // Apply the initial status indicator state
        ApplyKeySavedState();

        // Apply the placeholder text
        ApplyPlaceholder();

        // Text changes are handled via the existing binding to ApiKey property

        // Initial visibility of the copy button
        UpdateCopyButtonVisibility();
    }

    // ── Property Changed Callbacks ─────────────────────────────────────────

    private static void OnApiKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (APIKeyInputView)d;
        var newValue = (string)e.NewValue;

        // Push the new value into the GlassTextField (avoiding re-entrant loops)
        if (view.IsLoaded && view.ApiKeyField != null)
            view.SyncTextFieldFromProperty();

        view.UpdateCopyButtonVisibility();
    }

    private static void OnIsKeySavedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (APIKeyInputView)d;
        if (view.IsLoaded)
            view.ApplyKeySavedState();
    }

    private static void OnProviderNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (APIKeyInputView)d;
        if (view.IsLoaded)
            view.ApplyPlaceholder();
    }

    // ── Text Field Sync ────────────────────────────────────────────────────

    /// <summary>
    /// Copies the ApiKey dependency property value into the GlassTextField.
    /// Called when the DP changes externally (e.g. a parent ViewModel binding).
    /// </summary>
    private void SyncTextFieldFromProperty()
    {
        if (ApiKeyField == null) return;

        if (ApiKeyField.Text != (ApiKey ?? string.Empty))
            ApiKeyField.Text = ApiKey ?? string.Empty;
    }

    // ── Status Indicator ───────────────────────────────────────────────────

    /// <summary>
    /// Updates the StatusIndicator label and color based on IsKeySaved.
    ///   - true  → green "Running" status, "已加密存储于 Credential Manager"
    ///   - false → gray "Stopped" status, "输入后自动加密存储"
    /// </summary>
    private void ApplyKeySavedState()
    {
        if (KeyStatus == null) return;

        if (IsKeySaved)
        {
            KeyStatus.Label = "已加密存储于 Credential Manager";
            KeyStatus.Status = "Running";
        }
        else
        {
            KeyStatus.Label = "输入后自动加密存储";
            KeyStatus.Status = "Stopped";
        }
    }

    // ── Placeholder ────────────────────────────────────────────────────────

    /// <summary>
    /// Formats the GlassTextField placeholder as "输入 {ProviderName} API Key".
    /// When ProviderName is empty the placeholder reads "输入 API Key".
    /// </summary>
    private void ApplyPlaceholder()
    {
        if (ApiKeyField == null) return;

        var provider = ProviderName ?? string.Empty;
        ApiKeyField.Placeholder = string.IsNullOrWhiteSpace(provider)
            ? "输入 API Key"
            : $"输入 {provider.Trim()} API Key";
    }

    // ── Copy to Clipboard ──────────────────────────────────────────────────

    /// <summary>
    /// Copies the current ApiKey value to the Windows clipboard.
    /// </summary>
    private void OnCopyKey(object sender, RoutedEventArgs e)
    {
        var key = ApiKey ?? string.Empty;
        if (!string.IsNullOrEmpty(key))
        {
            Clipboard.SetText(key);
        }
    }

    /// <summary>
    /// Shows the copy button when the ApiKey text is non-empty; hides it otherwise.
    /// </summary>
    private void UpdateCopyButtonVisibility()
    {
        if (CopyKeyBtn == null) return;

        CopyKeyBtn.Visibility = string.IsNullOrEmpty(ApiKey)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
