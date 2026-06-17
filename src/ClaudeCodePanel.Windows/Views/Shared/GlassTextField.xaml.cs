using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A glass-effect text field UserControl matching the Swift GlassTextField design.
    ///
    /// Variants:
    ///   "Regular" — height 40, standard single-line TextBox, font 15
    ///   "Compact" — height 32, smaller TextBox, font 13
    ///   "Secure"  — height 40, PasswordBox with show/hide toggle button
    ///
    /// States:
    ///   Normal   — translucent white background (#0DFFFFFF), subtle white border (6% opacity)
    ///   Focused  — accent border at 60% opacity + blue glow (DropShadowEffect)
    ///   Error    — red border (#FF3B30) + red glow, error message shown below
    ///
    /// The Text property updates on every keystroke (PropertyChanged semantics)
    /// for real-time two-way binding.
    /// </summary>
    public partial class GlassTextField : UserControl
    {
        // ── Dependency Properties ──────────────────────────────────────────

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder),
                typeof(string),
                typeof(GlassTextField),
                new PropertyMetadata(null, OnPlaceholderChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(GlassTextField),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextChanged));

        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(
                nameof(Variant),
                typeof(string),
                typeof(GlassTextField),
                new PropertyMetadata("Regular", OnVariantChanged));

        public static readonly DependencyProperty IsErrorProperty =
            DependencyProperty.Register(
                nameof(IsError),
                typeof(bool),
                typeof(GlassTextField),
                new PropertyMetadata(false, OnIsErrorChanged));

        public static readonly DependencyProperty ErrorMessageProperty =
            DependencyProperty.Register(
                nameof(ErrorMessage),
                typeof(string),
                typeof(GlassTextField),
                new PropertyMetadata(null, OnErrorMessageChanged));

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

        public string Variant
        {
            get => (string)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public bool IsError
        {
            get => (bool)GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        // ── Color Constants ───────────────────────────────────────────────

        private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);  // #6faadd
        private static readonly Color ErrorColor = Color.FromRgb(0xcf, 0x6b, 0x6b);   // #cf6b6b

        // Resting border: white at 6% opacity
        private static readonly Color RestingBorderColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

        // Focused border: accent at 60% opacity
        private static readonly Color FocusedBorderColor = Color.FromArgb(0x99, AccentColor.R, AccentColor.G, AccentColor.B);

        // Focused glow: accent at 20% opacity
        private static readonly Color FocusedGlowColor = Color.FromArgb(0x33, AccentColor.R, AccentColor.G, AccentColor.B);

        // Error glow: red at 30% opacity
        private static readonly Color ErrorGlowColor = Color.FromArgb(0x4D, ErrorColor.R, ErrorColor.G, ErrorColor.B);

        // ── Internal State ────────────────────────────────────────────────

        // Created in code so brushes are not frozen and can be animated.
        private readonly SolidColorBrush _borderBrush;

        private bool _isFocused;
        private bool _isSecureVisible;  // true = password shown as plain text
        private bool _isUpdating;        // guard flag to prevent re-entrant DP updates

        // ── Constructor ───────────────────────────────────────────────────

        public GlassTextField()
        {
            InitializeComponent();

            _borderBrush = new SolidColorBrush(RestingBorderColor);
            StrokeBorder.BorderBrush = _borderBrush;

            ApplyVariant();
            UpdatePlaceholder();
            UpdateErrorState(animate: false);
            ApplyFocusState(animate: false);
        }

        // ── Property Changed Callbacks ────────────────────────────────────

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassTextField)d).UpdatePlaceholder();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassTextField)d).OnTextPropertyChanged((string)e.NewValue);
        }

        private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassTextField)d).ApplyVariant();
        }

        private static void OnIsErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassTextField)d).UpdateErrorState(animate: true);
        }

        private static void OnErrorMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassTextField)d).UpdateErrorMessage();
        }

        // ── Variant Application ───────────────────────────────────────────

        /// <summary>
        /// Applies height, font size, and toggles visibility of the password
        /// box and show/hide button based on the current Variant.
        ///
        /// Regular — height 40, font 15, TextBox only
        /// Compact — height 32, font 13, TextBox only
        /// Secure  — height 40, font 15, PasswordBox + toggle button
        /// </summary>
        private void ApplyVariant()
        {
            if (StrokeBorder == null || InputTextBox == null || InputPasswordBox == null)
                return;

            var variant = Variant ?? "Regular";

            switch (variant)
            {
                case "Compact":
                    StrokeBorder.Height = 32;
                    InputTextBox.FontSize = 13;
                    InputPasswordBox.FontSize = 13;
                    ToggleSecureButton.Visibility = Visibility.Collapsed;
                    InputPasswordBox.Visibility = Visibility.Collapsed;
                    InputTextBox.Visibility = Visibility.Visible;
                    break;

                case "Secure":
                    StrokeBorder.Height = 40;
                    InputTextBox.FontSize = 15;
                    InputPasswordBox.FontSize = 15;
                    ToggleSecureButton.Visibility = Visibility.Visible;
                    // Show PasswordBox or TextBox based on _isSecureVisible
                    ApplySecureVisibility();
                    break;

                default: // "Regular"
                    StrokeBorder.Height = 40;
                    InputTextBox.FontSize = 15;
                    InputPasswordBox.FontSize = 15;
                    ToggleSecureButton.Visibility = Visibility.Collapsed;
                    InputPasswordBox.Visibility = Visibility.Collapsed;
                    InputTextBox.Visibility = Visibility.Visible;
                    break;
            }

            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Toggles visibility between TextBox and PasswordBox for the Secure variant.
        /// When revealing the password (isSecureVisible=true), the PasswordBox text
        /// is copied to the TextBox. When hiding (isSecureVisible=false), the TextBox
        /// text is copied to the PasswordBox.
        /// </summary>
        private void ApplySecureVisibility()
        {
            if ((Variant ?? "Regular") != "Secure")
                return;

            if (_isSecureVisible)
            {
                // Copy password from PasswordBox to TextBox before switching
                InputPasswordBox.Visibility = Visibility.Collapsed;
                _isUpdating = true;
                InputTextBox.Text = InputPasswordBox.Password;
                _isUpdating = false;
                InputTextBox.Visibility = Visibility.Visible;

                // Update toggle button to show "eye.slash" (click to hide)
                ToggleSecureButton.Content = ""; // Hide glyph
            }
            else
            {
                // Copy text from TextBox to PasswordBox before switching
                InputTextBox.Visibility = Visibility.Collapsed;
                InputPasswordBox.Password = InputTextBox.Text;
                InputPasswordBox.Visibility = Visibility.Visible;

                // Update toggle button to show "eye" (click to reveal)
                ToggleSecureButton.Content = ""; // RedEye glyph
            }

            UpdateWatermarkVisibility();
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
        /// Shows the watermark when the active input is empty and unfocused.
        /// Hides it otherwise (text present, or focused).
        /// </summary>
        private void UpdateWatermarkVisibility()
        {
            if (WatermarkBlock == null)
                return;

            var variant = Variant ?? "Regular";

            bool isEmpty;
            if (variant == "Secure" && !_isSecureVisible)
            {
                isEmpty = string.IsNullOrEmpty(InputPasswordBox?.Password);
            }
            else
            {
                isEmpty = string.IsNullOrEmpty(InputTextBox?.Text);
            }

            WatermarkBlock.Visibility =
                (isEmpty && !_isFocused && !string.IsNullOrEmpty(Placeholder))
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // ── Focus State ───────────────────────────────────────────────────

        /// <summary>
        /// Animates the border brush and glow effect to the focused appearance
        /// (accent border + blue shadow). The <paramref name="animate"/> parameter
        /// controls whether the transition is animated (150 ms ease-out) or applied
        /// immediately.
        /// </summary>
        private void ApplyFocusState(bool animate)
        {
            if (_borderBrush == null || GlowShadow == null)
                return;

            if (_isFocused)
            {
                // Focus glow: 6 px blur, blue tint at 20% opacity
                GlowShadow.BlurRadius = 6;
                GlowShadow.ShadowDepth = 0;
                GlowShadow.Color = FocusedGlowColor;
                GlowShadow.Opacity = 1.0;

                AnimateBorderTo(FocusedBorderColor, animate);
            }
            else
            {
                // Resting state: no glow
                GlowShadow.BlurRadius = 0;
                GlowShadow.ShadowDepth = 0;
                GlowShadow.Opacity = 0;

                // If error is active, keep the error border; otherwise restore resting border
                if (!IsError)
                {
                    AnimateBorderTo(RestingBorderColor, animate);
                }
            }
        }

        // ── Error State ───────────────────────────────────────────────────

        /// <summary>
        /// Applies or removes the error visual state (red border + red glow).
        /// The <paramref name="animate"/> parameter controls whether the transition
        /// is animated (150 ms ease-out) or applied immediately.
        /// </summary>
        private void UpdateErrorState(bool animate)
        {
            if (_borderBrush == null || GlowShadow == null)
                return;

            if (IsError)
            {
                // Error glow: 6 px blur, red tint at 30% opacity
                GlowShadow.BlurRadius = 6;
                GlowShadow.ShadowDepth = 0;
                GlowShadow.Color = ErrorGlowColor;
                GlowShadow.Opacity = 1.0;

                AnimateBorderTo(ErrorColor, animate);
            }
            else
            {
                // Remove error glow; restore focus or resting border
                if (!_isFocused)
                {
                    GlowShadow.BlurRadius = 0;
                    GlowShadow.ShadowDepth = 0;
                    GlowShadow.Opacity = 0;
                }
                else
                {
                    // Still focused, restore focus glow
                    GlowShadow.BlurRadius = 6;
                    GlowShadow.ShadowDepth = 0;
                    GlowShadow.Color = FocusedGlowColor;
                    GlowShadow.Opacity = 1.0;
                }

                AnimateBorderTo(_isFocused ? FocusedBorderColor : RestingBorderColor, animate);
            }

            UpdateErrorMessage();
        }

        /// <summary>
        /// Shows or hides the error message TextBlock below the input.
        /// Only shown when IsError is true AND ErrorMessage is non-empty.
        /// </summary>
        private void UpdateErrorMessage()
        {
            if (ErrorMessageBlock == null)
                return;

            var message = ErrorMessage;
            if (IsError && !string.IsNullOrEmpty(message))
            {
                ErrorMessageBlock.Text = message;
                ErrorMessageBlock.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorMessageBlock.Visibility = Visibility.Collapsed;
            }
        }

        // ── Input Event Handlers ──────────────────────────────────────────

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            _isFocused = true;
            ApplyFocusState(animate: true);
            UpdateWatermarkVisibility();
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            _isFocused = false;
            ApplyFocusState(animate: true);
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Updates the Text dependency property on every keystroke from the
        /// visible TextBox. Uses the _isUpdating guard to avoid re-entrant
        /// updates when the TextBox is set programmatically.
        /// This provides PropertyChanged semantics for real-time two-way binding.
        /// </summary>
        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
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

            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Updates the Text dependency property on every keystroke from the
        /// PasswordBox (only active in Secure variant when password is hidden).
        /// </summary>
        private void OnPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating)
                return;

            var currentPassword = InputPasswordBox.Password;
            if ((string)GetValue(TextProperty) != currentPassword)
            {
                _isUpdating = true;
                SetValue(TextProperty, currentPassword);
                _isUpdating = false;
            }

            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Handles the Text dependency property changing from an external source
        /// (e.g. a ViewModel binding). Updates the active input control accordingly.
        /// </summary>
        private void OnTextPropertyChanged(string newValue)
        {
            if (_isUpdating)
                return;

            _isUpdating = true;

            var variant = Variant ?? "Regular";

            if (variant == "Secure" && !_isSecureVisible)
            {
                if (InputPasswordBox != null && InputPasswordBox.Password != newValue)
                {
                    InputPasswordBox.Password = newValue ?? string.Empty;
                }
            }
            else
            {
                if (InputTextBox != null && InputTextBox.Text != newValue)
                {
                    InputTextBox.Text = newValue ?? string.Empty;
                }
            }

            _isUpdating = false;
            UpdateWatermarkVisibility();
        }

        /// <summary>
        /// Toggles password visibility for the Secure variant.
        /// Swaps between PasswordBox and TextBox, syncing the content.
        /// </summary>
        private void OnToggleSecureClick(object sender, RoutedEventArgs e)
        {
            _isSecureVisible = !_isSecureVisible;
            ApplySecureVisibility();
        }

        // ── Border Animation ──────────────────────────────────────────────

        /// <summary>
        /// Animates the border brush to the target color using a 150 ms ease-out
        /// animation, matching the Swift animation duration.
        /// When <paramref name="animate"/> is false the color is applied immediately
        /// (used during initialisation).
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
