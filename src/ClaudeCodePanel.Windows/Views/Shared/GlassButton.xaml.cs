using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A Liquid Glass pill-shaped button matching the Claude-Win .glass-button design.
    ///
    /// Variants:
    ///   "Primary"   — Strong white text on glass bg, bold
    ///   "Secondary" — Muted text, subtle glass bg
    ///   "Ghost"     — Transparent, border appears on hover
    ///   "Accent"    — Blue-tinted (accent) glass, inner glow
    ///   "Danger"    — Red-tinted border + text
    ///
    /// Sizes:
    ///   "Regular" — height 44, horizontal padding 22, font 15
    ///   "Small"   — height 34, horizontal padding 16, font 14
    /// </summary>
    public partial class GlassButton : UserControl
    {
        // ── Dependency Properties ──────────────────────────────────────────

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(GlassButton),
                new PropertyMetadata(null, OnTitleChanged));

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(GlassButton),
                new PropertyMetadata(null, OnIconGlyphChanged));

        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(nameof(Variant), typeof(string), typeof(GlassButton),
                new PropertyMetadata("Primary", OnVariantChanged));

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(string), typeof(GlassButton),
                new PropertyMetadata("Regular", OnSizeChanged));

        public static readonly DependencyProperty IsDisabledProperty =
            DependencyProperty.Register(nameof(IsDisabled), typeof(bool), typeof(GlassButton),
                new PropertyMetadata(false, OnIsDisabledChanged));

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(GlassButton),
                new PropertyMetadata(null));

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(GlassButton),
                new PropertyMetadata(null));

        // ── CLR Wrappers ──────────────────────────────────────────────────

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public string Variant
        {
            get => (string)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public string Size
        {
            get => (string)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public bool IsDisabled
        {
            get => (bool)GetValue(IsDisabledProperty);
            set => SetValue(IsDisabledProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        // ── Liquid Glass Color Constants ──────────────────────────────────

        private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);
        private static readonly Color AccentHoverColor = Color.FromRgb(0x8b, 0xba, 0xff);
        private static readonly Color DangerColor = Color.FromRgb(0xcf, 0x6b, 0x6b);

        // Primary: rgba(255,255,255,0.06) bg, hover stronger
        private static readonly Color PrimaryBgColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
        private static readonly Color PrimaryHoverBgColor = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
        // Accent: rgba(100,180,255,0.08) bg
        private static readonly Color AccentBgColor = Color.FromArgb(0x14, 0x64, 0xb4, 0xff);
        private static readonly Color AccentHoverBgColor = Color.FromArgb(0x1E, 0x64, 0xb4, 0xff);
        // Danger: rgba(207,107,107,0.04) bg
        private static readonly Color DangerBgColor = Color.FromArgb(0x0A, 0xcf, 0x6b, 0x6b);
        private static readonly Color DangerHoverBgColor = Color.FromArgb(0x1A, 0xcf, 0x6b, 0x6b);

        // Borders
        private static readonly Color BorderDefaultColor = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
        private static readonly Color BorderHoverColor = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);
        private static readonly Color BorderAccentColor = Color.FromArgb(0x33, 0x64, 0xb4, 0xff);
        private static readonly Color BorderAccentHoverColor = Color.FromArgb(0x4D, 0x64, 0xb4, 0xff);
        private static readonly Color BorderDangerColor = Color.FromArgb(0x40, 0xcf, 0x6b, 0x6b);
        private static readonly Color BorderDangerHoverColor = Color.FromArgb(0x73, 0xcf, 0x6b, 0x6b);

        // ── Internal State ────────────────────────────────────────────────

        private readonly SolidColorBrush _backgroundBrush;
        private readonly SolidColorBrush _borderBrush;
        private readonly SolidColorBrush _foregroundBrush;
        private bool _isHovered;

        // ── Constructor ───────────────────────────────────────────────────

        public GlassButton()
        {
            InitializeComponent();

            _backgroundBrush = new SolidColorBrush(PrimaryBgColor);
            _borderBrush = new SolidColorBrush(BorderDefaultColor);
            _foregroundBrush = new SolidColorBrush(Colors.White);

            RootBorder.Background = _backgroundBrush;
            RootBorder.BorderBrush = _borderBrush;
            RootBorder.BorderThickness = new Thickness(1);
            TitleTextBlock.Foreground = _foregroundBrush;

            ApplySize();
            UpdateTitle();
            UpdateIconGlyph();
            ApplyVariantState(animate: false);
            ApplyDisabledState();
        }

        // ── Property Changed Callbacks ────────────────────────────────────

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassButton)d).UpdateTitle();
        }

        private static void OnIconGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassButton)d).UpdateIconGlyph();
        }

        private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (GlassButton)d;
            button._isHovered = false;
            button.ResetAnimations();
            button.ApplyVariantState(animate: false);
            button.ApplyDisabledState();
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassButton)d).ApplySize();
        }

        private static void OnIsDisabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassButton)d).ApplyDisabledState();
        }

        // ── Title & Icon ──────────────────────────────────────────────────

        private void UpdateTitle()
        {
            if (TitleTextBlock == null) return;
            TitleTextBlock.Text = Title ?? string.Empty;
        }

        private void UpdateIconGlyph()
        {
            if (IconGlyphElement == null) return;
            var glyph = IconGlyph;
            if (string.IsNullOrEmpty(glyph))
            {
                IconGlyphElement.Visibility = Visibility.Collapsed;
            }
            else
            {
                IconGlyphElement.Text = glyph;
                IconGlyphElement.Visibility = Visibility.Visible;
            }
        }

        // ── Size ──────────────────────────────────────────────────────────

        private void ApplySize()
        {
            if (RootBorder == null || ContentStack == null || TitleTextBlock == null) return;

            var size = Size ?? "Regular";

            switch (size)
            {
                case "Small":
                    RootBorder.Height = 34;
                    ContentStack.Margin = new Thickness(16, 0, 16, 0);
                    TitleTextBlock.FontSize = 14;
                    if (IconGlyphElement != null) IconGlyphElement.FontSize = 14;
                    break;

                default: // "Regular"
                    RootBorder.Height = 44;
                    ContentStack.Margin = new Thickness(22, 0, 22, 0);
                    TitleTextBlock.FontSize = 15;
                    if (IconGlyphElement != null) IconGlyphElement.FontSize = 15;
                    break;
            }
        }

        // ── Variant State ─────────────────────────────────────────────────

        private void ApplyVariantState(bool animate)
        {
            if (RootBorder == null || _backgroundBrush == null ||
                _borderBrush == null || _foregroundBrush == null) return;

            var variant = Variant ?? "Primary";

            Color bgTarget, borderTarget, fgTarget;

            switch (variant)
            {
                case "Secondary":
                    bgTarget = _isHovered
                        ? Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)
                        : Color.FromArgb(0x05, 0xFF, 0xFF, 0xFF);
                    borderTarget = _isHovered ? BorderHoverColor : BorderDefaultColor;
                    fgTarget = Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF); // text-2
                    break;

                case "Ghost":
                    bgTarget = _isHovered
                        ? Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)
                        : Colors.Transparent;
                    borderTarget = _isHovered ? BorderDefaultColor : Colors.Transparent;
                    fgTarget = Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF); // text-2
                    break;

                case "Accent":
                    bgTarget = _isHovered ? AccentHoverBgColor : AccentBgColor;
                    borderTarget = _isHovered ? BorderAccentHoverColor : BorderAccentColor;
                    fgTarget = Color.FromArgb(0xF2, 0xb4, 0xd2, 0xff);
                    break;

                case "Danger":
                    bgTarget = _isHovered ? DangerHoverBgColor : DangerBgColor;
                    borderTarget = _isHovered ? BorderDangerHoverColor : BorderDangerColor;
                    fgTarget = DangerColor;
                    break;

                default: // "Primary"
                    bgTarget = _isHovered ? PrimaryHoverBgColor : PrimaryBgColor;
                    borderTarget = _isHovered ? BorderHoverColor : BorderDefaultColor;
                    fgTarget = Colors.White;
                    break;
            }

            if (animate)
            {
                AnimateBackgroundTo(bgTarget);
                AnimateBorderTo(borderTarget);
                AnimateForegroundTo(fgTarget);
            }
            else
            {
                SetColor(_backgroundBrush, bgTarget);
                SetColor(_borderBrush, borderTarget);
                SetColor(_foregroundBrush, fgTarget);
            }
        }

        // ── Disabled State ────────────────────────────────────────────────

        private void ApplyDisabledState()
        {
            if (RootBorder == null) return;
            var isDisabled = IsDisabled;

            if (isDisabled)
            {
                RootBorder.Opacity = 0.35;
                RootBorder.IsHitTestVisible = false;
            }
            else
            {
                RootBorder.Opacity = 1.0;
                RootBorder.IsHitTestVisible = true;
            }
        }

        // ── Animation Helpers ─────────────────────────────────────────────

        private void SetColor(SolidColorBrush brush, Color color)
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = color;
        }

        private void ResetAnimations()
        {
            if (_backgroundBrush != null) _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            if (_borderBrush != null) _borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            if (_foregroundBrush != null) _foregroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            if (PressScale != null)
            {
                PressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                PressScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                PressScale.ScaleX = 1.0;
                PressScale.ScaleY = 1.0;
            }
        }

        private void AnimateBackgroundTo(Color targetColor)
        {
            AnimateColor(_backgroundBrush, targetColor, 160);
        }

        private void AnimateBorderTo(Color targetColor)
        {
            AnimateColor(_borderBrush, targetColor, 160);
        }

        private void AnimateForegroundTo(Color targetColor)
        {
            AnimateColor(_foregroundBrush, targetColor, 160);
        }

        private static void AnimateColor(SolidColorBrush? brush, Color targetColor, int durationMs)
        {
            if (brush == null) return;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            var fromColor = brush.Color;
            var animation = new ColorAnimation
            {
                From = fromColor,
                To = targetColor,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private void AnimatePressScale(double targetScale)
        {
            if (PressScale == null) return;

            PressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PressScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            var duration = TimeSpan.FromMilliseconds(80);
            var easing = new CubicEase { EasingMode = EasingMode.EaseIn };

            var animX = new DoubleAnimation(PressScale.ScaleX, targetScale, duration) { EasingFunction = easing };
            var animY = new DoubleAnimation(PressScale.ScaleY, targetScale, duration) { EasingFunction = easing };

            PressScale.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
            PressScale.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
        }

        // ── Mouse Event Handlers ──────────────────────────────────────────

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (IsDisabled) return;
            _isHovered = true;
            ApplyVariantState(animate: true);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isHovered = false;
            ApplyVariantState(animate: true);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsDisabled) return;
            AnimatePressScale(0.97);
            RootBorder.CaptureMouse();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            RootBorder.ReleaseMouseCapture();

            if (IsDisabled) return;
            AnimatePressScale(1.0);

            var command = Command;
            if (RootBorder.IsMouseOver && command != null && command.CanExecute(CommandParameter))
            {
                command.Execute(CommandParameter);
            }
        }
    }
}
