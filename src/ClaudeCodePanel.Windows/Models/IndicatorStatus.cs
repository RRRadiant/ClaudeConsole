namespace ClaudeCodePanel.Windows.Models;

/// <summary>
/// Status indicator state used across Models, Services, and Views.
/// Originally defined in StatusIndicator.xaml.cs — extracted to a shared location
/// so Models don't depend on Views.
/// </summary>
public enum IndicatorStatus
{
    Running,
    Stopped,
    Error
}
