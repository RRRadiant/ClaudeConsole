using System;
using System.IO;
using ClaudeCodePanel.Windows.WebUI;

namespace ClaudeCodePanel.Windows.Tests.WebUI;

public class WebUiAssetLocatorTests
{
    [Fact]
    public void GetAssetDirectory_UsesWebUiFolderUnderApplicationBase()
    {
        var path = WebUiAssetLocator.GetAssetDirectory(@"C:\Apps\ClaudeConsole");

        Assert.Equal(@"C:\Apps\ClaudeConsole\WebUI", path);
    }

    [Fact]
    public void IsReady_RequiresIndexHtml()
    {
        var root = Path.Combine(Path.GetTempPath(), $"claude-console-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            Assert.False(WebUiAssetLocator.IsReady(root));
            File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html>");
            Assert.True(WebUiAssetLocator.IsReady(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
