using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudeCodePanel.Windows.Helpers;
using Microsoft.Win32;

namespace ClaudeCodePanel.Windows.Services;

public enum ThemeMode
{
    System,
    Light,
    Dark,
    Custom
}

/// <summary>
/// Manages shell appearance, including base theme mode and accent palette.
/// Theme resources are applied before the window is created and can be swapped
/// at runtime with a lightweight overlay transition.
/// </summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    public static ThemeService Instance { get; } = new();

    private const string DarkThemePath = "Resources/Themes/DarkTheme.xaml";
    private const string LightThemePath = "Resources/Themes/LightTheme.xaml";

    private static string PrefsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "ClaudeCodePanel", "theme_pref.txt");

    private ThemeMode _currentThemeMode = ThemeMode.System;
    private Color _customAccentColor = (Color)ColorConverter.ConvertFromString("#4F8CFF");

    public ThemeMode CurrentThemeMode => _currentThemeMode;

    public bool IsDarkTheme => ResolveIsDarkTheme(_currentThemeMode);

    public Color CustomAccentColor => _customAccentColor;

    public Color ActiveAccentColor =>
        _currentThemeMode == ThemeMode.Custom
            ? _customAccentColor
            : GetDefaultAccentColor(IsDarkTheme);

    public int AppearanceRevision { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void LoadSavedTheme()
    {
        try
        {
            if (File.Exists(PrefsPath))
            {
                var lines = File.ReadAllLines(PrefsPath);
                ParsePreference(lines);
            }
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("ThemeService.LoadPreference", ex);
        }

        ApplyThemeResources();
    }

    public void ToggleTheme()
    {
        SetThemeMode(IsDarkTheme ? ThemeMode.Light : ThemeMode.Dark);
    }

    public void SetTheme(bool dark)
    {
        SetThemeMode(dark ? ThemeMode.Dark : ThemeMode.Light);
    }

    public void SetThemeMode(ThemeMode mode)
    {
        if (_currentThemeMode == mode)
            return;

        _currentThemeMode = mode;
        SwitchThemeWithAnimation();
        SavePreference();
        NotifyAppearanceChanged();
    }

    public void SetCustomAccent(Color color)
    {
        var modeChanged = _currentThemeMode != ThemeMode.Custom;
        var colorChanged = _customAccentColor != color;

        if (!modeChanged && !colorChanged)
            return;

        _customAccentColor = color;
        _currentThemeMode = ThemeMode.Custom;
        SwitchThemeWithAnimation();
        SavePreference();
        NotifyAppearanceChanged();
    }

    private void ParsePreference(IEnumerable<string> lines)
    {
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.Contains('='))
            {
                // Backward compatibility with the previous "dark"/"light" file format.
                _currentThemeMode = string.Equals(line, "light", StringComparison.OrdinalIgnoreCase)
                    ? ThemeMode.Light
                    : ThemeMode.Dark;
                continue;
            }

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                continue;

            switch (parts[0].ToLowerInvariant())
            {
                case "mode":
                    if (Enum.TryParse(parts[1], true, out ThemeMode mode))
                        _currentThemeMode = mode;
                    break;
                case "accent":
                    if (TryParseColor(parts[1], out var color))
                        _customAccentColor = color;
                    break;
            }
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (_currentThemeMode is not (ThemeMode.System or ThemeMode.Custom))
            return;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            ApplyThemeResources();
            NotifyAppearanceChanged();
        });
    }

    private void NotifyAppearanceChanged()
    {
        AppearanceRevision++;
        OnPropertyChanged(nameof(CurrentThemeMode));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(CustomAccentColor));
        OnPropertyChanged(nameof(ActiveAccentColor));
        OnPropertyChanged(nameof(AppearanceRevision));
    }

    private void ApplyThemeResources()
    {
        ApplyThemeDict();
        ApplyAccentOverrides();
    }

    private void SwitchThemeWithAnimation()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow == null)
        {
            ApplyThemeResources();
            return;
        }

        if (mainWindow.Content is not Grid rootGrid)
        {
            ApplyThemeResources();
            return;
        }

        try
        {
            var overlay = new Border
            {
                Background = new SolidColorBrush(IsDarkTheme
                    ? Color.FromRgb(0x08, 0x0D, 0x1F)
                    : Color.FromRgb(0xF8, 0xF9, 0xFA)),
                Opacity = 1.0,
                IsHitTestVisible = false
            };

            rootGrid.Children.Add(overlay);
            ApplyThemeResources();

            var fade = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };

            fade.Completed += (_, _) =>
            {
                if (overlay.Parent is Panel panel)
                    panel.Children.Remove(overlay);
            };

            overlay.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("ThemeService.SwitchThemeWithAnimation", ex);
            ApplyThemeResources();
        }
    }

    private void ApplyThemeDict()
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var themePath = IsDarkTheme ? DarkThemePath : LightThemePath;

        var toRemove = new List<ResourceDictionary>();
        foreach (var dict in merged)
        {
            var src = dict.Source?.ToString() ?? string.Empty;
            if (src.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                src.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                toRemove.Add(dict);
            }
        }

        foreach (var dict in toRemove)
            merged.Remove(dict);

        var newDict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
        merged.Insert(0, newDict);

        Debug.WriteLine($"[ThemeService] Mode={_currentThemeMode}, resolved={(IsDarkTheme ? "DARK" : "LIGHT")}");
    }

    private void ApplyAccentOverrides()
    {
        var accent = ActiveAccentColor;
        var hover = Blend(accent, Colors.White, IsDarkTheme ? 0.18 : 0.10);
        var subtle = WithAlpha(accent, IsDarkTheme ? (byte)0x24 : (byte)0x18);
        var glow = WithAlpha(accent, IsDarkTheme ? (byte)0x54 : (byte)0x32);
        var border = WithAlpha(accent, IsDarkTheme ? (byte)0x52 : (byte)0x66);

        SetBrushResource("AccentBrush", accent);
        SetBrushResource("AccentHoverBrush", hover);
        SetBrushResource("AccentSubtleBrush", subtle);
        SetBrushResource("AccentGlowBrush", glow);
        SetBrushResource("BorderAccentBrush", border);
    }

    private static void SetBrushResource(string key, Color color)
    {
        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    private void SavePreference()
    {
        try
        {
            var dir = Path.GetDirectoryName(PrefsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllLines(PrefsPath,
            [
                $"mode={_currentThemeMode}",
                $"accent=#{_customAccentColor.R:X2}{_customAccentColor.G:X2}{_customAccentColor.B:X2}"
            ]);
        }
        catch (Exception ex)
        {
            SharedHelpers.SafeLog("ThemeService.SavePreference", ex);
        }
    }

    private static bool ResolveIsDarkTheme(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => false,
        ThemeMode.Dark => true,
        ThemeMode.System => !GetSystemUsesLightTheme(),
        ThemeMode.Custom => !GetSystemUsesLightTheme(),
        _ => true
    };

    private static bool GetSystemUsesLightTheme()
    {
        try
        {
            using var personalizeKey =
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = personalizeKey?.GetValue("AppsUseLightTheme");

            return value switch
            {
                int i => i > 0,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static Color GetDefaultAccentColor(bool darkTheme) =>
        darkTheme
            ? Color.FromRgb(0x6F, 0xAA, 0xDD)
            : Color.FromRgb(0x25, 0x63, 0xEB);

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Blend(Color baseColor, Color mixColor, double amount)
    {
        var clamped = Math.Clamp(amount, 0.0, 1.0);
        byte Mix(byte source, byte target) =>
            (byte)Math.Round(source + ((target - source) * clamped));

        return Color.FromArgb(
            0xFF,
            Mix(baseColor.R, mixColor.R),
            Mix(baseColor.G, mixColor.G),
            Mix(baseColor.B, mixColor.B));
    }

    private static bool TryParseColor(string raw, out Color color)
    {
        try
        {
            var parsed = ColorConverter.ConvertFromString(raw);
            if (parsed is Color parsedColor)
            {
                color = parsedColor;
                return true;
            }
        }
        catch
        {
            // ignored
        }

        color = default;
        return false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
