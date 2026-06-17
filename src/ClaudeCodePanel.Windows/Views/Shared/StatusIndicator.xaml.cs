using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A status indicator dot with animated pulse and label.
    /// Matches the Claude-Win Liquid Glass design.
    ///
    /// Status values:
    ///   "Running" — green dot (#4ea88d), pulse 1.5s
    ///   "Stopped" — muted gray, static
    ///   "Error"   — red dot (#cf6b6b), pulse 0.8s
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

        // Liquid Glass status colors
        private static readonly Color RunningColor = Color.FromRgb(0x4e, 0xa8, 0x8d);
        private static readonly Color StoppedColor = Color.FromRgb(0x99, 0x99, 0x99);
        private static readonly Color ErrorColor = Color.FromRgb(0xcf, 0x6b, 0x6b);

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

        private void ApplyStatus()
        {
            StatusEllipse.BeginAnimation(OpacityProperty, null);

            string status = Status ?? "Stopped";

            switch (status)
            {
                case "Running":
                    StatusEllipse.Fill = new SolidColorBrush(RunningColor);
                    StartRunningAnimation();
                    break;

                case "Error":
                    StatusEllipse.Fill = new SolidColorBrush(ErrorColor);
                    StartErrorAnimation();
                    break;

                default: // "Stopped"
                    StatusEllipse.Fill = new SolidColorBrush(StoppedColor);
                    StatusEllipse.Opacity = 1.0;
                    break;
            }
        }

        private void StartRunningAnimation()
        {
            var animation = new DoubleAnimation
            {
                From = 0.5, To = 1.0,
                Duration = TimeSpan.FromSeconds(1.5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            StatusEllipse.BeginAnimation(OpacityProperty, animation);
        }

        private void StartErrorAnimation()
        {
            var animation = new DoubleAnimation
            {
                From = 0.3, To = 1.0,
                Duration = TimeSpan.FromSeconds(0.8),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            StatusEllipse.BeginAnimation(OpacityProperty, animation);
        }
    }
}
