using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClaudeCodePanel.Windows.Design;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Attached behavior that adds page-transition animations to a ContentControl.
/// When content changes, the old view drifts left while fading, then the new
/// view glides in from the right with a slightly longer ease-out.
/// The first content assignment is never animated.
/// </summary>
public static class ContentTransitionBehavior
{
    // ── Attached property ───────────────────────────────────────────

    /// <summary>
    /// Set to true on a ContentControl to enable content-transition animations.
    /// The animation is triggered manually via <see cref="TransitionToAsync"/>.
    /// </summary>
    public static readonly DependencyProperty EnableContentTransitionProperty =
        DependencyProperty.RegisterAttached(
            "EnableContentTransition",
            typeof(bool),
            typeof(ContentTransitionBehavior),
            new PropertyMetadata(false));

    public static bool GetEnableContentTransition(DependencyObject obj) =>
        (bool)obj.GetValue(EnableContentTransitionProperty);

    public static void SetEnableContentTransition(DependencyObject obj, bool value) =>
        obj.SetValue(EnableContentTransitionProperty, value);

    // ── Internal tracking ──────────────────────────────────────────

    private static readonly DependencyProperty IsFirstTransitionProperty =
        DependencyProperty.RegisterAttached(
            "IsFirstTransition",
            typeof(bool),
            typeof(ContentTransitionBehavior),
            new PropertyMetadata(true));

    private static bool GetIsFirstTransition(DependencyObject obj) =>
        (bool)obj.GetValue(IsFirstTransitionProperty);

    private static void SetIsFirstTransition(DependencyObject obj, bool value) =>
        obj.SetValue(IsFirstTransitionProperty, value);

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>
    /// Animates the ContentControl to the new content:
    /// 1. Exit: old content fades out (opacity 1→0) + drifts left (X 0→-18) in 180 ms.
    /// 2. Swap: content is replaced.
    /// 3. Entrance: new content fades in (opacity 0→1) + glides in (X 24→0) in 280 ms.
    ///
    /// The very first call is a no-op (no animation for initial load).
    /// </summary>
    /// <param name="cc">The ContentControl to animate.</param>
    /// <param name="newContent">The new content to display.</param>
    public static async Task TransitionToAsync(ContentControl cc, object newContent)
    {
        ArgumentNullException.ThrowIfNull(cc);

        bool isFirst = GetIsFirstTransition(cc);

        if (isFirst)
        {
            // Initial load — no animation, just set content
            SetIsFirstTransition(cc, false);
            cc.Content = newContent;
            return;
        }

        // Don't animate if the content hasn't actually changed
        if (Equals(cc.Content, newContent))
            return;

        if (ShouldReduceEffects())
        {
            cc.Content = newContent;
            cc.Opacity = 1;
            cc.RenderTransform = Transform.Identity;
            return;
        }

        // ── Step 1: Exit animation ──
        await AnimateExitAsync(cc);

        // ── Step 2: Swap content (hide CC first to prevent flash) ──
        cc.Opacity = 0;
        cc.Content = newContent;

        // ── Step 3: Entrance animation (restores Opacity from 0→1) ──
        await AnimateEntranceAsync(cc);
    }

    // ── Exit animation (180 ms, gentle ease-in) ─

    private static Task<bool> AnimateExitAsync(FrameworkElement element)
    {
        var tcs = new TaskCompletionSource<bool>();

        element.RenderTransform = new TranslateTransform(0, 0);
        element.RenderTransformOrigin = new Point(0.5, 0.5);

        var sb = new Storyboard();

        // Fade out
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fadeOut, element);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fadeOut);

        // Drift left (X 0 → -18)
        var slideLeft = new DoubleAnimation(0, -18, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(slideLeft, element);
        Storyboard.SetTargetProperty(slideLeft,
            new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        sb.Children.Add(slideLeft);

        sb.Completed += (_, _) =>
        {
            element.RenderTransform = Transform.Identity;
            tcs.TrySetResult(true);
        };
        sb.Begin();

        return tcs.Task;
    }

    // ── Entrance animation (280 ms, ease-out) ──────────────────────

    private static Task<bool> AnimateEntranceAsync(FrameworkElement element)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Start state: invisible, shifted down
        element.RenderTransform = new TranslateTransform(24, 0);
        element.Opacity = 0;

        var sb = new Storyboard();

        // Fade in
        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeIn, element);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fadeIn);

        // Glide in (X 24 → 0)
        var slideIn = new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideIn, element);
        Storyboard.SetTargetProperty(slideIn,
            new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        sb.Children.Add(slideIn);

        sb.Completed += (_, _) =>
        {
            element.RenderTransform = Transform.Identity;
            tcs.TrySetResult(true);
        };
        sb.Begin();

        return tcs.Task;
    }

    private static bool ShouldReduceEffects()
    {
        var reduceMotion = SystemParameters.ClientAreaAnimation == false;
        var remoteSession = SystemParameters.IsRemoteSession;
        var renderTier = RenderCapability.Tier >> 16;
        return UiPerformancePolicy.ShouldReduceEffects(reduceMotion, remoteSession, renderTier);
    }
}
