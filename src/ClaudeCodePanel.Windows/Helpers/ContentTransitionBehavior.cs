using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Attached behavior that adds page-transition animations to a ContentControl.
/// When content changes, the old view fades out + slides up (200 ms cubic-bezier),
/// and the new view fades in + slides down (300 ms ease-out).
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
    /// 1. Exit: old content fades out (opacity 1→0) + slides up (Y 0→-8) in 200 ms.
    /// 2. Swap: content is replaced.
    /// 3. Entrance: new content fades in (opacity 0→1) + slides down (Y 8→0) in 300 ms.
    ///
    /// The very first call is a no-op (no animation for initial load).
    /// </summary>
    /// <param name="cc">The ContentControl to animate.</param>
    /// <param name="newContent">The new content to display.</param>
    public static async Task TransitionToAsync(ContentControl cc, object newContent)
    {
        if (cc == null) throw new ArgumentNullException(nameof(cc));

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

        // ── Step 1: Exit animation ──
        await AnimateExitAsync(cc);

        // ── Step 2: Swap content (hide CC first to prevent flash) ──
        cc.Opacity = 0;
        cc.Content = newContent;

        // ── Step 3: Entrance animation (restores Opacity from 0→1) ──
        await AnimateEntranceAsync(cc);
    }

    // ── Exit animation (200 ms, cubic-bezier → CubicEase EaseInOut) ─

    private static Task AnimateExitAsync(FrameworkElement element)
    {
        var tcs = new TaskCompletionSource<bool>();

        element.RenderTransform = new TranslateTransform(0, 0);
        element.RenderTransformOrigin = new Point(0.5, 0.5);

        var sb = new Storyboard();

        // Fade out
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(fadeOut, element);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fadeOut);

        // Slide up (Y 0 → -8)
        var slideUp = new DoubleAnimation(0, -8, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(slideUp, element);
        Storyboard.SetTargetProperty(slideUp,
            new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        sb.Children.Add(slideUp);

        sb.Completed += (_, _) =>
        {
            element.RenderTransform = Transform.Identity;
            tcs.TrySetResult(true);
        };
        sb.Begin();

        return tcs.Task;
    }

    // ── Entrance animation (300 ms, ease-out) ──────────────────────

    private static Task AnimateEntranceAsync(FrameworkElement element)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Start state: invisible, shifted down
        element.RenderTransform = new TranslateTransform(0, 8);
        element.Opacity = 0;

        var sb = new Storyboard();

        // Fade in
        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeIn, element);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fadeIn);

        // Slide down (Y 8 → 0)
        var slideDown = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideDown, element);
        Storyboard.SetTargetProperty(slideDown,
            new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        sb.Children.Add(slideDown);

        sb.Completed += (_, _) =>
        {
            element.RenderTransform = Transform.Identity;
            tcs.TrySetResult(true);
        };
        sb.Begin();

        return tcs.Task;
    }
}
