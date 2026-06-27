using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A status indicator dot with animated pulse, color-matched drop shadow,
    /// and smooth 300 ms color transitions between states.
    ///
    /// Status values:
    ///   "Running" — green dot (#34d399), 2s breathing pulse (0.4↔1.0)
    ///   "Stopped" — muted gray, static, opacity 0.5
    ///   "Error"   — red dot (#f87171), rapid 0.3s blink ×3 then solid
    /// </summary>
    public partial class StatusIndicator : UserControl
    {
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(StatusIndicator),
                new PropertyMetadata("Stopped", OnStatusChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatusIndicator),
                new PropertyMetadata(string.Empty, OnLabelChanged));

        public string Status
        {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        // Status colors
        private static readonly Color RunningColor = Color.FromRgb(0x34, 0xd3, 0x99);
        private static readonly Color StoppedColor = Color.FromRgb(0x99, 0x99, 0x99);
        private static readonly Color ErrorColor = Color.FromRgb(0xf8, 0x71, 0x71);

        private string? _previousStatus;
        private Color _previousColor = StoppedColor;

        public StatusIndicator()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LabelTextBlock.Text = Label ?? string.Empty;
            ApplyStatus();
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (StatusIndicator)d;
            if (control.IsLoaded)
                control.ApplyStatus();
        }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (StatusIndicator)d;
            control.LabelTextBlock.Text = (e.NewValue as string) ?? string.Empty;
        }

        /// <summary>Applies color, glow, and animation based on current status.</summary>
        private void ApplyStatus()
        {
            // Stop any running animation
            StatusEllipse.BeginAnimation(UIElement.OpacityProperty, null);

            string status = Status ?? "Stopped";

            Color targetColor;
            switch (status)
            {
                case "Running":
                    targetColor = RunningColor;
                    break;
                case "Error":
                    targetColor = ErrorColor;
                    break;
                default: // "Stopped"
                    targetColor = StoppedColor;
                    break;
            }

            // ── 300 ms color transition from previous color ──
            AnimateColorTransition(targetColor);

            // ── Apply glow color ──
            if (DotGlow != null)
                DotGlow.Color = targetColor;

            _previousColor = targetColor;

            // ── Animate based on status ──
            switch (status)
            {
                case "Running":
                    StartBreathingPulse(); // 2s breathing, 0.4↔1.0
                    break;

                case "Error":
                    StartErrorBlink(); // 0.3s blink ×3, then solid 1.0
                    break;

                default: // "Stopped"
                    StatusEllipse.Opacity = 0.5; // static, muted
                    break;
            }

            _previousStatus = status;
        }

        // ── 300 ms color transition ─────────────────────────────────────

        private void AnimateColorTransition(Color targetColor)
        {
            var fromBrush = StatusEllipse.Fill as SolidColorBrush;
            Color fromColor = fromBrush?.Color ?? _previousColor;

            var brush = new SolidColorBrush(fromColor);
            StatusEllipse.Fill = brush;

            var anim = new ColorAnimation(fromColor, targetColor, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        // ── Breathing pulse (Running): 2s, opacity 0.4↔1.0, Forever ───

        private void StartBreathingPulse()
        {
            StatusEllipse.Opacity = 0.4;

            var anim = new DoubleAnimation(0.4, 1.0, TimeSpan.FromSeconds(2.0))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            StatusEllipse.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // ── Error blink: 0.3s interval, 3 flashes, then solid 1.0 ──────

        private void StartErrorBlink()
        {
            var sb = new Storyboard();

            // 3 flashes = 6 keyframes (on/off/on/off/on/off) over 1.8s
            double interval = 0.3; // seconds per half-blink
            double totalDuration = interval * 6; // 1.8s for 3 full flashes

            var anim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(totalDuration),
                RepeatBehavior = new RepeatBehavior(1) // play once
            };

            // Flash 1
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, TimeSpan.FromSeconds(0)));
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.3, TimeSpan.FromSeconds(interval)));
            // Flash 2
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, TimeSpan.FromSeconds(interval * 2)));
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.3, TimeSpan.FromSeconds(interval * 3)));
            // Flash 3
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, TimeSpan.FromSeconds(interval * 4)));
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.3, TimeSpan.FromSeconds(interval * 5)));
            // Final solid
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, TimeSpan.FromSeconds(interval * 6)));

            Storyboard.SetTarget(anim, StatusEllipse);
            Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(anim);

            // After animation ends, ensure solid 1.0
            sb.Completed += (_, _) =>
            {
                StatusEllipse.Opacity = 1.0;
            };

            StatusEllipse.Opacity = 1.0;
            sb.Begin();
        }
    }
}
