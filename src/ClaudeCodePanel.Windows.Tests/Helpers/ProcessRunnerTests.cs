using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Services;
using Xunit;

namespace ClaudeCodePanel.Windows.Tests.Helpers;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_EchoCommand_ReturnsStdout()
    {
        var result = await ProcessRunner.RunAsync("cmd.exe", "/c echo hello");

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Stdout);
    }

    [Fact]
    public async Task RunAsync_Timeout_ReturnsTimedOut()
    {
        // Use a timeout of 1ms — the process will not complete in time
        var result = await ProcessRunner.RunAsync("cmd.exe", "/c timeout /t 30 /nobreak", 1);

        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_NonExistentCommand_ReturnsNonZero()
    {
        var result = await ProcessRunner.RunAsync("nonexistent_command_xyz", "");

        Assert.False(result.TimedOut);
        Assert.NotEqual(0, result.ExitCode);
    }
}

public sealed class TaskExtensionsTests
{
    [Fact]
    public async Task WithTimeout_CompletesBeforeTimeout_ReturnsResult()
    {
        var task = Task.FromResult(42);
        var result = await task.WithTimeout(5000, "test");
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task WithTimeout_ExceedsTimeout_Throws()
    {
        var task = Task.Delay(30000); // 30 seconds
        await Assert.ThrowsAsync<TimeoutException>(
            () => task.WithTimeout(1, "test-timeout"));
    }

    [Fact]
    public void SafeFireAndForget_ExceptionInTask_DoesNotThrow()
    {
        // This should not throw — the exception is caught internally
        Task.Run(() => throw new InvalidOperationException("test"))
            .SafeFireAndForget("test-context");
    }
}

public sealed class ConfigFileServiceWriteJSONTests
{
    [Fact]
    public void WriteJSON_MtimeMatch_Succeeds()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var service = ConfigFileService.Instance;
            var dict = new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>();
            var value = System.Text.Json.JsonSerializer.SerializeToElement("test-value");
            dict["test-key"] = value;

            // First write should succeed
            service.WriteJSON(dict, tempFile);

            // Get the mtime after writing
            var mtime = System.IO.File.GetLastWriteTimeUtc(tempFile);

            // Second write with matching mtime should succeed
            dict["test-key-2"] = System.Text.Json.JsonSerializer.SerializeToElement("test-value-2");
            service.WriteJSON(dict, tempFile, expectedMtime: mtime);

            // Verify file was updated
            var content = System.IO.File.ReadAllText(tempFile);
            Assert.Contains("test-key-2", content);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteJSON_MtimeMismatch_ThrowsConflict()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var service = ConfigFileService.Instance;
            var dict = new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>();
            dict["key"] = System.Text.Json.JsonSerializer.SerializeToElement("value");
            service.WriteJSON(dict, tempFile);

            // Use a future mtime to force mismatch
            var futureMtime = DateTime.UtcNow.AddHours(1);

            var ex = Assert.Throws<ConfigFileException>(() =>
                service.WriteJSON(dict, tempFile, expectedMtime: futureMtime));
            Assert.Equal(ConfigFileError.ConflictDetected, ex.Error);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }
}
