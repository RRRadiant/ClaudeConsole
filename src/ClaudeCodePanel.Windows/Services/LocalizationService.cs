using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Provides localized strings and language switching.
/// Binds to a singleton ResourceManager for Strings.resx/Strings.en.resx.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private static readonly ResourceManager _resManager =
        new("ClaudeCodePanel.Windows.Resources.Strings", typeof(LocalizationService).Assembly);

    public bool IsChinese => CultureInfo.CurrentUICulture.Name.StartsWith("zh");

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService() { }

    /// <summary>Gets a localized string by key.</summary>
    public string Get(string key)
    {
        return _resManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    /// <summary>Gets a formatted localized string.</summary>
    public string Get(string key, params object[] args)
    {
        var fmt = Get(key);
        return string.Format(fmt, args);
    }

    /// <summary>Toggles between Chinese and English.</summary>
    public void ToggleLanguage()
    {
        var target = IsChinese ? "en" : "zh-CN";
        SetLanguage(target);
    }

    /// <summary>Sets the UI language.</summary>
    public void SetLanguage(string culture)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        OnPropertyChanged(nameof(IsChinese));
        OnPropertyChanged("Item[]"); // notify all bindings
    }

    /// <summary>Indexer for XAML binding: {Binding [Key]}.</summary>
    public string this[string key] => Get(key);

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
