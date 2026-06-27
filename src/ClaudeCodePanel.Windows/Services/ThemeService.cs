using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using ClaudeCodePanel.Windows.Helpers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Manages dark/light theme switching. Persists preference to disk.
/// Theme is applied at startup before any window is created.
/// Supports animated transitions for smooth theme changes.
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    public static ThemeService Instance { get; } = new();

    private const string DarkThemePath = "Resources/Themes/DarkTheme.xaml";
    private const string LightThemePath = "Resources/Themes/LightTheme.xaml";

    private static string PrefsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "ClaudeCodePanel", "theme_pref.txt");

    private bool _isDarkTheme = true;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        private set
        {
            if (_isDarkTheme == value) return;
            _isDarkTheme = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private ThemeService() { }

    /// <summary>Load saved theme and apply it before window creation.</summary>
    public void LoadSavedTheme()
    {
        try
        {
            if (File.Exists(PrefsPath))
            {
                var saved = File.ReadAllText(PrefsPath).Trim();
                IsDarkTheme = saved != "light";
            }
        }
        catch (Exception ex) { SharedHelpers.SafeLog("ThemeService.LoadPreference", ex); /* use default */ }
        ApplyThemeDict();
    }

    /// <summary>
    /// Toggles dark/light with an animated overlay transition:
    /// a full-window overlay fades out in 3 steps (0ms→40%, 150ms→70%, 300ms→100%),
    /// masking the abrupt resource-dictionary swap underneath.
    /// </summary>
    public void ToggleTheme()
    {
        // Set backing field directly so IsDarkTheme returns new value but
        // PropertyChanged fires AFTER the dictionary swap (step 2) so that
        // any handler recreating views sees the new theme resources.
        _isDarkTheme = !_isDarkTheme;
        SwitchThemeWithAnimation();    // calls ApplyThemeDict() → dictionary swapped
        SavePreference();
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    /// <summary>
    /// Switches the theme resource dictionary with a 3-step fade-out overlay.
    /// The overlay hides the content while the dictionary swaps, then fades out
    /// gradually to reveal the new theme.
    /// </summary>
    private void SwitchThemeWithAnimation()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow == null)
        {
            ApplyThemeDict();
            return;
        }

        // Find the root Grid to place the overlay on
        if (mainWindow.Content is not UIElement rootElement)
        {
            ApplyThemeDict();
            return;
        }

        try
        {
            // ── Create full-window overlay ──
            var overlay = new Border
            {
                Background = new SolidColorBrush(
                    IsDarkTheme
                        ? Color.FromRgb(0x08, 0x0d, 0x1f)   // dark bg
                        : Color.FromRgb(0xf8, 0xf9, 0xfa)),  // light bg
                Opacity = 1.0,
                IsHitTestVisible = false
            };

            // Place overlay on top of everything
            if (mainWindow.Content is Grid rootGrid)
            {
                rootGrid.Children.Add(overlay);
            }
            else
            {
                ApplyThemeDict();
                return;
            }

            // ── Swap the resource dictionary underneath ──
            ApplyThemeDict();

            // ── Fade out overlay in 3 steps via DispatcherTimer ──
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            int step = 0;
            timer.Tick += (_, _) =>
            {
                step++;
                switch (step)
                {
                    case 1:
                        overlay.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(150))
                            {
                                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                            });
                        break;
                    case 2:
                        var finalAnim = new DoubleAnimation(overlay.Opacity, 0.0,
                            TimeSpan.FromMilliseconds(150))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        finalAnim.Completed += (_, _) =>
                        {
                            if (overlay.Parent is Panel p)
                                p.Children.Remove(overlay);
                        };
                        overlay.BeginAnimation(UIElement.OpacityProperty, finalAnim);
                        timer.Stop();
                        break;
                }
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("ThemeService.SwitchThemeWithAnimation", ex);
            // If overlay animation fails, ensure theme is still applied
            ApplyThemeDict();
        }
    }

    public void SetTheme(bool dark)
    {
        if (_isDarkTheme == dark) return;
        _isDarkTheme = dark;
        SwitchThemeWithAnimation();
        SavePreference();
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private void ApplyThemeDict()
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var themePath = IsDarkTheme ? DarkThemePath : LightThemePath;

        // Collect and remove all existing theme dictionaries
        var toRemove = new List<ResourceDictionary>();
        foreach (var dict in merged)
        {
            var src = dict.Source?.ToString() ?? "";
            if (src.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                src.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                toRemove.Add(dict);
            }
        }
        foreach (var dict in toRemove)
            merged.Remove(dict);

        // Insert new theme at index 0
        var newDict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
        merged.Insert(0, newDict);

        Debug.WriteLine($"[ThemeService] Theme switched to {(IsDarkTheme ? "DARK" : "LIGHT")}, " +
                        $"dictionaries in merged: {merged.Count}");
    }

    private void SavePreference()
    {
        try
        {
            var dir = Path.GetDirectoryName(PrefsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(PrefsPath, IsDarkTheme ? "dark" : "light");
        }
        catch (Exception ex) { SharedHelpers.SafeLog("ThemeService.SavePreference", ex); /* best effort */ }
    }
}
