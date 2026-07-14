using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeCodePanel.Windows.ViewModels;

/// <summary>
/// Connection-test status mirroring Swift's ConnectionStatus enum.
/// When status is <see cref="ConnectionStatus.Failed"/>,
/// <see cref="ConnectionStatusMessage"/> carries the error detail.
/// </summary>
public enum ConnectionStatus
{
    Unknown,
    Testing,
    Success,
    Failed
}

/// <summary>
/// API Configuration ViewModel — port of APIConfigViewModel.swift (368 lines).
/// Manages provider selection, API key storage, connection testing, and model detection.
/// </summary>
public partial class APIConfigViewModel : ObservableObject
{
    // ──────────────────────────────────────────────
    //  Injected services
    // ──────────────────────────────────────────────
    private readonly IConfigFileService _configFileService;
    private readonly ICredentialService _credentialService;
    private readonly ISyncService _syncService;
    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "ClaudeCodePanel-Windows/1.0" } }
    };

    // ──────────────────────────────────────────────
    //  Observable bindable properties
    // ──────────────────────────────────────────────

    [ObservableProperty]
    private APIProvider _selectedProvider = APIProvider.Anthropic;

    [ObservableProperty]
    private string _apiKey = "";

    [ObservableProperty]
    private string _baseURL = "";

    [ObservableProperty]
    private int _maxTokens = 8192;

    [ObservableProperty]
    private int _timeout = 60;

    [ObservableProperty]
    private HashSet<string> _enabledModels = new();

    [ObservableProperty]
    private List<string> _availableModels = new();

    [ObservableProperty]
    private string _customBaseURL = "";

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    private ConnectionStatus _connectionStatus = ConnectionStatus.Unknown;

    /// <summary>
    /// Human-readable detail when <see cref="ConnectionStatus"/> is
    /// <see cref="ConnectionStatus.Failed"/>; null otherwise.
    /// </summary>
    [ObservableProperty]
    private string? _connectionStatusMessage;

    [ObservableProperty]
    private bool _isKeySaved;

    [ObservableProperty]
    private bool _showAdvancedOptions;

    [ObservableProperty]
    private string? _errorMessage;

    // ──────────────────────────────────────────────
    //  Model-detection state
    // ──────────────────────────────────────────────

    /// <summary>Models returned by the API detection call (NOT auto-enabled).</summary>
    [ObservableProperty]
    private List<string> _detectedModels = new();

    [ObservableProperty]
    private bool _isDetectingModels;

    /// <summary>
    /// Message shown after detection completes (e.g. "Found 5 models" or error info).
    /// </summary>
    [ObservableProperty]
    private string? _detectionMessage;

    // ── Known Anthropic model catalog (update periodically) ──────
    private static readonly List<string> KnownAnthropicModels = new()
    {
        "claude-opus-4-8",
        "claude-opus-4-8-20250514",
        "claude-sonnet-4-6",
        "claude-sonnet-4-6-20250514",
        "claude-haiku-4-5",
        "claude-haiku-4-5-20251001",
        "claude-opus-4-5",
        "claude-sonnet-4-5",
        "claude-haiku-4-5-20250301",
    };

    // ──────────────────────────────────────────────
    //  Constructor
    // ──────────────────────────────────────────────

    public APIConfigViewModel(
        IConfigFileService configFileService,
        ICredentialService credentialService,
        ISyncService? syncService = null)
    {
        _configFileService = configFileService;
        _credentialService = credentialService;
        _syncService = syncService ?? SyncService.Instance;
    }

    // ──────────────────────────────────────────────
    //  URL helpers (private computed properties)
    // ──────────────────────────────────────────────

    /// <summary>
    /// The raw base URL (from settings or provider default), normalized to no trailing slash.
    /// </summary>
    private string RawBaseURL
    {
        get
        {
            var url = (string.IsNullOrEmpty(BaseURL) ? SelectedProvider.DefaultBaseURL() : BaseURL)
                .TrimEnd('/');
            return url;
        }
    }

    /// <summary>
    /// Strips /anthropic and trailing /v1 suffix if present — used for model detection.
    /// This prevents doubling up on API version paths when constructing /v1/models.
    /// </summary>
    private string BaseURLForModels
    {
        get
        {
            var url = RawBaseURL;

            // Strip DeepSeek Anthropic compatibility suffix
            if (url.EndsWith("/anthropic", StringComparison.OrdinalIgnoreCase))
            {
                url = url[..^"/anthropic".Length];
            }

            // Strip any trailing API version path so we don't double up
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                url = url[..^"/v1".Length];
            }

            return url;
        }
    }

    // ──────────────────────────────────────────────
    //  Config Loading / Saving
    // ──────────────────────────────────────────────

    /// <summary>
    /// Load configuration: SyncService (env vars) → settings.json overrides → Credential Manager for key.
    /// SyncService is the primary source because real configs use ANTHROPIC_BASE_URL / ANTHROPIC_AUTH_TOKEN env vars.
    /// </summary>
    [RelayCommand]
    public void LoadConfig()
    {
        // ── 1. PRIMARY: SyncService (reads env vars from settings.json + settings.local.json) ──
        var synced = _syncService.SyncAll();
        if (synced.DidSync)
        {
            SelectedProvider = synced.Provider;
            if (!string.IsNullOrEmpty(synced.BaseURL))
                BaseURL = synced.BaseURL;
            if (synced.EnabledModels.Count > 0)
            {
                // If current EnabledModels is empty, populate from synced; otherwise merge
                if (EnabledModels.Count == 0)
                    EnabledModels = new HashSet<string>(synced.EnabledModels);
                else
                    EnabledModels.UnionWith(synced.EnabledModels);
            }
            if (!string.IsNullOrEmpty(synced.ApiKey))
                ApiKey = synced.ApiKey;
        }
        AvailableModels = SelectedProvider.DefaultModels().ToList();

        // ── 2. OVERRIDE: direct settings.json keys (flat format), if present ──
        try
        {
            var dict = _configFileService.ReadJSON(_configFileService.SettingsPath);
            if (dict != null)
            {
                if (dict.TryGetValue("provider", out var providerElement))
                {
                    var providerStr = providerElement.GetString() ?? "";
                    SelectedProvider = providerStr.ToLowerInvariant() switch
                    {
                        "anthropic" => APIProvider.Anthropic,
                        "openai" => APIProvider.OpenAI,
                        "deepseek" => APIProvider.DeepSeek,
                        "custom" => APIProvider.Custom,
                        _ => SelectedProvider
                    };
                }

                if (dict.TryGetValue("baseURL", out var baseElement))
                    BaseURL = baseElement.GetString() ?? BaseURL;

                if (dict.TryGetValue("maxTokens", out var mtElement) && mtElement.TryGetInt32(out var mt))
                    MaxTokens = mt;

                if (dict.TryGetValue("timeout", out var toElement) && toElement.TryGetInt32(out var to))
                    Timeout = to;

                if (dict.TryGetValue("enabledModels", out var modelsElement) &&
                    modelsElement.ValueKind == JsonValueKind.Array)
                {
                    var models = new HashSet<string>();
                    foreach (var m in modelsElement.EnumerateArray())
                    {
                        var s = m.GetString();
                        if (!string.IsNullOrEmpty(s))
                            models.Add(s);
                    }
                    if (models.Count > 0)
                        EnabledModels = models;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APIConfigViewModel] LoadConfig settings.json parse failed: {ex.Message}");
        }

        // ── 3. Read API key from CredentialService (secure storage) ──
        if (_credentialService.TryRead(SelectedProvider.CredentialKey(), out var key))
        {
            if (!string.IsNullOrEmpty(key))
            {
                ApiKey = key;
            }
        }

        // Persist SyncService API key to Credential Manager (one-time migration)
        if (string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(synced.ApiKey))
        {
            ApiKey = synced.ApiKey;
            try { _credentialService.Save(SelectedProvider.CredentialKey(), synced.ApiKey); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIConfigViewModel] LoadConfig credential save migration failed: {ex.Message}");
            }
        }

        IsKeySaved = !string.IsNullOrEmpty(ApiKey);
    }

    /// <summary>
    /// Persist the current configuration to Windows Credential Manager
    /// and settings.json.
    /// </summary>
    [RelayCommand]
    public async Task SaveConfigAsync()
    {
        try
        {
            // ── 1. Save / delete API key in Credential Manager ──
            if (!string.IsNullOrEmpty(ApiKey))
            {
                _credentialService.Save(SelectedProvider.CredentialKey(), ApiKey);
            }
            else
            {
                try
                {
                    _credentialService.Delete(SelectedProvider.CredentialKey());
                }
                catch
                {
                    // Key may not exist — that's fine
                }
            }

            // ── 2. Save key to Credential Manager ──
            IsKeySaved = !string.IsNullOrEmpty(ApiKey);

            // ── 3. Write env vars to settings.json (preserve existing keys) ──
            var settingsPath = _configFileService.SettingsPath;
            var dir = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Read existing file first to preserve all other keys
            Dictionary<string, JsonElement> rootDict;
            try { rootDict = _configFileService.ReadJSON(settingsPath) ?? new(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APIConfigViewModel] SaveConfigAsync ReadJSON failed: {ex.Message}");
                rootDict = new();
            }

            // Merge/update the "env" object
            Dictionary<string, JsonElement> envDict = new();
            if (rootDict.TryGetValue("env", out var existingEnv) &&
                existingEnv.ValueKind == JsonValueKind.Object)
            {
                foreach (var kvp in existingEnv.EnumerateObject())
                    envDict[kvp.Name] = kvp.Value.Clone();
            }

            // Update env vars from current config
            void SetEnv(string key, string value)
            {
                if (!string.IsNullOrEmpty(value))
                    envDict[key] = JsonSerializer.SerializeToElement(value);
                else
                    envDict.Remove(key);
            }

            SetEnv("ANTHROPIC_BASE_URL", BaseURL);
            SetEnv("ANTHROPIC_AUTH_TOKEN", ApiKey);
            SetEnv("ANTHROPIC_MODEL", EnabledModels.FirstOrDefault() ?? "");
            SetEnv("ANTHROPIC_DEFAULT_OPUS_MODEL", "");
            SetEnv("ANTHROPIC_DEFAULT_SONNET_MODEL", "");
            SetEnv("ANTHROPIC_DEFAULT_HAIKU_MODEL", "");
            if (EnabledModels.Count > 1)
            {
                var ops = EnabledModels.Where(m => m.ToLowerInvariant().Contains("opus")).FirstOrDefault();
                var son = EnabledModels.Where(m => m.ToLowerInvariant().Contains("sonnet")).FirstOrDefault();
                var hai = EnabledModels.Where(m => m.ToLowerInvariant().Contains("haiku")).FirstOrDefault();
                if (!string.IsNullOrEmpty(ops)) SetEnv("ANTHROPIC_DEFAULT_OPUS_MODEL", ops);
                if (!string.IsNullOrEmpty(son)) SetEnv("ANTHROPIC_DEFAULT_SONNET_MODEL", son);
                if (!string.IsNullOrEmpty(hai)) SetEnv("ANTHROPIC_DEFAULT_HAIKU_MODEL", hai);
            }

            rootDict["env"] = JsonSerializer.SerializeToElement(envDict);

            // Also save flat keys for panel's own reading
            if (!string.IsNullOrEmpty(BaseURL))
                rootDict["baseURL"] = JsonSerializer.SerializeToElement(BaseURL);
            rootDict["provider"] = JsonSerializer.SerializeToElement(SelectedProvider.ToString().ToLowerInvariant());
            if (EnabledModels.Count > 0)
                rootDict["enabledModels"] = JsonSerializer.SerializeToElement(
                    EnabledModels.ToList());
            rootDict["maxTokens"] = JsonSerializer.SerializeToElement(MaxTokens);
            rootDict["timeout"] = JsonSerializer.SerializeToElement(Timeout);

            // Write preserving existing keys
            _configFileService.WriteJSON(rootDict, settingsPath);

            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    // ──────────────────────────────────────────────
    //  Connection Testing
    // ──────────────────────────────────────────────

    /// <summary>
    /// Test the API connection using the configured provider, base URL, and API key.
    /// For Anthropic: POST /v1/messages with a minimal message body.
    /// For OpenAI / DeepSeek / Custom: GET /v1/models on the normalized base URL.
    /// </summary>
    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsTestingConnection = true;
        ConnectionStatus = ConnectionStatus.Testing;
        ConnectionStatusMessage = null;

        var urlString = RawBaseURL;
        string testURL;

        switch (SelectedProvider)
        {
            case APIProvider.Anthropic:
                // Use the base URL as-is (DeepSeek's /anthropic IS the Anthropic-compatible
                // messages endpoint). Send a POST with a minimal messages payload.
                testURL = urlString + "/v1/messages";
                break;

            case APIProvider.OpenAI:
            case APIProvider.DeepSeek:
            case APIProvider.Custom:
            default:
                // For non-Anthropic providers, use the base URL directly with /v1/models.
                // Strip /anthropic if the user set ANTHROPIC_BASE_URL to DeepSeek's
                // anthropic endpoint.
                testURL = BaseURLForModels + "/v1/models";
                break;
        }

        if (!Uri.TryCreate(testURL, UriKind.Absolute, out var uri))
        {
            ConnectionStatus = ConnectionStatus.Failed;
            ConnectionStatusMessage = "无效的 URL";
            IsTestingConnection = false;
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            using var request = new HttpRequestMessage();

            if (SelectedProvider == APIProvider.Anthropic)
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = uri;
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

                var body = new
                {
                    model = "claude-sonnet-4-6",
                    max_tokens = 1,
                    messages = new[]
                    {
                        new { role = "user", content = "hi" }
                    }
                };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");
            }
            else
            {
                request.Method = HttpMethod.Get;
                request.RequestUri = uri;
            }

            if (!string.IsNullOrEmpty(ApiKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ApiKey}");
            }

            using var response = await _httpClient.SendAsync(request, cts.Token);
            var statusCode = (int)response.StatusCode;

            if (statusCode == 401 || statusCode == 403)
            {
                ConnectionStatus = ConnectionStatus.Failed;
                ConnectionStatusMessage = "认证失败 — 请检查 API Key";
            }
            else if (statusCode == 429)
            {
                ConnectionStatus = ConnectionStatus.Failed;
                ConnectionStatusMessage = "请求过于频繁 — 请稍后重试";
            }
            else if (statusCode >= 200 && statusCode <= 299)
            {
                ConnectionStatus = ConnectionStatus.Success;
            }
            else
            {
                ConnectionStatus = ConnectionStatus.Failed;
                ConnectionStatusMessage = $"HTTP {statusCode}";
            }
        }
        catch (TaskCanceledException)
        {
            ConnectionStatus = ConnectionStatus.Failed;
            ConnectionStatusMessage = "连接超时 — 请检查网络和 Base URL";
        }
        catch (HttpRequestException ex)
        {
            ConnectionStatus = ConnectionStatus.Failed;
            ConnectionStatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ConnectionStatus = ConnectionStatus.Failed;
            ConnectionStatusMessage = ex.Message;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    // ──────────────────────────────────────────────
    //  Provider Switching
    // ──────────────────────────────────────────────

    /// <summary>
    /// Reset configuration when the user switches API provider.
    /// Resets models, base URL, and attempts to load the new provider's saved key.
    /// </summary>
    [RelayCommand]
    public void ProviderChanged()
    {
        BaseURL = SelectedProvider.DefaultBaseURL();
        AvailableModels = SelectedProvider.DefaultModels().ToList();
        EnabledModels = new HashSet<string>();
        DetectedModels = new List<string>();
        DetectionMessage = null;
        ApiKey = "";
        IsKeySaved = false;
        ConnectionStatus = ConnectionStatus.Unknown;
        ConnectionStatusMessage = null;

        // Try to load the new provider's saved key from Credential Manager
        if (_credentialService.TryRead(SelectedProvider.CredentialKey(), out var key))
        {
            ApiKey = key;
            IsKeySaved = !string.IsNullOrEmpty(key);
        }
    }

    // ──────────────────────────────────────────────
    //  Model Detection
    // ──────────────────────────────────────────────

    /// <summary>
    /// Detect available models from the provider's API.
    /// Detected models are NOT automatically enabled — the user must select them.
    /// </summary>
    [RelayCommand]
    public async Task DetectModelsAsync()
    {
        IsDetectingModels = true;
        DetectionMessage = null;
        DetectedModels = new List<string>();

        // Use the normalized base URL that strips /anthropic and /v1 suffixes
        // so we don't double up on API version paths.
        var urlString = BaseURLForModels;

        switch (SelectedProvider)
        {
            case APIProvider.Anthropic:
                // Anthropic doesn't expose a public /v1/models endpoint.
                // Use a well-known curated list based on the current model catalog.
                await DetectAnthropicModelsAsync(urlString);
                break;

            case APIProvider.OpenAI:
            case APIProvider.DeepSeek:
            case APIProvider.Custom:
            default:
                await DetectOpenAICompatibleModelsAsync(urlString);
                break;
        }

        IsDetectingModels = false;

        if (DetectedModels.Count == 0)
        {
            DetectionMessage = "未检测到模型 — 请确认 Base URL 和 API Key 正确";
        }
        else
        {
            DetectionMessage = $"检测到 {DetectedModels.Count} 个模型 — 点击 + 添加";
        }
    }

    /// <summary>
    /// Anthropic: show the known model catalog and optionally verify against
    /// any proxy-backed /v1/models endpoint.
    /// </summary>
    private async Task DetectAnthropicModelsAsync(string baseURL)
    {
        // If no API key, show the known list for manual selection.
        if (string.IsNullOrEmpty(ApiKey))
        {
            DetectedModels = KnownAnthropicModels;
            return;
        }

        // Try a lightweight validation call — some proxies expose a models endpoint.
        var modelsURL = baseURL + "/v1/models";
        if (!Uri.TryCreate(modelsURL, UriKind.Absolute, out var uri))
        {
            DetectedModels = KnownAnthropicModels;
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ApiKey}");
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.ValueKind == JsonValueKind.Array)
                {
                    var apiModels = new List<string>();
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var idElement) &&
                            idElement.GetString() is string id && !string.IsNullOrEmpty(id))
                        {
                            apiModels.Add(id);
                        }
                    }

                    if (apiModels.Count > 0)
                    {
                        DetectedModels = apiModels;
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APIConfigViewModel] DetectAnthropicModelsAsync API call failed: {ex.Message}");
        }

        // Fallback: show the known curated list.
        DetectedModels = KnownAnthropicModels;
    }

    /// <summary>
    /// OpenAI-compatible /v1/models endpoint (used by OpenAI, DeepSeek, and custom providers).
    /// Parses {"data": [{"id": "..."}]} and filters out non-chat models.
    /// </summary>
    private async Task DetectOpenAICompatibleModelsAsync(string baseURL)
    {
        var testURL = baseURL + "/v1/models";

        if (!Uri.TryCreate(testURL, UriKind.Absolute, out var uri))
        {
            DetectionMessage = $"无效的检测 URL: {baseURL}/v1/models";
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (!string.IsNullOrEmpty(ApiKey))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ApiKey}");
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                DetectionMessage = $"模型检测失败: HTTP {(int)response.StatusCode} — 尝试 {uri}";
                return;
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var rawModels = new List<string>();
            foreach (var item in dataElement.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idElement) &&
                    idElement.GetString() is string id && !string.IsNullOrEmpty(id))
                {
                    rawModels.Add(id);
                }
            }

            // Filter out non-chat models for better UX
            var excludedKeywords = new[] { "embedding", "moderation", "tts-", "whisper-", "dall-e" };
            DetectedModels = rawModels
                .Where(model =>
                {
                    var lower = model.ToLowerInvariant();
                    return !excludedKeywords.Any(kw => lower.Contains(kw));
                })
                .ToList();
        }
        catch (TaskCanceledException)
        {
            DetectionMessage = "检测请求超时 — 请检查网络";
        }
        catch (HttpRequestException ex)
        {
            DetectionMessage = $"无法连接 — {ex.Message}";
        }
        catch (JsonException)
        {
            DetectionMessage = "检测响应格式无效";
        }
        catch (Exception ex)
        {
            DetectionMessage = $"检测请求失败: {ex.Message}";
        }
    }

    // ──────────────────────────────────────────────
    //  Model selection helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Toggle a detected model into / out of the enabled set.
    /// Raises <see cref="ObservableObject.OnPropertyChanged"/> for
    /// <see cref="EnabledModels"/> so the UI re-evaluates the collection.
    /// </summary>
    [RelayCommand]
    public void ToggleDetectedModel(string model)
    {
        if (string.IsNullOrEmpty(model))
            return;

        if (EnabledModels.Contains(model))
        {
            EnabledModels.Remove(model);
        }
        else
        {
            EnabledModels.Add(model);
        }

        // HashSet mutation does not change the reference, so the
        // [ObservableProperty] setter is not called.  Notify manually.
        OnPropertyChanged(nameof(EnabledModels));
    }

    /// <summary>
    /// Add all detected models to the enabled set.
    /// </summary>
    [RelayCommand]
    public void EnableAllDetectedModels()
    {
        EnabledModels.UnionWith(DetectedModels);
        OnPropertyChanged(nameof(EnabledModels));
    }

    /// <summary>
    /// Clear all enabled models (but keep the detected list intact).
    /// Assigning a new instance triggers the [ObservableProperty]
    /// setter, so no manual notification is needed.
    /// </summary>
    [RelayCommand]
    public void ClearEnabledModels()
    {
        EnabledModels = new HashSet<string>();
    }
}
