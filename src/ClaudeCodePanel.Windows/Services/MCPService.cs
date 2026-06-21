using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Services;

public sealed class MCPService
{
    public static MCPService Instance { get; } = new();

    private readonly HttpClient _httpClient;

    private MCPService()
    {
        _httpClient = new HttpClient();
    }

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

    private async Task<MCPConnectionResult> TestSSEConnectionAsync(MCPServerConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
            return MCPConnectionResult.Failure("无效的 URL");

        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var uri))
            return MCPConnectionResult.Failure("无效的 URL");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            using var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
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

    private async Task<MCPConnectionResult> TestStdioConnectionAsync(MCPServerConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Command))
            return MCPConnectionResult.Failure("命令不能为空");

        // Build a simple test: run <command> --help and check exit code
        // If the command is a Python script, skip --help (might hang)
        var testArgs = new List<string> { config.Command };

        bool isPython = config.Command.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
                        || config.Args.Any(a => a.EndsWith(".py", StringComparison.OrdinalIgnoreCase));

        if (!isPython)
            testArgs.Add("--help");

        // Use cmd.exe on Windows, /usr/bin/env on Unix
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

        // On Windows, prepend /c to run the command through cmd.exe
        if (shell == "cmd.exe")
            process.StartInfo.ArgumentList.Add("/c");

        foreach (var arg in testArgs)
            process.StartInfo.ArgumentList.Add(arg);

        // Merge environment variables
        foreach (var kvp in config.Env)
            process.StartInfo.Environment[kvp.Key] = kvp.Value;

        var tcs = new TaskCompletionSource<MCPConnectionResult>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        // Capture stderr from async drain instead of sync ReadToEnd() in Exited handler
        string? stderrOutput = null;

        // Register timeout — dispose the registration to prevent resource leak
        using var ctr = cts.Token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    tcs.TrySetResult(MCPConnectionResult.Success("命令可执行 (超时终止)"));
                }
            }
            catch
            {
                // Process may have already exited
            }
        });

        process.Exited += (_, _) =>
        {
            if (tcs.Task.IsCompleted)
                return;

            try
            {
                if (process.ExitCode == 0 || process.ExitCode == 1)
                {
                    // Exit 0 or 1 from --help both mean the binary was found and ran
                    tcs.TrySetResult(MCPConnectionResult.Success("命令可执行"));
                }
                else
                {
                    var errMsg = (stderrOutput ?? "").Trim();
                    if (string.IsNullOrEmpty(errMsg))
                        errMsg = $"退出码 {process.ExitCode}";
                    tcs.TrySetResult(MCPConnectionResult.Failure(errMsg));
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetResult(MCPConnectionResult.Failure(ex.Message));
            }
            finally
            {
                process.Dispose();
            }
        };

        try
        {
            process.EnableRaisingEvents = true;

            // Guard: if process already exited before we registered the handler,
            // handle it synchronously to prevent event-handler leak
            if (process.HasExited)
            {
                if (process.ExitCode == 0 || process.ExitCode == 1)
                    return MCPConnectionResult.Success("命令可执行");
                return MCPConnectionResult.Failure($"退出码 {process.ExitCode}");
            }

            process.Start();

            // Begin reading stdout/stderr asynchronously to avoid deadlocks
            // Capture stderr into our field so the Exited handler can read it
            _ = process.StandardOutput.ReadToEndAsync();
            _ = Task.Run(async () =>
            {
                stderrOutput = await process.StandardError.ReadToEndAsync();
            });

            return await tcs.Task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            process.Dispose();
            return MCPConnectionResult.Failure(ex.Message);
        }
    }
}
