using System.IO;

namespace ClaudeCodePanel.Windows.WebUI;

public static class WebUiAssetLocator
{
    public static string GetAssetDirectory(string applicationBaseDirectory) =>
        Path.Combine(applicationBaseDirectory, "WebUI");

    public static bool IsReady(string assetDirectory) =>
        File.Exists(Path.Combine(assetDirectory, "index.html"));
}
