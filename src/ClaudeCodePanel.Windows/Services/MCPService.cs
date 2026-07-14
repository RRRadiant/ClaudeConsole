using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Services;

public sealed class MCPService : IMCPService
{
    public static MCPService Instance { get; } = new();

    private MCPService() { }

    // ── Test Connection ─────────────────────────────────────

    /// <summary>
    /// Test whether an MCP server is reachable. Does NOT start a persistent process.
    /// </summary>
    /// <remarks>
    /// - SSE servers: sends an HTTP GET and checks the response.
    /// - STDIO servers: launches the command with --help to verify it runs.
    /// - Builtin/Plugin servers: returns success immediately (managed by Claude Code).
    /// </remarks>
    public async Task<MCPConnectionResult> TestConnectionAsync(MCPServerConfig config)
    {
        return config.ServerType switch
        {
            MCPServerType.Sse => await TestSSEConnectionAsync(config).ConfigureAwait(false),
            MCPServerType.Stdio => await TestStdioConnectionAsync(config).ConfigureAwait(false),
            MCPServerType.Builtin or MCPServerType.Plugin =>
                // Builtin/plugin servers are managed by Claude Code — assume reachable
                MCPConnectionResult.Success("由 Claude Code 管理"),
            _ => MCPConnectionResult.Failure("未知的服务器类型")
        };
    }

    private static async Task<MCPConnectionResult> TestSSEConnectionAsync(MCPServerConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
            return MCPConnectionResult.Failure("无效的 URL");

        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var uri))
            return MCPConnectionResult.Failure("无效的 URL");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            using var response = await HttpClientFactory.Create().SendAsync(request, cts.Token).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;

            // SSE endpoints often return 4xx for GET (no session), but the server IS reachable
            if (statusCode is >= 200 and <= 499)
                return MCPConnectionResult.Success($"服务器可达 (HTTP {statusCode})");

            return MCPConnectionResult.Success("服务器可达");
        }
        catch (OperationCanceledException)
        {
            return MCPConnectionResult.Failure("连接超时 (8s)");
        }
        catch (Exception ex)
        {
            return MCPConnectionResult.Failure(ex.Message);
        }
    }

    private static async Task<MCPConnectionResult> TestStdioConnectionAsync(MCPServerConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Command))
            return MCPConnectionResult.Failure("命令不能为空");

        var testArgs = new List<string> { config.Command };

        bool isPython = config.Command.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
                        || config.Args.Any(a => a.EndsWith(".py", StringComparison.OrdinalIgnoreCase));

        if (config.Args.Count > 0)
            testArgs.AddRange(config.Args);
        else if (!isPython)
            testArgs.Add("--help");

        string shell = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? "cmd.exe"
            : "/usr/bin/env";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (shell == "cmd.exe")
            process.StartInfo.ArgumentList.Add("/c");

        foreach (var arg in testArgs)
            process.StartInfo.ArgumentList.Add(arg);

        foreach (var kvp in config.Env)
            process.StartInfo.Environment[kvp.Key] = kvp.Value;

        try
        {
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var exitTask = process.WaitForExitAsync();
            var delayTask = Task.Delay(TimeSpan.FromSeconds(8));

            var completed = await Task.WhenAny(exitTask, delayTask).ConfigureAwait(false);
            if (completed == delayTask)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { SharedHelpers.SafeLog("MCPService.TestStdioConnectionAsync.Kill", ex); }
                return MCPConnectionResult.Success("命令可执行 (超时终止)");
            }

            await exitTask.ConfigureAwait(false);
            var stderrOutput = (await stderrTask.ConfigureAwait(false)).Trim();

            if (process.ExitCode == 0 || process.ExitCode == 1)
                return MCPConnectionResult.Success("命令可执行");

            var errMsg = stderrOutput;
            if (string.IsNullOrEmpty(errMsg))
                errMsg = $"退出码 {process.ExitCode}";
            return MCPConnectionResult.Failure(errMsg);
        }
        catch (Exception ex)
        {
            return MCPConnectionResult.Failure(ex.Message);
        }
        finally
        {
            process.Dispose();
        }
    }
}
