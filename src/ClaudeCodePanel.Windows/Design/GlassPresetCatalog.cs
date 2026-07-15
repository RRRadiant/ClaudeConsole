using System;

namespace ClaudeCodePanel.Windows.Design;

public enum GlassPresetKind
{
    Subtle,
    Interactive,
    Prominent,
    Navigation,
    Fallback
}

public sealed record GlassPreset(
    double CornerRadius,
    double SurfaceOpacity,
    double RimOpacity,
    double HoverLift,
    bool UsesElasticity,
    bool UsesChromaticEdge);

public static class GlassPresetCatalog
{
    public static GlassPreset Get(GlassPresetKind kind) => kind switch
    {
        GlassPresetKind.Subtle => new(18, 0.06, 0.12, 0, false, false),
        GlassPresetKind.Interactive => new(16, 0.09, 0.20, 2, true, true),
        GlassPresetKind.Prominent => new(24, 0.12, 0.26, 1, true, true),
        GlassPresetKind.Navigation => new(22, 0.08, 0.18, 0, false, false),
        GlassPresetKind.Fallback => new(18, 0.94, 0.10, 0, false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
