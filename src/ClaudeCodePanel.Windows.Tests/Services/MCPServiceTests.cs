using ClaudeCodePanel.Windows.Helpers;
using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.Tests.Services;

public class MCPServiceTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(401)]
    [InlineData(404)]
    public void ClassifySseStatusCode_ReachableHttpResponse_ReturnsSuccess(int statusCode)
    {
        var result = MCPService.ClassifySseStatusCode(statusCode);

        Assert.Equal(MCPConnectionState.Success, result.State);
        Assert.Contains(statusCode.ToString(), result.Message);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public void ClassifySseStatusCode_ServerError_ReturnsFailure(int statusCode)
    {
        var result = MCPService.ClassifySseStatusCode(statusCode);

        Assert.Equal(MCPConnectionState.Failure, result.State);
        Assert.Contains(statusCode.ToString(), result.Message);
    }

    [Fact]
    public void ClassifyStdioProcessResult_Timeout_ReturnsFailure()
    {
        var result = MCPService.ClassifyStdioProcessResult(
            new ProcessResult(-1, "", "", TimedOut: true));

        Assert.Equal(MCPConnectionState.Failure, result.State);
        Assert.Contains("超时", result.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ClassifyStdioProcessResult_ExecutableExit_ReturnsSuccess(int exitCode)
    {
        var result = MCPService.ClassifyStdioProcessResult(
            new ProcessResult(exitCode, "help", "", TimedOut: false));

        Assert.Equal(MCPConnectionState.Success, result.State);
    }

    [Fact]
    public void ClassifyStdioProcessResult_NonExecutableExit_ReturnsFailureDetail()
    {
        var result = MCPService.ClassifyStdioProcessResult(
            new ProcessResult(127, "", "command not found", TimedOut: false));

        Assert.Equal(MCPConnectionState.Failure, result.State);
        Assert.Equal("command not found", result.Message);
    }
}
