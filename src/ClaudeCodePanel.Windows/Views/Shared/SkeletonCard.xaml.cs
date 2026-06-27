using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeCodePanel.Windows.Views.Shared;

/// <summary>
/// A skeleton loading placeholder that matches GlassCard appearance.
/// Shows 3 rounded placeholder rectangles with a shimmer sweep animation.
/// Swaps to real content when <see cref="IsLoading"/> becomes false.
/// </summary>
public partial class SkeletonCard : UserControl
{
    // ── Dependency Properties ──────────────────────────────────────────

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(SkeletonCard),
            new PropertyMetadata(true, OnIsLoadingChanged));

    public static readonly DependencyProperty CardContentTemplateProperty =
        DependencyProperty.Register(
            nameof(CardContentTemplate),
            typeof(DataTemplate),
            typeof(SkeletonCard),
            new PropertyMetadata(null));

    // ── CLR Wrappers ──────────────────────────────────────────────────

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public DataTemplate? CardContentTemplate
    {
        get => (DataTemplate?)GetValue(CardContentTemplateProperty);
        set => SetValue(CardContentTemplateProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────

    public SkeletonCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartShimmerAnimation();
        UpdateVisualState();
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SkeletonCard)d).UpdateVisualState();
    }

    // ── Visual State ──────────────────────────────────────────────────

    private void UpdateVisualState()
    {
        if (SkeletonLayer == null || ContentLayer == null) return;

        if (IsLoading)
        {
            SkeletonLayer.Visibility = Visibility.Visible;
            ContentLayer.Visibility = Visibility.Collapsed;
            StartShimmerAnimation();
        }
        else
        {
            SkeletonLayer.Visibility = Visibility.Collapsed;
            ContentLayer.Visibility = Visibility.Visible;
            StopShimmerAnimation();
        }
    }

    // ── Shimmer Animation ─────────────────────────────────────────────

    private void StartShimmerAnimation()
    {
        if (ShimmerTranslate == null) return;

        // Animate the translate X from -1 to 2 (left to right sweep), 1.5 s, looping
        var anim = new DoubleAnimation(-1.0, 2.0, TimeSpan.FromSeconds(1.5))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        ShimmerTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void StopShimmerAnimation()
    {
        ShimmerTranslate?.BeginAnimation(TranslateTransform.XProperty, null);
    }
}
