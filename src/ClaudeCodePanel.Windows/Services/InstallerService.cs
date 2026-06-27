using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private static async Task<string> FindNpmAsync()
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
    private static async Task<string?> RunQuickProcessAsync(string fileName, string arguments, int timeoutMs = 5000)
    {
        try
        {
            string actualFileName;
            string actualArguments;

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".cmd" || ext == ".bat")
            {
                actualFileName = "cmd.exe";
                actualArguments = $"/c \"{fileName}\" {arguments}";
            }
            else
            {
                actualFileName = fileName;
                actualArguments = arguments;
            }

            var psi = new ProcessStartInfo
            {
                FileName = actualFileName,
                Arguments = actualArguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;

            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var exitTask = p.WaitForExitAsync();
            var delayTask = Task.Delay(timeoutMs);

            var completed = await Task.WhenAny(exitTask, delayTask);
            if (completed == delayTask)
            {
                try { p.Kill(); } catch { }
                return null;
            }

            await exitTask;
            var stdout = await stdoutTask;
            return stdout.Trim();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InstallerService] RunQuickProcessAsync failed: {ex.Message}");
            return null;
        }
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

            var exitTask = process.WaitForExitAsync();
            var delayTask = Task.Delay(timeoutMs);

            var completed = await Task.WhenAny(exitTask, delayTask);
            if (completed == delayTask)
            {
                try { process.Kill(); } catch { }
                return new InstallResult { Success = false, Error = "超时" };
            }

            await exitTask;
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
            var npm = await FindNpmAsync();
            return await RunCommandAsync($"\"{npm}\" install -g {ClaudePkg} --registry=https://registry.npmmirror.com");
        }
        if (method == InstallMethod.Winget)
            return await RunCommandAsync("winget install Anthropic.ClaudeCode");

        return new InstallResult { Success = false, Error = "未知安装方式" };
    }

    // ── Claude Code CLI Uninstall ──────────────────────────

    public async Task<InstallResult> UninstallCliAsync()
    {
        var npm = await FindNpmAsync();
        return await RunCommandAsync($"\"{npm}\" uninstall -g {ClaudePkg}", 60_000);
    }

    // ── Claude Code CLI Status ─────────────────────────────

    /// <summary>
    /// Detect if Claude Code CLI is installed by searching PATH,
    /// known paths, npm global bin directory, and fallback shell.
    /// All process calls are async to avoid deadlocks.
    /// </summary>
    public async Task<CliStatus> GetClaudeStatusAsync()
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
