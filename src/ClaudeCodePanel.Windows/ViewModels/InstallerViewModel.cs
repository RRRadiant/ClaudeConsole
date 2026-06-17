using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.ViewModels;

public partial class InstallerViewModel : ObservableObject
{
    private readonly InstallerService _installer = InstallerService.Instance;

    [ObservableProperty]
    private InstallerService.CliStatus _claudeStatus = new() { Installed = false };

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isErrorMessage;

    [ObservableProperty]
    private bool _showStatusMessage;

    public InstallerViewModel()
    {
        _ = RefreshStatusAsync().ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
                Debug.WriteLine($"[InstallerViewModel] RefreshStatusAsync failed: {t.Exception.GetBaseException().Message}");
        }, TaskScheduler.Default);
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        try
        {
            ClaudeStatus = await Task.Run(() => _installer.GetClaudeStatus());
        }
        catch { }
    }

    [RelayCommand]
    private async Task InstallAsync(string methodStr)
    {
        if (!Enum.TryParse<InstallerService.InstallMethod>(methodStr, out var method)) return;

        IsInstalling = true;
        ShowStatusMessage = false;
        StatusMessage = "正在安装...";
        IsErrorMessage = false;
        ShowStatusMessage = true;

        try
        {
            var result = await _installer.InstallCliAsync(method);

            if (result.Success)
            {
                StatusMessage = "安装成功";
                IsErrorMessage = false;
            }
            else
            {
                var err = result.Error ?? string.Empty;
                StatusMessage = err.Contains("winget") || err.Contains("npm")
                    ? $"系统缺少 {method}。请前往「环境检测」安装 Node.js"
                    : $"安装失败: {Truncate(err, 300)}";
                IsErrorMessage = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"安装异常: {ex.Message}";
            IsErrorMessage = true;
        }

        ShowStatusMessage = true;
        IsInstalling = false;
        await RefreshStatusAsync();
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        IsInstalling = true;
        ShowStatusMessage = false;
        StatusMessage = "卸载中...";
        IsErrorMessage = false;
        ShowStatusMessage = true;

        try
        {
            var result = await _installer.UninstallCliAsync();

            StatusMessage = result.Success ? "已卸载" : $"卸载失败: {result.Error}";
            IsErrorMessage = !result.Success;
        }
        catch (Exception ex)
        {
            StatusMessage = $"卸载异常: {ex.Message}";
            IsErrorMessage = true;
        }

        ShowStatusMessage = true;
        IsInstalling = false;
        await RefreshStatusAsync();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
