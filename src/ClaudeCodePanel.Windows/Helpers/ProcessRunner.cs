using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Result of running a process asynchronously.
/// </summary>
public readonly record struct ProcessResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);

/// <summary>
/// Unified process runner that handles .cmd/.bat wrapping, async stdout/stderr
/// draining, timeout via Task.WhenAny, and consistent exception handling.
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Runs a process asynchronously with a timeout.
    /// - .cmd/.bat files are automatically wrapped via cmd.exe /c
    /// - stdout and stderr are drained concurrently with the wait
    /// - Returns <see cref="ProcessResult"/> with TimedOut=true on timeout
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName, string arguments, int timeoutMs = 5000)
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

            using var process = Process.Start(psi);
            if (process == null)
                return new ProcessResult(-1, "", "Failed to start process", false);

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var exitTask = process.WaitForExitAsync();
            var delayTask = Task.Delay(timeoutMs);

            var completed = await Task.WhenAny(exitTask, delayTask).ConfigureAwait(false);
            if (completed == delayTask)
            {
                try { process.Kill(); } catch { /* process already exited */ }
                return new ProcessResult(-1, "", "", true);
            }

            await exitTask.ConfigureAwait(false);

            var stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();
            return new ProcessResult(process.ExitCode, stdout, stderr, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessRunner] RunAsync failed ({fileName} {arguments}): {ex.Message}");
            return new ProcessResult(-1, "", ex.Message, false);
        }
    }
}
