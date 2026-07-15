using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// CLI installer and status detection service.
/// Ported from Claude-Win src/main/ipc/installer.ts.
/// </summary>
public sealed class InstallerService : IInstallerService
{
    public static InstallerService Instance { get; } = new();

    private readonly Func<string, string, int, Task<ProcessResult>> _runProcess;
    private readonly Func<Task<CliStatus>>? _statusProbe;

    private InstallerService()
        : this(ProcessRunner.RunAsync, statusProbe: null)
    {
    }

    internal InstallerService(
        Func<string, string, int, Task<ProcessResult>> runProcess,
        Func<Task<CliStatus>>? statusProbe)
    {
        _runProcess = runProcess;
        _statusProbe = statusProbe;
    }

    // ── Public types ───────────────────────────────────────

    public enum InstallMethod { Npm, Winget }

    public sealed class CliStatus
    {
        public bool Installed { get; init; }
        public string? Version { get; init; }
        public string? Path { get; init; }
    }

    public sealed class InstallResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
    }

    // ── NPM detection ──────────────────────────────────────

    /// <summary>
    /// Find npm's full path — PATH may not be refreshed after install.
    /// Searches 15+ known locations and falls back to PowerShell discovery.
    /// </summary>
    private async Task<string> FindNpmAsync()
    {
        var homedir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var paths = new[]
        {
            Path.Combine(homedir, ".npm-global", "bin", "npm.cmd"),
            Path.Combine(homedir, ".npm-global", "bin", "npm"),
            Path.Combine(homedir, ".npm-global", "bin", "npm.exe"),
            Path.Combine(appData, "npm", "npm.cmd"),
            Path.Combine(appData, "npm", "npm.exe"),
            @"C:\Program Files\nodejs\npm.cmd",
            @"C:\Program Files\nodejs\npm.exe",
            @"C:\Program Files (x86)\nodejs\npm.cmd",
            @"C:\Program Files (x86)\nodejs\npm.exe",
            Path.Combine(programFiles, "nodejs", "npm.cmd"),
            Path.Combine(programFiles, "nodejs", "npm.exe"),
            Path.Combine(localAppData, "Programs", "nodejs", "npm.cmd"),
            Path.Combine(appData, "npm", "npm.cmd"),
            @"C:\nodejs\npm.cmd",
        };

        foreach (var p in paths)
        {
            if (File.Exists(p)) return p;
        }

        // PowerShell fallback — async process runner
        var result = await RunQuickProcessAsync("powershell",
            "-NoProfile -Command \"(Get-Command npm.cmd -ErrorAction SilentlyContinue).Source\"");
        if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
            return result;

        result = await RunQuickProcessAsync("powershell",
            "-NoProfile -Command \"(Get-Command npm -ErrorAction SilentlyContinue).Source\"");
        if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
            return result;

        return "npm"; // Last resort: hope it's in PATH
    }

    /// <summary>
    /// Runs a quick process, drains stdout asynchronously, and returns trimmed output.
    /// Uses cmd.exe /c wrapper for .cmd/.bat files for consistency with RunCommandAsync.
    /// </summary>
    private async Task<string?> RunQuickProcessAsync(string fileName, string arguments, int timeoutMs = 5000)
    {
        var result = await _runProcess(fileName, arguments, timeoutMs).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode != 0)
            return null;
        return string.IsNullOrWhiteSpace(result.Stdout) ? null : result.Stdout;
    }

    // ── Process runner ─────────────────────────────────────

    private async Task<InstallResult> RunCommandAsync(string fileName, string arguments, int timeoutMs = 180_000)
    {
        var result = await _runProcess(fileName, arguments, timeoutMs).ConfigureAwait(false);

        if (result.TimedOut)
            return new InstallResult { Success = false, Error = "超时" };

        if (result.ExitCode == 0)
            return new InstallResult { Success = true };

        return new InstallResult
        {
            Success = false,
            Error = !string.IsNullOrWhiteSpace(result.Stderr)
                ? result.Stderr.Trim()
                : (!string.IsNullOrWhiteSpace(result.Stdout) ? result.Stdout.Trim() : $"退出码 {result.ExitCode}")
        };
    }

    // ── Claude Code CLI Install ────────────────────────────

    private const string ClaudePkg = "@anthropic-ai/claude-code";
    private const string OfficialNpmRegistry = "https://registry.npmjs.org";

    public async Task<InstallResult> InstallCliAsync(InstallMethod method)
    {
        var statusBeforeInstall = await GetClaudeStatusAsync().ConfigureAwait(false);

        if (method == InstallMethod.Npm)
        {
            var npm = await FindNpmAsync().ConfigureAwait(false);
            var preflight = await RunCommandAsync(npm, "--version", 10_000).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return new InstallResult
                {
                    Success = false,
                    Error = $"未检测到可用的 npm: {preflight.Error}"
                };
            }

            var installResult = await RunCommandAsync(
                npm,
                $"install -g {ClaudePkg} --registry={OfficialNpmRegistry}",
                AppConstants.TimeoutInstall).ConfigureAwait(false);

            return await VerifyOrRecoverInstallAsync(
                method,
                npm,
                statusBeforeInstall,
                installResult).ConfigureAwait(false);
        }
        if (method == InstallMethod.Winget)
        {
            var preflight = await RunCommandAsync("winget", "--version", 10_000).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return new InstallResult
                {
                    Success = false,
                    Error = $"未检测到可用的 winget: {preflight.Error}"
                };
            }

            var installResult = await RunCommandAsync(
                "winget",
                "install --id Anthropic.ClaudeCode --exact --accept-package-agreements " +
                "--accept-source-agreements --silent",
                AppConstants.TimeoutInstall).ConfigureAwait(false);

            return await VerifyOrRecoverInstallAsync(
                method,
                npmPath: null,
                statusBeforeInstall,
                installResult).ConfigureAwait(false);
        }

        return new InstallResult { Success = false, Error = "未知安装方式" };
    }

    private async Task<InstallResult> VerifyOrRecoverInstallAsync(
        InstallMethod method,
        string? npmPath,
        CliStatus statusBeforeInstall,
        InstallResult installResult)
    {
        if (!installResult.Success)
        {
            if (!statusBeforeInstall.Installed)
                await CleanupPartialInstallAsync(method, npmPath).ConfigureAwait(false);

            return installResult;
        }

        var statusAfterInstall = await GetClaudeStatusAsync().ConfigureAwait(false);
        if (statusAfterInstall.Installed)
            return new InstallResult { Success = true };

        if (!statusBeforeInstall.Installed)
            await CleanupPartialInstallAsync(method, npmPath).ConfigureAwait(false);

        return new InstallResult
        {
            Success = false,
            Error = "安装命令已完成，但未检测到 Claude Code；已清理本次安装产生的残留。"
        };
    }

    private async Task CleanupPartialInstallAsync(InstallMethod method, string? npmPath)
    {
        if (method == InstallMethod.Npm && !string.IsNullOrWhiteSpace(npmPath))
        {
            await RunCommandAsync(
                npmPath,
                $"uninstall -g {ClaudePkg}",
                AppConstants.TimeoutUninstall).ConfigureAwait(false);
        }
        else if (method == InstallMethod.Winget)
        {
            await RunCommandAsync(
                "winget",
                "uninstall --id Anthropic.ClaudeCode --exact --silent",
                AppConstants.TimeoutUninstall).ConfigureAwait(false);
        }
    }

    // ── Claude Code CLI Uninstall ──────────────────────────

    public async Task<InstallResult> UninstallCliAsync()
    {
        var statusBeforeUninstall = await GetClaudeStatusAsync().ConfigureAwait(false);
        if (!statusBeforeUninstall.Installed)
            return new InstallResult { Success = true };

        InstallResult uninstallResult;
        if (IsWingetInstallation(statusBeforeUninstall.Path))
        {
            uninstallResult = await RunCommandAsync(
                "winget",
                "uninstall --id Anthropic.ClaudeCode --exact --silent",
                AppConstants.TimeoutUninstall).ConfigureAwait(false);
        }
        else
        {
            var npm = await FindNpmAsync().ConfigureAwait(false);
            uninstallResult = await RunCommandAsync(
                npm,
                $"uninstall -g {ClaudePkg}",
                AppConstants.TimeoutUninstall).ConfigureAwait(false);
        }

        if (!uninstallResult.Success)
            return uninstallResult;

        var statusAfterUninstall = await GetClaudeStatusAsync().ConfigureAwait(false);
        return statusAfterUninstall.Installed
            ? new InstallResult
            {
                Success = false,
                Error = $"卸载命令已完成，但仍检测到 Claude Code: {statusAfterUninstall.Path ?? "未知路径"}"
            }
            : new InstallResult { Success = true };
    }

    private static bool IsWingetInstallation(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               (path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("WinGet", StringComparison.OrdinalIgnoreCase));
    }

    // ── Claude Code CLI Status ─────────────────────────────

    /// <summary>
    /// Detect if Claude Code CLI is installed by searching PATH,
    /// known paths, npm global bin directory, and fallback shell.
    /// All process calls are async to avoid deadlocks.
    /// </summary>
    public Task<CliStatus> GetClaudeStatusAsync()
    {
        return _statusProbe?.Invoke() ?? GetClaudeStatusCoreAsync();
    }

    private async Task<CliStatus> GetClaudeStatusCoreAsync()
    {
        const string binaryName = "claude";
        var homedir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // 1. PATH search via 'where'
        var foundPaths = new List<string>();
        var whereOutput = await RunQuickProcessAsync("cmd.exe", $"/c where {binaryName} 2>nul");
        if (!string.IsNullOrWhiteSpace(whereOutput))
        {
            foundPaths.AddRange(whereOutput.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
        }

        // 2. Known installation paths
        var npmBin = await RunQuickProcessAsync("npm", "bin -g");

        var bases = new List<string>
        {
            Path.Combine(homedir, ".npm-global", "bin"),
            Path.Combine(homedir, ".local", "bin"),
            Path.Combine(appData, "npm"),
            Environment.GetEnvironmentVariable("APPDATA") is { } ad ? Path.Combine(ad, "npm") : "",
        };
        if (!string.IsNullOrWhiteSpace(npmBin))
            bases.Add(npmBin);

        foreach (var baseDir in bases)
        {
            if (string.IsNullOrWhiteSpace(baseDir)) continue;
            foreach (var ext in new[] { ".cmd", ".exe", "" })
            {
                foundPaths.Add(Path.Combine(baseDir, $"{binaryName}{ext}"));
            }
        }

        // 3. Check each path
        foreach (var candidatePath in foundPaths)
        {
            if (string.IsNullOrWhiteSpace(candidatePath)) continue;
            if (!File.Exists(candidatePath)) continue;

            var version = await RunQuickProcessAsync(candidatePath, "--version", 10_000);
            if (!string.IsNullOrWhiteSpace(version))
                return new CliStatus { Installed = true, Version = version, Path = candidatePath };
        }

        // 4. Last resort: try running bare command
        var bareVersion = await RunQuickProcessAsync("cmd.exe", $"/c {binaryName} --version", 10_000);
        if (!string.IsNullOrWhiteSpace(bareVersion))
            return new CliStatus { Installed = true, Version = bareVersion, Path = binaryName };

        return new CliStatus { Installed = false, Version = null, Path = null };
    }

}
