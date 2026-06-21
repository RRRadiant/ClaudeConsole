using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Tests.Models;

public class MCPTypesTests
{
    [Theory]
    [InlineData(MCPServerType.Stdio, "STDIO")]
    [InlineData(MCPServerType.Sse, "SSE")]
    [InlineData(MCPServerType.Builtin, "内置")]
    [InlineData(MCPServerType.Plugin, "插件")]
    public void MCPServerType_Label_ReturnsExpected(MCPServerType type, string expected)
    {
        Assert.Equal(expected, type.Label());
    }

    [Theory]
    [InlineData(MCPServerStatus.Running, "运行中")]
    [InlineData(MCPServerStatus.Starting, "启动中...")]
    [InlineData(MCPServerStatus.Stopping, "停止中...")]
    [InlineData(MCPServerStatus.Stopped, "已停止")]
    [InlineData(MCPServerStatus.Error, "错误")]
    public void MCPServerStatus_Label_ReturnsExpected(MCPServerStatus status, string expected)
    {
        Assert.Equal(expected, status.Label());
    }

    [Theory]
    [InlineData(MCPServerStatus.Running, IndicatorStatus.Running)]
    [InlineData(MCPServerStatus.Starting, IndicatorStatus.Running)]
    [InlineData(MCPServerStatus.Stopping, IndicatorStatus.Stopped)]
    [InlineData(MCPServerStatus.Stopped, IndicatorStatus.Stopped)]
    [InlineData(MCPServerStatus.Error, IndicatorStatus.Error)]
    public void ToIndicatorStatus_ReturnsExpected(MCPServerStatus status, IndicatorStatus expected)
    {
        Assert.Equal(expected, status.ToIndicatorStatus());
    }

    [Fact]
    public void MCPConnectionResult_Unknown_HasCorrectState()
    {
        var result = MCPConnectionResult.Unknown();
        Assert.Equal(MCPConnectionState.Unknown, result.State);
    }

    [Fact]
    public void MCPConnectionResult_Testing_HasCorrectState()
    {
        var result = MCPConnectionResult.Testing();
        Assert.Equal(MCPConnectionState.Testing, result.State);
    }

    [Fact]
    public void MCPConnectionResult_Success_HasMessage()
    {
        var result = MCPConnectionResult.Success("服务器可达");
        Assert.Equal(MCPConnectionState.Success, result.State);
        Assert.Equal("服务器可达", result.Message);
        Assert.Equal("服务器可达", result.Label);
        Assert.Equal(IndicatorStatus.Running, result.IndicatorStatus);
    }

    [Fact]
    public void MCPConnectionResult_Failure_HasMessage()
    {
        var result = MCPConnectionResult.Failure("连接超时");
        Assert.Equal(MCPConnectionState.Failure, result.State);
        Assert.Equal("连接超时", result.Message);
        Assert.Equal("连接超时", result.Label);
        Assert.Equal(IndicatorStatus.Error, result.IndicatorStatus);
    }
}
