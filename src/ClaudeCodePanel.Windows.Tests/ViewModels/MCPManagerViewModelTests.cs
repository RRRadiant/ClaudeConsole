using ClaudeCodePanel.Windows.Models;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Tests.ViewModels;

public class MCPManagerViewModelTests
{
    [Fact]
    public void SaveServer_PluginWithoutProjectPath_StopsAndShowsError()
    {
        var viewModel = new MCPManagerViewModel
        {
            NewName = "plugin:test",
            NewServerType = MCPServerType.Plugin,
            NewEnabled = true
        };

        viewModel.SaveServer();

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Empty(viewModel.Servers);
    }

    [Fact]
    public void ResolveProjectPath_EmptyValue_ReturnsNull()
    {
        var result = MCPManagerViewModel.ResolveProjectPath("", MCPServerType.Plugin);

        Assert.Null(result);
    }
}
