using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.ViewModels;

/// <summary>
/// ViewModel for the environment detection panel.
/// Ported from Claude-Win EnvCheckPanel.tsx.
/// Displays Node.js, npm, and Git installation status with download buttons.
/// </summary>
public partial class EnvCheckViewModel : ObservableObject
{
    private readonly EnvironmentService _env;

    // ── Observable properties ──────────────────────────────

    [ObservableProperty]
    private ObservableCollection<DepItem> _deps = new();

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private int _readyCount;

    [ObservableProperty]
    private int _missingCount;

    [ObservableProperty]
    private bool _allOk;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _showStatusMessage;

    // ── Dep item model ─────────────────────────────────────

    public partial class DepItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _installed;

        [ObservableProperty]
        private string? _version;

        [ObservableProperty]
        private string? _downloadUrl;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private string _statusColor = "#59FFFFFF";

        public bool HasDownloadUrl => !string.IsNullOrWhiteSpace(DownloadUrl);
    }

    // ── Constructor ────────────────────────────────────────

    public EnvCheckViewModel(EnvironmentService? environmentService = null)
    {
        _env = environmentService ?? EnvironmentService.Instance;
        _ = CheckAsync().ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
                Debug.WriteLine($"[EnvCheckViewModel] CheckAsync failed: {t.Exception.GetBaseException().Message}");
        }, TaskScheduler.Default);
    }

    // ── Commands ───────────────────────────────────────────

    [RelayCommand]
    private async Task CheckAsync()
    {
        IsLoading = true;
        ShowStatusMessage = false;

        try
        {
            var results = await _env.CheckAllDepsAsync();

            Deps.Clear();
            foreach (var r in results)
            {
                Deps.Add(new DepItem
                {
                    Name = r.Name,
                    Description = r.Description,
                    Installed = r.Installed,
                    Version = r.Version,
                    DownloadUrl = r.DownloadUrl,
                    StatusText = r.Installed ? "已安装" : "未安装",
                    StatusColor = r.Installed ? "#4ea88d" : "#59FFFFFF"
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"检测失败: {ex.Message}";
            ShowStatusMessage = true;
        }

        UpdateSummary();
        IsLoading = false;
    }

    [RelayCommand]
    private void Download(string depType)
    {
        try
        {
            _env.OpenDownloadUrl(depType);
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开下载页面失败: {ex.Message}";
            ShowStatusMessage = true;
        }
    }

    // ── Helpers ────────────────────────────────────────────

    private void UpdateSummary()
    {
        ReadyCount = Deps.Count(d => d.Installed);
        MissingCount = Deps.Count(d => !d.Installed);
        AllOk = Deps.Count > 0 && Deps.All(d => d.Installed);
    }
}
