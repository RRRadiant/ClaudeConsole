using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A search field UserControl matching the Swift SearchField design.
    ///
    /// Renders a magnifying glass icon, a text input, and a clear button
    /// (shown only when text is not empty). On focus, the border animates
    /// to accent blue at 60% opacity with a subtle blue glow.
    ///
    /// Layout:
    ///   Height = 32, CornerRadius = 8
    ///   Background: #FFFFFF0D (white at 5% opacity)
    ///   Resting border: white at 6% opacity
    ///   Focused border: accent (#6faadd) at 60% opacity
    ///   Focused glow: accent at 20% opacity, 6 px blur radius
    ///
    /// Animation: 150 ms ease-out on border color matching the Swift
    /// .animation(.easeOut(duration:0.15), value:isFocused) behaviour.
    /// </summary>
    public partial class SearchField : UserControl
    {
        // ── Dependency Properties ──────────────────────────────────────────

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder),
                typeof(string),
                typeof(SearchField),
                new PropertyMetadata(null, OnPlaceholderChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(SearchField),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextPropertyChanged));

        // ── CLR Wrappers ──────────────────────────────────────────────────

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        // ── Color Constants ───────────────────────────────────────────────

        private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);  // #6faadd

        // Resting border: white at 6% opacity
        private static readonly Color RestingBorderColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

        // Focused border: accent at 60% opacity
        private static readonly Color FocusedBorderColor = Color.FromArgb(0x99, AccentColor.R, AccentColor.G, AccentColor.B);

        // Focused glow: accent at 20% opacity
        private static readonly Color FocusedGlowColor = Color.FromArgb(0x33, AccentColor.R, AccentColor.G, AccentColor.B);

        // ── Internal State ────────────────────────────────────────────────

        // Created in code so the brush is not frozen and can be animated.
        private readonly SolidColorBrush _borderBrush;

        private bool _isFocused;
        private bool _isUpdating;  // guard flag to prevent re-entrant DP updates

        // ── Constructor ───────────────────────────────────────────────────

        public SearchField()
        {
            InitializeComponent();

            _borderBrush = new SolidColorBrush(RestingBorderColor);
            StrokeBorder.BorderBrush = _borderBrush;

            UpdatePlaceholder();
            UpdateWatermarkVisibility();
            UpdateClearButtonVisibility();
            ApplyFocusState(animate: false);
        }

        // ── Property Changed Callbacks ────────────────────────────────────

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SearchField)d).UpdatePlaceholder();
        }

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SearchField)d).OnTextDpChanged((string)e.NewValue);
        }

        // ── Placeholder / Watermark ───────────────────────────────────────

        private void UpdatePlaceholder()
        {
            if (WatermarkBlock == null)
                return;

            WatermarkBlock.Text = Placeholder ?? string.Empty;
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Shows the watermark TextBlock when the TextBox is empty, unfocused,
        /// and a non-empty Placeholder has been set. Hides it otherwise.
        /// </summary>
        private void UpdateWatermarkVisibility()
        {
            if (WatermarkBlock == null)
                return;

            var isEmpty = string.IsNullOrEmpty(InputTextBox?.Text);

            WatermarkBlock.Visibility =
                (isEmpty && !_isFocused && !string.IsNullOrEmpty(Placeholder))
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // ── Text Synchronisation ──────────────────────────────────────────

        /// <summary>
        /// Updates the Text dependency property on every keystroke from the
        /// visible TextBox. Uses the _isUpdating guard to avoid re-entrant
        /// updates when the TextBox is set programmatically.
        /// This provides PropertyChanged semantics for real-time two-way binding.
        /// </summary>
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating)
                return;

            var currentText = InputTextBox.Text;
            if ((string)GetValue(TextProperty) != currentText)
            {
                _isUpdating = true;
                SetValue(TextProperty, currentText);
                _isUpdating = false;
            }

            UpdateClearButtonVisibility();
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Handles the Text dependency property changing from an external source
        /// (e.g. a ViewModel binding). Updates the TextBox text accordingly.
        /// </summary>
        private void OnTextDpChanged(string newValue)
        {
            if (_isUpdating)
                return;

            _isUpdating = true;

            if (InputTextBox != null && InputTextBox.Text != newValue)
            {
                InputTextBox.Text = newValue ?? string.Empty;
            }

            _isUpdating = false;
            UpdateClearButtonVisibility();
            UpdateWatermarkVisibility();
        }

        // ── Clear Button ──────────────────────────────────────────────────

        /// <summary>
        /// Shows the clear button when the TextBox contains text;
        /// hides it when the TextBox is empty (matching the Swift
        /// <c>if !text.isEmpty</c> conditional).
        /// </summary>
        private void UpdateClearButtonVisibility()
        {
            if (ClearButton == null)
                return;

            ClearButton.Visibility = string.IsNullOrEmpty(InputTextBox?.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>
        /// Clears the text and returns focus to the TextBox so the user can
        /// continue typing without interruption.
        /// </summary>
        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            // Set the TextBox directly; the TextChanged handler will propagate
            // the empty value to the Text dependency property.
            InputTextBox.Text = string.Empty;
            InputTextBox.Focus();
        }

        // ── Focus State ───────────────────────────────────────────────────

        /// <summary>
        /// Marks the field as focused and applies the focus visual state.
        /// </summary>
        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            _isFocused = true;
            ApplyFocusState(animate: true);
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Marks the field as unfocused and restores the resting visual state.
        /// </summary>
        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            _isFocused = false;
            ApplyFocusState(animate: true);
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Animates the border brush and glow effect to the focused or resting
        /// appearance. The <paramref name="animate"/> parameter controls whether
        /// the transition is animated (150 ms ease-out, matching the Swift
        /// .animation duration) or applied immediately (used during initialisation).
        /// </summary>
        private void ApplyFocusState(bool animate)
        {
            if (_borderBrush == null || GlowShadow == null)
                return;

            if (_isFocused)
            {
                // Focus glow: 6 px blur, accent blue at 20% opacity
                GlowShadow.BlurRadius = 6;
                GlowShadow.ShadowDepth = 0;
                GlowShadow.Color = FocusedGlowColor;
                GlowShadow.Opacity = 1.0;

                AnimateBorderTo(FocusedBorderColor, animate);
            }
            else
            {
                // Resting state: no glow, subtle white border
                GlowShadow.BlurRadius = 0;
                GlowShadow.ShadowDepth = 0;
                GlowShadow.Opacity = 0;

                AnimateBorderTo(RestingBorderColor, animate);
            }
        }

        // ── Border Animation ──────────────────────────────────────────────

        /// <summary>
        /// Animates the border brush to the target color using a 150 ms ease-out
        /// animation, matching the Swift animation duration.
        /// When <paramref name="animate"/> is false the color is applied immediately.
        /// </summary>
        private void AnimateBorderTo(Color targetColor, bool animate)
        {
            if (_borderBrush == null)
                return;

            // Kill any running animation so we can snapshot the current value.
            _borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            if (!animate)
            {
                _borderBrush.Color = targetColor;
                return;
            }

            var fromColor = _borderBrush.Color;

            // Avoid animating when the from and to colors are the same.
            if (fromColor == targetColor)
                return;

            var animation = new ColorAnimation
            {
                From = fromColor,
                To = targetColor,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
    }
}
