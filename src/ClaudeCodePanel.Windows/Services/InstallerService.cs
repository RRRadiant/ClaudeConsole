using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// CLI installer and status detection service.
/// Ported from Claude-Win src/main/ipc/installer.ts.
/// </summary>
public sealed class InstallerService
{
    public static InstallerService Instance { get; } = new();

    private InstallerService() { }

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
    private static string FindNpm()
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

        // PowerShell fallback
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"(Get-Command npm.cmd -ErrorAction SilentlyContinue).Source\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            var result = p?.StandardOutput.ReadToEnd().Trim();
            if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
                return result;
        }
        catch { }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"(Get-Command npm -ErrorAction SilentlyContinue).Source\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            var result = p?.StandardOutput.ReadToEnd().Trim();
            if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
                return result;
        }
        catch { }

        return "npm"; // Last resort: hope it's in PATH
    }

    // ── Process runner ─────────────────────────────────────

    private static async Task<InstallResult> RunCommandAsync(string command, int timeoutMs = 180_000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return new InstallResult { Success = false, Error = "无法启动进程" };

            // Read stdout/stderr asynchronously BEFORE waiting to avoid deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { }
                return new InstallResult { Success = false, Error = "超时" };
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0)
                return new InstallResult { Success = true };

            return new InstallResult
            {
                Success = false,
                Error = !string.IsNullOrWhiteSpace(stderr)
                    ? stderr.Trim()
                    : (!string.IsNullOrWhiteSpace(stdout) ? stdout.Trim() : $"退出码 {process.ExitCode}")
            };
        }
        catch (Exception ex)
        {
            return new InstallResult { Success = false, Error = ex.Message };
        }
    }

    // ── Claude Code CLI Install ────────────────────────────

    private const string ClaudePkg = "@anthropic-ai/claude-code";

    public async Task<InstallResult> InstallCliAsync(InstallMethod method)
    {
        if (method == InstallMethod.Npm)
        {
            var npm = FindNpm();
            return await RunCommandAsync($"\"{npm}\" install -g {ClaudePkg} --registry=https://registry.npmmirror.com");
        }
        if (method == InstallMethod.Winget)
            return await RunCommandAsync("winget install Anthropic.ClaudeCode");

        return new InstallResult { Success = false, Error = "未知安装方式" };
    }

    // ── Claude Code CLI Uninstall ──────────────────────────

    public async Task<InstallResult> UninstallCliAsync()
    {
        var npm = FindNpm();
        return await RunCommandAsync($"\"{npm}\" uninstall -g {ClaudePkg}", 60_000);
    }

    // ── Claude Code CLI Status ─────────────────────────────

    /// <summary>
    /// Detect if Claude Code CLI is installed by searching PATH,
    /// known paths, npm global bin directory, and fallback shell.
    /// </summary>
    public CliStatus GetClaudeStatus()
    {
        const string binaryName = "claude";
        var homedir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // 1. PATH search via 'where'
        var foundPaths = new List<string>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c where {binaryName} 2>nul",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            var output = p?.StandardOutput.ReadToEnd().Trim();
            if (!string.IsNullOrWhiteSpace(output))
            {
                foundPaths.AddRange(output.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
            }
        }
        catch { }

        // 2. Known installation paths
        string? npmBin = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "npm",
                Arguments = "bin -g",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            npmBin = p?.StandardOutput.ReadToEnd().Trim();
        }
        catch { }

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
            try
            {
                if (!File.Exists(candidatePath)) continue;

                var psi = new ProcessStartInfo
                {
                    FileName = candidatePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(10_000);
                var version = p?.StandardOutput.ReadToEnd().Trim();
                if (!string.IsNullOrWhiteSpace(version))
                    return new CliStatus { Installed = true, Version = version, Path = candidatePath };
            }
            catch { }
        }

        // 4. Last resort: try running bare command
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {binaryName} --version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(10_000);
            var version = p?.StandardOutput.ReadToEnd().Trim();
            if (!string.IsNullOrWhiteSpace(version))
                return new CliStatus { Installed = true, Version = version, Path = binaryName };
        }
        catch { }

        return new CliStatus { Installed = false, Version = null, Path = null };
    }

}
