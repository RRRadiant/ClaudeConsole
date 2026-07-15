namespace ClaudeCodePanel.Windows.Design;

public static class UiPerformancePolicy
{
    public static bool ShouldReduceEffects(
        bool reduceMotion,
        bool remoteSession,
        int renderTier) => reduceMotion || remoteSession || renderTier <= 0;

    public static bool ShouldUseContinuousDecorativeAnimation(
        bool reduceMotion,
        bool remoteSession,
        int renderTier) => false;
}
