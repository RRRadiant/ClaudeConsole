namespace ClaudeCodePanel.Windows.Design;

public readonly record struct AppearancePanelState(bool IsExpanded)
{
    public static AppearancePanelState Collapsed { get; } = new(false);

    public AppearancePanelState Toggle() => new(!IsExpanded);
}
