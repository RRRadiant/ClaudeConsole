using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Task utility extensions for fire-and-forget patterns and timeout support.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Fire-and-forget a task safely — exceptions are logged to Debug.WriteLine
    /// and optionally reported via <paramref name="onException"/>.
    /// </summary>
    public static async void SafeFireAndForget(
        this Task task,
        string context = "",
        Action<Exception>? onException = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{context}] Fire-and-forget failed: {ex.Message}");
            onException?.Invoke(ex);
        }
    }

    /// <summary>
    /// Adds a timeout to a task. Throws <see cref="TimeoutException"/> if the
    /// task does not complete within <paramref name="timeoutMs"/> milliseconds.
    /// </summary>
    public static async Task<T> WithTimeout<T>(
        this Task<T> task, int timeoutMs, string operationName = "")
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token)).ConfigureAwait(false);
        if (completed != task)
            throw new TimeoutException($"[{operationName}] timed out after {timeoutMs}ms");
        cts.Cancel(); // cancel the delay timer
        return await task.ConfigureAwait(false);
    }

    /// <summary>
    /// Non-generic overload of <see cref="WithTimeout{T}"/>.
    /// </summary>
    public static async Task WithTimeout(
        this Task task, int timeoutMs, string operationName = "")
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token)).ConfigureAwait(false);
        if (completed != task)
            throw new TimeoutException($"[{operationName}] timed out after {timeoutMs}ms");
        cts.Cancel();
        await task.ConfigureAwait(false);
    }
}
