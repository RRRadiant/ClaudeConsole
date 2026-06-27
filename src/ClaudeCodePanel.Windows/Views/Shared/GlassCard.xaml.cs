using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A Liquid Glass card control matching the Claude-Win .glass-panel design.
    /// Provides a multi-layer gradient background with semi-transparent surface,
    /// rounded corners, subtle border with hover animation, and optional title.
    ///
    /// Inherits from ContentControl (not UserControl) to avoid WPF namescope conflicts.
    ///
    /// Variants:
    ///   "Default" — Standard glass panel, 32px radius, 24px padding, 1px border, hover
    ///   "Compact" — Compact card, 24px radius, 18px padding, 1px border, hover
    ///   "Plain"   — 20px padding, no border, no hover
    ///   "Aurora"  — Enhanced glass, stronger highlight, accent-glow shadow
    ///   "Signal"  — Blue-tinted panel, accent glow border
    /// </summary>
    public partial class GlassCard : ContentControl
    {
        // ── Dependency Properties ──────────────────────────────────────────

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(GlassCard),
                new PropertyMetadata(null, OnTitleChanged));

        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(
                nameof(Variant),
                typeof(string),
                typeof(GlassCard),
                new PropertyMetadata("Default", OnVariantChanged));

        // ── CLR Wrappers ──────────────────────────────────────────────────

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Variant
        {
            get => (string)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        // ── Liquid Glass Color Constants ──────────────────────────────────

        // Border: rgba(255,255,255,0.10) resting, rgba(255,255,255,0.15) hover
        private static readonly Color BorderRestingColor = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
        private static readonly Color BorderHoverColor = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);
        // Aurora: rgba(255,255,255,0.15)
        private static readonly Color BorderAuroraColor = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);
        // Signal: rgba(100,180,255,0.10)
        private static readonly Color BorderSignalColor = Color.FromArgb(0x1A, 0x64, 0xb4, 0xff);

        // ── Internal State ────────────────────────────────────────────────

        private readonly SolidColorBrush _borderBrush;
        private Border? _outerBorder;
        private Border? _strokeBorder;
        private Grid? _layoutGrid;
        private TextBlock? _titleTextBlock;
        private Border? _glossHighlight;
        private bool _entranceAnimated;

        // Drop shadows
        private DropShadowEffect? _darkModeShadow;
        private DropShadowEffect? _auroraGlow;

        // ── Constructor ───────────────────────────────────────────────────

        static GlassCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(GlassCard),
                new FrameworkPropertyMetadata(typeof(GlassCard)));
        }

        public GlassCard()
        {
            _borderBrush = new SolidColorBrush(BorderRestingColor);
            Loaded += OnLoaded;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _outerBorder = GetTemplateChild("OuterBorder") as Border;
            _strokeBorder = GetTemplateChild("StrokeBorder") as Border;
            _layoutGrid = GetTemplateChild("LayoutGrid") as Grid;
            _titleTextBlock = GetTemplateChild("TitleTextBlock") as TextBlock;
            _glossHighlight = GetTemplateChild("GlossHighlight") as Border;

            if (_outerBorder != null)
            {
                _outerBorder.MouseEnter += OnMouseEnter;
                _outerBorder.MouseLeave += OnMouseLeave;
            }

            if (_strokeBorder != null)
                _strokeBorder.BorderBrush = _borderBrush;

            UpdateTitle();
            UpdateVariant();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateTitle();
            UpdateVariant();
            ApplyThemeShadows();
            AnimateContentEntrance();

            // Listen for theme changes to re-apply dark-mode shadow
            ThemeService.Instance.PropertyChanged += (_, _) =>
            {
                Dispatcher.Invoke(ApplyThemeShadows);
            };
        }

        // ── Property Changed Callbacks ────────────────────────────────────

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassCard)d).UpdateTitle();
        }

        private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassCard)d).UpdateVariant();
        }

        // ── Visual State Updates ──────────────────────────────────────────

        private void UpdateTitle()
        {
            if (_titleTextBlock == null) return;

            var title = Title;
            if (string.IsNullOrEmpty(title))
            {
                _titleTextBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                _titleTextBlock.Text = title;
                _titleTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void UpdateVariant()
        {
            if (_layoutGrid == null || _strokeBorder == null || _outerBorder == null)
                return;

            var variant = Variant ?? "Default";

            switch (variant)
            {
                case "Compact":
                    _layoutGrid.Margin = new Thickness(18);
                    _strokeBorder.BorderThickness = new Thickness(1);
                    _outerBorder.CornerRadius = new CornerRadius(24);
                    _strokeBorder.CornerRadius = new CornerRadius(24);
                    _borderBrush.Color = BorderRestingColor;
                    RemoveAuroraGlow();
                    break;

                case "Plain":
                    _layoutGrid.Margin = new Thickness(24);
                    _strokeBorder.BorderThickness = new Thickness(0);
                    _outerBorder.CornerRadius = new CornerRadius(32);
                    _strokeBorder.CornerRadius = new CornerRadius(32);
                    RemoveAuroraGlow();
                    break;

                case "Aurora":
                    _layoutGrid.Margin = new Thickness(24);
                    _strokeBorder.BorderThickness = new Thickness(1);
                    _outerBorder.CornerRadius = new CornerRadius(32);
                    _strokeBorder.CornerRadius = new CornerRadius(32);
                    _borderBrush.Color = BorderAuroraColor;
                    ApplyAuroraGlow();
                    break;

                case "Signal":
                    _layoutGrid.Margin = new Thickness(24);
                    _strokeBorder.BorderThickness = new Thickness(1);
                    _outerBorder.CornerRadius = new CornerRadius(32);
                    _strokeBorder.CornerRadius = new CornerRadius(32);
                    _borderBrush.Color = BorderSignalColor;
                    RemoveAuroraGlow();
                    break;

                default: // "Default"
                    _layoutGrid.Margin = new Thickness(24);
                    _strokeBorder.BorderThickness = new Thickness(1);
                    _outerBorder.CornerRadius = new CornerRadius(32);
                    _strokeBorder.CornerRadius = new CornerRadius(32);
                    _borderBrush.Color = BorderRestingColor;
                    RemoveAuroraGlow();
                    break;
            }
        }

        // ── Theme-aware drop shadow ───────────────────────────────────────

        /// <summary>
        /// Applies a subtle DropShadowEffect in dark mode (BlurRadius=16, Opacity=0.15).
        /// Removed in light mode for clarity.
        /// </summary>
        private void ApplyThemeShadows()
        {
            if (_outerBorder == null) return;

            bool isDark = ThemeService.Instance.IsDarkTheme;

            if (isDark)
            {
                if (_darkModeShadow == null)
                {
                    _darkModeShadow = new DropShadowEffect
                    {
                        BlurRadius = 16,
                        Opacity = 0.15,
                        ShadowDepth = 2,
                        Color = Colors.Black
                    };
                }
                // Combine: if Aurora, the aurora glow is additional
                if (_outerBorder.Effect == null || _outerBorder.Effect == _darkModeShadow)
                    _outerBorder.Effect = _darkModeShadow;
            }
            else
            {
                // Remove dark-mode shadow in light mode
                if (_outerBorder.Effect == _darkModeShadow)
                    _outerBorder.Effect = null;
            }
        }

        /// <summary>Adds a blue accent glow (second DropShadowEffect) for Aurora variant.</summary>
        private void ApplyAuroraGlow()
        {
            if (_outerBorder == null) return;

            if (_auroraGlow == null)
            {
                _auroraGlow = new DropShadowEffect
                {
                    BlurRadius = 12,
                    Opacity = 0.35,
                    ShadowDepth = 0,
                    Color = Color.FromRgb(0x6f, 0xaa, 0xdd) // AccentBrush color
                };
            }

            if (_outerBorder.Effect is DropShadowEffect existing)
            {
                // If dark-mode shadow exists, replace with aurora (stronger glow takes priority)
                _outerBorder.Effect = _auroraGlow;
            }
            else
            {
                _outerBorder.Effect = _auroraGlow;
            }
        }

        private void RemoveAuroraGlow()
        {
            if (_outerBorder == null) return;

            if (_outerBorder.Effect == _auroraGlow)
            {
                // Restore dark-mode shadow if applicable
                _outerBorder.Effect = ThemeService.Instance.IsDarkTheme ? _darkModeShadow : null;
            }
        }

        // ── Content entrance animation ────────────────────────────────────

        /// <summary>
        /// Animates the first TextBlock in the card content:
        /// slides up 6px + fades in (300 ms ease-out).
        /// Uses Tag="animated" on the TextBlock to avoid re-triggering.
        /// </summary>
        private void AnimateContentEntrance()
        {
            if (_entranceAnimated) return;
            if (_layoutGrid == null) return;

            // Find the ContentPresenter inside the LayoutGrid (row 1)
            var cp = FindVisualChild<ContentPresenter>(_layoutGrid);
            if (cp == null) return;

            // Defer until content is actually loaded
            cp.Loaded += (_, _) =>
            {
                if (_entranceAnimated) return;

                var firstTextBlock = FindVisualChild<TextBlock>(cp);
                if (firstTextBlock == null) return;
                if (firstTextBlock.Tag is string tag && tag == "animated") return;

                firstTextBlock.Tag = "animated";
                _entranceAnimated = true;

                firstTextBlock.RenderTransform = new TranslateTransform(0, 6);
                firstTextBlock.Opacity = 0;

                var sb = new Storyboard();

                var slideUp = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(slideUp, firstTextBlock);
                Storyboard.SetTargetProperty(slideUp,
                    new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                sb.Children.Add(slideUp);

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeIn, firstTextBlock);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(fadeIn);

                sb.Completed += (_, _) =>
                {
                    firstTextBlock.RenderTransform = Transform.Identity;
                };

                sb.Begin();
            };
        }

        // ── Hover Animation (300 ms) ──────────────────────────────────────

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            var variant = Variant ?? "Default";
            if (variant == "Plain") return;

            Color target;
            switch (variant)
            {
                case "Aurora":
                    target = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF); // 0.20
                    break;
                case "Signal":
                    target = Color.FromArgb(0x26, 0x64, 0xb4, 0xff); // 0.15
                    break;
                default:
                    target = BorderHoverColor;
                    break;
            }

            AnimateBorderTo(target);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            var variant = Variant ?? "Default";
            if (variant == "Plain") return;

            Color target;
            switch (variant)
            {
                case "Aurora":
                    target = BorderAuroraColor;
                    break;
                case "Signal":
                    target = BorderSignalColor;
                    break;
                default:
                    target = BorderRestingColor;
                    break;
            }

            AnimateBorderTo(target);
        }

        private void AnimateBorderTo(Color targetColor)
        {
            if (_borderBrush == null) return;

            _borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            var fromColor = _borderBrush.Color;
            var animation = new ColorAnimation
            {
                From = fromColor,
                To = targetColor,
                Duration = TimeSpan.FromMilliseconds(300), // 300 ms as per prompt
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        // ── Helper ────────────────────────────────────────────────────────

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found) return found;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null) return descendant;
            }
            return null;
        }
    }
}
