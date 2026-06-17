using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Converters
{
    /// <summary>
    /// Converts a boolean value to <see cref="Visibility"/>.
    /// true  → Visible
    /// false → Collapsed
    /// Set Invert = true to reverse the mapping.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// When true, the conversion is inverted: true → Collapsed, false → Visible.
        /// </summary>
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool effective = Invert ? !boolValue : boolValue;
                return effective ? Visibility.Visible : Visibility.Collapsed;
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                return Invert ? !result : result;
            }
            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts a boolean value to its inverse.
    /// true  → false
    /// false → true
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts an <see cref="IndicatorStatus"/> to a <see cref="SolidColorBrush"/>.
    /// Running → #4ea88d (green)
    /// Stopped → #999999 (gray)
    /// Error   → #cf6b6b (red)
    /// </summary>
    [ValueConversion(typeof(IndicatorStatus), typeof(SolidColorBrush))]
    public class StatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush RunningBrush  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ea88d"));
        private static readonly SolidColorBrush StoppedBrush  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
        private static readonly SolidColorBrush ErrorBrush    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cf6b6b"));
        private static readonly SolidColorBrush FallbackBrush = new SolidColorBrush(Colors.Transparent);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IndicatorStatus status)
            {
                return status switch
                {
                    IndicatorStatus.Running => RunningBrush,
                    IndicatorStatus.Stopped => StoppedBrush,
                    IndicatorStatus.Error   => ErrorBrush,
                    _                       => FallbackBrush
                };
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("StatusToColorConverter does not support ConvertBack.");
        }
    }

    /// <summary>
    /// Converts a boolean value to an opacity double.
    /// true  → 1.0 (fully opaque)
    /// false → 0.0 (fully transparent)
    /// </summary>
    [ValueConversion(typeof(bool), typeof(double))]
    public class StatusToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? 1.0 : 0.0;

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return d > 0.5;

            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts a reference value to <see cref="Visibility"/>.
    /// not null → Visible
    /// null     → Collapsed
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NullToVisibilityConverter does not support ConvertBack.");
        }
    }

    /// <summary>
    /// Converts a boolean value to a localized status label.
    /// true  → "已启用" (Enabled)
    /// false → "已禁用" (Disabled)
    /// The labels can be overridden via the TrueText / FalseText properties.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class BoolToTextConverter : IValueConverter
    {
        /// <summary>
        /// Text displayed when the value is true. Defaults to "已启用".
        /// </summary>
        public string TrueText { get; set; } = "已启用";

        /// <summary>
        /// Text displayed when the value is false. Defaults to "已禁用".
        /// </summary>
        public string FalseText { get; set; } = "已禁用";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? TrueText : FalseText;

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
                return text == TrueText;

            return DependencyProperty.UnsetValue;
        }
    }

    /// <summary>
    /// Converts a boolean value to <see cref="Visibility"/>, inverted.
    /// true  → Collapsed
    /// false → Visible
    /// Convenience class — equivalent to BoolToVisibilityConverter with Invert = true.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Collapsed;

            return DependencyProperty.UnsetValue;
        }
    }
}
