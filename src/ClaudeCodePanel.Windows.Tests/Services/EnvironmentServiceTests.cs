using System.IO;
using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.Tests.Services;

public class EnvironmentServiceTests
{
    [Fact]
    public async Task CheckAllDepsAsync_ProbesEachDependencyVersionOnlyOnce()
    {
        var calls = new List<(string FileName, string Arguments)>();
        Task<ProcessResult> RunAsync(string fileName, string arguments, int _)
        {
            lock (calls)
                calls.Add((fileName, arguments));

            if (fileName == "where")
                return Task.FromResult(new ProcessResult(0, $@"C:\tools\{arguments}.exe", "", false));

            var version = Path.GetFileNameWithoutExtension(fileName) switch
            {
                "node" => "v22.0.0",
                "npm" => "10.0.0",
                "git" => "git version 2.50.0",
                _ => ""
            };
            return Task.FromResult(new ProcessResult(0, version, "", false));
        }

        var service = new EnvironmentService(RunAsync, _ => true);

        var results = await service.CheckAllDepsAsync();

        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.True(result.Installed));
        Assert.Equal("v22.0.0", results.Single(result => result.Name == "node").Version);
        Assert.Equal("10.0.0", results.Single(result => result.Name == "npm").Version);
        Assert.Equal("git version 2.50.0", results.Single(result => result.Name == "git").Version);
        Assert.Equal(3, calls.Count(call => call.Arguments == "--version"));
        Assert.Equal(1, calls.Count(call => call.FileName == "where" && call.Arguments == "node"));
        Assert.Equal(1, calls.Count(call => call.FileName == "where" && call.Arguments == "npm"));
        Assert.Equal(1, calls.Count(call => call.FileName == "where" && call.Arguments == "git"));
    }
}
