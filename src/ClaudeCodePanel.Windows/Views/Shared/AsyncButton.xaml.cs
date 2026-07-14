#pragma warning disable CA1716
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A button UserControl matching the Swift AsyncButton design.
    /// Wraps an ICommand and shows one of four visual states:
    ///   Idle    — title and optional icon glyph on the accent background
    ///   Loading — indeterminate ProgressBar, disabled, accent at 60% opacity
    ///   Success — green background with a white checkmark, auto-resets after 1 s
    ///   Error   — red background with a white x-mark, auto-resets after 1.5 s
    ///
    /// The control disables itself while in the Loading state so that the
    /// command cannot be re-entered.
    ///
    /// Variants:
    ///   "Primary"   — accent-colour background with white text
    ///   "Secondary" — transparent tinted background with accent-colour text
    /// </summary>
    public partial class AsyncButton : UserControl
    {
        // ── Dependency Properties ──────────────────────────────────────────

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(AsyncButton),
                new PropertyMetadata(null, OnTitleChanged));

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(
                nameof(IconGlyph),
                typeof(string),
                typeof(AsyncButton),
                new PropertyMetadata(null, OnIconGlyphChanged));

        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(
                nameof(Variant),
                typeof(string),
                typeof(AsyncButton),
                new PropertyMetadata("Primary", OnVariantChanged));

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(
                nameof(Command),
                typeof(ICommand),
                typeof(AsyncButton),
                new PropertyMetadata(null));

        /// <summary>
        /// Optional parameter passed to the command when it executes.
        /// Not present in the Swift source but included for standard WPF MVVM binding support.
        /// </summary>
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(
                nameof(CommandParameter),
                typeof(object),
                typeof(AsyncButton),
                new PropertyMetadata(null));

        // ── CLR Wrappers ────────────────────────────────────────────────────

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

        // ── Color Constants (matching Swift design) ─────────────────────────

        private static readonly Color AccentColor = Color.FromRgb(0x6f, 0xaa, 0xdd);   // #6faadd
        private static readonly Color SuccessColor = Color.FromRgb(0x4e, 0xa8, 0x8d);  // #4ea88d
        private static readonly Color ErrorColor = Color.FromRgb(0xcf, 0x6b, 0x6b);    // #cf6b6b

        // ── Internal State ──────────────────────────────────────────────────

        private AsyncButtonState _state = AsyncButtonState.Idle;
        private DispatcherTimer? _resetTimer;

        // Created in code so they are not frozen and can be animated.
        private readonly SolidColorBrush _backgroundBrush;

        /// <summary>
        /// The four visual states of the button, matching the Swift
        /// AsyncButton.AsyncButtonState enum.
        /// </summary>
        public enum AsyncButtonState
        {
            Idle,
            Loading,
            Success,
            Error
        }

        // ── Constructor ─────────────────────────────────────────────────────

        public AsyncButton()
        {
            InitializeComponent();

            _backgroundBrush = new SolidColorBrush(AccentColor);
            RootBorder.Background = _backgroundBrush;

            UpdateTitle();
            UpdateIconGlyph();
            ApplyVisualState(animate: false);
        }

        // ── Property Changed Callbacks ──────────────────────────────────────

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AsyncButton)d).UpdateTitle();
        }

        private static void OnIconGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AsyncButton)d).UpdateIconGlyph();
        }

        private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (AsyncButton)d;
            button.ResetAnimations();
            button.ApplyVisualState(animate: false);
        }

        // ── Title & Icon Updates ────────────────────────────────────────────

        /// <summary>
        /// Sets the title text on the label. Empty string when Title is null.
        /// </summary>
        private void UpdateTitle()
        {
            if (TitleTextBlock == null)
                return;

            TitleTextBlock.Text = Title ?? string.Empty;
        }

        /// <summary>
        /// Shows or hides the icon glyph TextBlock and sets its text content.
        /// The glyph is collapsed when the property is null or empty.
        /// When collapsed its Margin also collapses, removing the 5 px gap.
        /// </summary>
        private void UpdateIconGlyph()
        {
            if (IconGlyphElement == null)
                return;

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

        // ── Visual State Application ────────────────────────────────────────

        /// <summary>
        /// Switches the visible panel, background colour, and foreground colours
        /// to match the current <see cref="_state"/> and <see cref="Variant"/>.
        ///
        /// State behaviour:
        ///   Idle    — IdlePanel visible; background per variant.
        ///   Loading — LoadingPanel visible; accent at 60 % opacity; control disabled.
        ///   Success — SuccessIcon visible; solid green (#34C759).
        ///   Error   — ErrorIcon visible; solid red (#FF3B30).
        ///
        /// Variant behaviour (Idle only):
        ///   Primary   — accent fill, white text.
        ///   Secondary — accent at 8 % opacity fill, accent-colour text.
        /// </summary>
        /// <param name="animate">
        /// When true, background colour transitions are animated with a 200 ms
        /// ease-out matching the Swift animation duration.
        /// </param>
        private void ApplyVisualState(bool animate)
        {
            if (RootBorder == null || _backgroundBrush == null)
                return;

            var variant = Variant ?? "Primary";

            // ── Visibility ──────────────────────────────────────────────
            IdlePanel.Visibility = _state == AsyncButtonState.Idle
                ? Visibility.Visible : Visibility.Collapsed;
            LoadingPanel.Visibility = _state == AsyncButtonState.Loading
                ? Visibility.Visible : Visibility.Collapsed;
            SuccessIcon.Visibility = _state == AsyncButtonState.Success
                ? Visibility.Visible : Visibility.Collapsed;
            ErrorIcon.Visibility = _state == AsyncButtonState.Error
                ? Visibility.Visible : Visibility.Collapsed;

            // ── Background ──────────────────────────────────────────────
            Color bgTarget;
            Color fgTarget;

            switch (_state)
            {
                case AsyncButtonState.Idle:
                    if (variant == "Secondary")
                    {
                        bgTarget = Color.FromArgb(0x14, AccentColor.R, AccentColor.G, AccentColor.B);
                        fgTarget = AccentColor;
                    }
                    else
                    {
                        bgTarget = AccentColor;
                        fgTarget = Colors.White;
                    }
                    break;

                case AsyncButtonState.Loading:
                    bgTarget = Color.FromArgb(0x99, AccentColor.R, AccentColor.G, AccentColor.B);
                    fgTarget = Colors.White;
                    break;

                case AsyncButtonState.Success:
                    bgTarget = SuccessColor;
                    fgTarget = Colors.White;
                    break;

                case AsyncButtonState.Error:
                    bgTarget = ErrorColor;
                    fgTarget = Colors.White;
                    break;

                default:
                    bgTarget = AccentColor;
                    fgTarget = Colors.White;
                    break;
            }

            if (animate)
            {
                AnimateBackgroundTo(bgTarget);
            }
            else
            {
                _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                _backgroundBrush.Color = bgTarget;
            }

            // ── Foreground (Idle text elements only — icon panels use
            //     hard-coded White foregrounds in XAML) ─────────────────
            if (TitleTextBlock != null)
                TitleTextBlock.Foreground = new SolidColorBrush(fgTarget);
            if (IconGlyphElement != null)
                IconGlyphElement.Foreground = new SolidColorBrush(fgTarget);

            // ── Hit-testing (disabled while loading) ────────────────────
            RootBorder.IsHitTestVisible = _state != AsyncButtonState.Loading;
        }

        // ── Animation ───────────────────────────────────────────────────────

        /// <summary>
        /// Kills any in-progress colour animation on the background brush so
        /// that a hard colour set can follow.
        /// </summary>
        private void ResetAnimations()
        {
            if (_backgroundBrush != null)
                _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        }

        /// <summary>
        /// Smoothly animates the background brush colour to the target.
        /// 200 ms ease-out matching the Swift .animation(.easeOut(duration: 0.2)).
        /// </summary>
        private void AnimateBackgroundTo(Color targetColor)
        {
            if (_backgroundBrush == null)
                return;

            _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            var fromColor = _backgroundBrush.Color;
            var animation = new ColorAnimation
            {
                From = fromColor,
                To = targetColor,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        // ── Reset Timer ─────────────────────────────────────────────────────

        /// <summary>
        /// Starts (or restarts) a one-shot timer that returns the button to
        /// the Idle state after <paramref name="delay"/>.
        /// Matching Swift: 1.0 s for Success, 1.5 s for Error.
        /// </summary>
        private void StartResetTimer(TimeSpan delay)
        {
            StopResetTimer();

            _resetTimer = new DispatcherTimer
            {
                Interval = delay
            };
            _resetTimer.Tick += OnResetTimerTick;
            _resetTimer.Start();
        }

        private void StopResetTimer()
        {
            if (_resetTimer != null)
            {
                _resetTimer.Stop();
                _resetTimer.Tick -= OnResetTimerTick;
                _resetTimer = null;
            }
        }

        private void OnResetTimerTick(object? sender, EventArgs e)
        {
            StopResetTimer();
            _state = AsyncButtonState.Idle;
            ApplyVisualState(animate: true);
        }

        // ── Command Execution ────────────────────────────────────────────────

        /// <summary>
        /// Guards against re-entry (Loading state), then transitions to Loading,
        /// executes the bound ICommand, and on completion transitions to either
        /// Success (1 s auto-reset) or Error (1.5 s auto-reset).
        ///
        /// Commands that implement <see cref="IAsyncCommand"/> are awaited so
        /// that the result-state transition happens after the async work finishes.
        /// Synchronous commands are executed on the dispatcher; any unhandled
        /// exception triggers the Error state.
        /// </summary>
        private async void ExecuteCommand()
        {
            if (_state == AsyncButtonState.Loading)
                return;

            var command = Command;
            if (command == null)
                return;

            if (!command.CanExecute(CommandParameter))
                return;

            _state = AsyncButtonState.Loading;
            ApplyVisualState(animate: true);

            try
            {
                if (command is IAsyncCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(CommandParameter);
                }
                else
                {
                    command.Execute(CommandParameter);
                }

                _state = AsyncButtonState.Success;
                ApplyVisualState(animate: true);
                StartResetTimer(TimeSpan.FromSeconds(1.0));
            }
            catch
            {
                _state = AsyncButtonState.Error;
                ApplyVisualState(animate: true);
                StartResetTimer(TimeSpan.FromSeconds(1.5));
            }
        }

        // ── Mouse Event Handlers ────────────────────────────────────────────

        /// <summary>
        /// Captures the mouse so that MouseUp is received even if the cursor
        /// leaves the element bounds. Ignores the event when loading.
        /// </summary>
        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_state == AsyncButtonState.Loading)
                return;

            RootBorder.CaptureMouse();
        }

        /// <summary>
        /// Releases mouse capture and executes the bound command when the
        /// release happens over the button, matching standard WPF Button
        /// behaviour (drag-away = cancel).
        /// </summary>
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            RootBorder.ReleaseMouseCapture();

            if (_state == AsyncButtonState.Loading)
                return;

            if (RootBorder.IsMouseOver)
            {
                ExecuteCommand();
            }
        }
    }

    /// <summary>
    /// An ICommand whose execution is inherently asynchronous.
    /// When an <see cref="AsyncButton"/> detects that its Command implements
    /// this interface it will await the task before transitioning to the
    /// Success or Error state.
    /// </summary>
    public interface IAsyncCommand : ICommand
    {
        /// <summary>
        /// Asynchronously executes the command.
        /// </summary>
        /// <param name="parameter">Optional command parameter.</param>
        /// <returns>A task that completes when the command work is done.</returns>
        Task ExecuteAsync(object parameter);
    }
}
#pragma warning restore CA1716
