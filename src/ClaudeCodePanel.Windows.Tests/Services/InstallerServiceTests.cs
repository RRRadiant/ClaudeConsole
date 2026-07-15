using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.Tests.Services;

public class InstallerServiceTests
{
    [Fact]
    public async Task InstallCliAsync_NpmUnavailable_StopsBeforeInstall()
    {
        var runner = new FakeRunner((_, arguments, _) =>
            arguments == "--version"
                ? Failure("npm unavailable")
                : Failure("not found"));
        var service = new InstallerService(runner.RunAsync, () =>
            Task.FromResult(new InstallerService.CliStatus { Installed = false }));

        var result = await service.InstallCliAsync(InstallerService.InstallMethod.Npm);

        Assert.False(result.Success);
        Assert.Contains("npm", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(runner.Calls, call => call.Arguments.StartsWith("install "));
    }

    [Fact]
    public async Task InstallCliAsync_NpmInstallFails_DoesNotUseMirrorFallback()
    {
        var runner = new FakeRunner((_, arguments, _) =>
        {
            if (arguments == "--version") return Success("10.0.0");
            if (arguments.StartsWith("install ")) return Failure("network failure");
            return Success();
        });
        var service = new InstallerService(runner.RunAsync, () =>
            Task.FromResult(new InstallerService.CliStatus { Installed = false }));

        var result = await service.InstallCliAsync(InstallerService.InstallMethod.Npm);

        Assert.False(result.Success);
        Assert.DoesNotContain(runner.Calls, call =>
            call.Arguments.Contains("npmmirror", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(runner.Calls, call =>
            call.Arguments.Contains("registry.npmjs.org", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InstallCliAsync_FreshInstallNotDetected_CleansUpPartialNpmInstall()
    {
        var runner = new FakeRunner((_, arguments, _) =>
        {
            if (arguments == "--version") return Success("10.0.0");
            return Success();
        });
        var probeResults = new Queue<InstallerService.CliStatus>(
        [
            new InstallerService.CliStatus { Installed = false },
            new InstallerService.CliStatus { Installed = false }
        ]);
        var service = new InstallerService(
            runner.RunAsync,
            () => Task.FromResult(probeResults.Dequeue()));

        var result = await service.InstallCliAsync(InstallerService.InstallMethod.Npm);

        Assert.False(result.Success);
        Assert.Contains(runner.Calls, call =>
            call.Arguments.StartsWith("uninstall -g ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallCliAsync_ExistingInstallFailure_DoesNotUninstallPreviousVersion()
    {
        var runner = new FakeRunner((_, arguments, _) =>
        {
            if (arguments == "--version") return Success("10.0.0");
            if (arguments.StartsWith("install ")) return Failure("upgrade failed");
            return Success();
        });
        var service = new InstallerService(runner.RunAsync, () =>
            Task.FromResult(new InstallerService.CliStatus
            {
                Installed = true,
                Version = "1.2.3",
                Path = "claude.cmd"
            }));

        var result = await service.InstallCliAsync(InstallerService.InstallMethod.Npm);

        Assert.False(result.Success);
        Assert.DoesNotContain(runner.Calls, call =>
            call.Arguments.StartsWith("uninstall -g ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallCliAsync_Winget_UsesExactOfficialPackageId()
    {
        var runner = new FakeRunner((_, _, _) => Success());
        var probeResults = new Queue<InstallerService.CliStatus>(
        [
            new InstallerService.CliStatus { Installed = false },
            new InstallerService.CliStatus { Installed = true, Path = @"C:\Program Files\WindowsApps\claude.exe" }
        ]);
        var service = new InstallerService(
            runner.RunAsync,
            () => Task.FromResult(probeResults.Dequeue()));

        var result = await service.InstallCliAsync(InstallerService.InstallMethod.Winget);

        Assert.True(result.Success);
        Assert.Contains(runner.Calls, call =>
            call.FileName == "winget" &&
            call.Arguments.Contains("--id Anthropic.ClaudeCode --exact", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UninstallCliAsync_WingetInstallation_UsesWingetAndVerifiesRemoval()
    {
        var runner = new FakeRunner((_, _, _) => Success());
        var probeResults = new Queue<InstallerService.CliStatus>(
        [
            new InstallerService.CliStatus
            {
                Installed = true,
                Path = @"C:\Program Files\WindowsApps\Anthropic.ClaudeCode\claude.exe"
            },
            new InstallerService.CliStatus { Installed = false }
        ]);
        var service = new InstallerService(
            runner.RunAsync,
            () => Task.FromResult(probeResults.Dequeue()));

        var result = await service.UninstallCliAsync();

        Assert.True(result.Success);
        Assert.Contains(runner.Calls, call =>
            call.FileName == "winget" && call.Arguments.StartsWith("uninstall --id "));
        Assert.DoesNotContain(runner.Calls, call =>
            call.Arguments.StartsWith("uninstall -g "));
    }

    [Fact]
    public async Task UninstallCliAsync_CommandSucceedsButClaudeRemains_ReturnsFailure()
    {
        var runner = new FakeRunner((_, arguments, _) =>
            arguments == "--version" ? Success("10.0.0") : Success());
        var installed = new InstallerService.CliStatus
        {
            Installed = true,
            Path = @"C:\Users\test\AppData\Roaming\npm\claude.cmd"
        };
        var service = new InstallerService(
            runner.RunAsync,
            () => Task.FromResult(installed));

        var result = await service.UninstallCliAsync();

        Assert.False(result.Success);
        Assert.Contains("仍", result.Error!);
    }

    private static ProcessResult Success(string stdout = "") => new(0, stdout, "", false);
    private static ProcessResult Failure(string stderr) => new(1, "", stderr, false);

    private sealed class FakeRunner(
        Func<string, string, int, ProcessResult> handler)
    {
        public List<(string FileName, string Arguments, int Timeout)> Calls { get; } = new();

        public Task<ProcessResult> RunAsync(string fileName, string arguments, int timeout)
        {
            Calls.Add((fileName, arguments, timeout));
            return Task.FromResult(handler(fileName, arguments, timeout));
        }
    }
}
