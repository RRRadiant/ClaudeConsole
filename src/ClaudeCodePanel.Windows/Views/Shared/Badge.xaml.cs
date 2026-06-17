using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A pill-shaped status badge matching the Claude-Win .status-pill design.
    ///
    /// Variants:
    ///   "Info"    — Accent blue (#6faadd)
    ///   "Success" — Green (#4ea88d)
    ///   "Neutral" — Secondary gray
    ///   "Warning" — Amber (#d6a24a)
    ///   "Danger"  — Red (#cf6b6b)
    /// </summary>
    public partial class Badge : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(Badge),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(nameof(Variant), typeof(string), typeof(Badge),
                new PropertyMetadata("Info", OnVariantChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Variant
        {
            get => (string)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        // Liquid Glass colors
        private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);
        private static readonly Color SuccessColor = Color.FromRgb(0x4e, 0xa8, 0x8d);
        private static readonly Color WarningColor = Color.FromRgb(0xd6, 0xa2, 0x4a);
        private static readonly Color DangerColor = Color.FromRgb(0xcf, 0x6b, 0x6b);
        private static readonly Color NeutralColor = Color.FromRgb(0x99, 0x99, 0x99);

        private readonly SolidColorBrush _foregroundBrush;
        private readonly SolidColorBrush _backgroundBrush;
        private readonly SolidColorBrush _borderBrush;

        public Badge()
        {
            InitializeComponent();

            _foregroundBrush = new SolidColorBrush(AccentColor);
            _backgroundBrush = new SolidColorBrush(Color.FromArgb(0x14, 0x6f, 0xaa, 0xdd));
            _borderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0x6f, 0xaa, 0xdd));

            BadgeTextBlock.Foreground = _foregroundBrush;
            BadgeBorder.Background = _backgroundBrush;
            BadgeBorder.BorderBrush = _borderBrush;

            BadgeTextBlock.Text = Text ?? string.Empty;
            ApplyVariant();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BadgeTextBlock.Text = Text ?? string.Empty;
            ApplyVariant();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var badge = (Badge)d;
            if (badge.BadgeTextBlock != null)
                badge.BadgeTextBlock.Text = (e.NewValue as string) ?? string.Empty;
        }

        private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Badge)d).ApplyVariant();
        }

        private void ApplyVariant()
        {
            if (_foregroundBrush == null || _backgroundBrush == null || _borderBrush == null) return;

            var variant = Variant ?? "Info";
            Color fg, bg;

            switch (variant)
            {
                case "Success":
                    fg = SuccessColor;
                    bg = Color.FromArgb(0x14, 0x4e, 0xa8, 0x8d);
                    _borderBrush.Color = Color.FromArgb(0x40, 0x4e, 0xa8, 0x8d);
                    break;

                case "Warning":
                    fg = WarningColor;
                    bg = Color.FromArgb(0x14, 0xd6, 0xa2, 0x4a);
                    _borderBrush.Color = Color.FromArgb(0x40, 0xd6, 0xa2, 0x4a);
                    break;

                case "Danger":
                    fg = DangerColor;
                    bg = Color.FromArgb(0x14, 0xcf, 0x6b, 0x6b);
                    _borderBrush.Color = Color.FromArgb(0x40, 0xcf, 0x6b, 0x6b);
                    break;

                case "Neutral":
                    fg = NeutralColor;
                    bg = Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF);
                    _borderBrush.Color = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
                    break;

                default: // "Info"
                    fg = AccentColor;
                    bg = Color.FromArgb(0x14, 0x6f, 0xaa, 0xdd);
                    _borderBrush.Color = Color.FromArgb(0x1A, 0x6f, 0xaa, 0xdd);
                    break;
            }

            _foregroundBrush.Color = fg;
            _backgroundBrush.Color = bg;
        }
    }
}
