namespace ClaudeCodePanel.Windows.Design;

public enum WindowLayoutMode
{
    Compact,
    Laptop,
    Wide
}

public sealed record WindowLayoutProfile(
    WindowLayoutMode Mode,
    double SidebarWidth,
    double PagePadding,
    bool ShowNavigationLabels,
    bool UseTwoColumnContent)
{
    public static WindowLayoutProfile ForWidth(double width) => width switch
    {
        >= 1280 => new(WindowLayoutMode.Wide, 272, 36, true, true),
        >= 980 => new(WindowLayoutMode.Laptop, 232, 28, true, true),
        >= 820 => new(WindowLayoutMode.Compact, 76, 20, false, false),
        _ => new(WindowLayoutMode.Compact, 76, 16, false, false)
    };
}
