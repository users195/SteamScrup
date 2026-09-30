using System.ComponentModel;
using System.Globalization;

namespace SteamScrup.Core;

public enum AppLanguage { System, ChineseSimplified, English }

/// <summary>
/// Tiny in-process localization service: no satellite assemblies, no resx, no
/// generated code. Strings are embedded as dictionaries so switching language at
/// runtime is a single PropertyChanged notification.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    private readonly Dictionary<string, string> _zh = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _en = new(StringComparer.Ordinal);
    private AppLanguage _language = AppLanguage.System;

    private Localizer()
    {
        LocalizationStrings.LoadChinese(_zh);
        LocalizationStrings.LoadEnglish(_en);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the active language changes.</summary>
    public event EventHandler? LanguageChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The language actually in effect once "System" is resolved.</summary>
    public bool IsChinese => _language switch
    {
        AppLanguage.ChineseSimplified => true,
        AppLanguage.English => false,
        _ => CurrentCultureIsChinese(),
    };

    public string CurrentLanguageCode => IsChinese ? "zh-CN" : "en";

    private static bool CurrentCultureIsChinese()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Indexer used by XAML bindings so the UI updates when the language changes.</summary>
    public string this[string key] => Get(key);

    /// <summary>Keys requested that were not found in either table. Diagnostic only.</summary>
    private readonly HashSet<string> _missingKeys = new(StringComparer.Ordinal);

    /// <summary>Distinct keys that resolved to nothing; surfaced by the self-test.</summary>
    public IReadOnlyCollection<string> MissingKeys => _missingKeys;

    public string Get(string key)
    {
        var table = IsChinese ? _zh : _en;
        if (table.TryGetValue(key, out var value)) return value;

        // Fall back to the other table so a missing translation is still readable.
        var fallback = IsChinese ? _en : _zh;
        if (fallback.TryGetValue(key, out var other)) return other;

        // Neither table has it. Returning the key keeps the UI alive, but it has to be
        // recorded: this silent fallback previously hid a whole class of bug (icon glyphs
        // were looked up here, came back as their key names, and rendered as rows of
        // missing-glyph boxes).
        _missingKeys.Add(key);
        return key;
    }

    /// <summary>True when the key exists in the active language or the other one.</summary>
    public bool Has(string key) =>
        _zh.ContainsKey(key) || _en.ContainsKey(key);

    public string Format(string key, params object?[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch (FormatException) { return template; }
    }

    /// <summary>Applies the language preference parsed from settings.</summary>
    public static AppLanguage ParseLanguage(string? value) => value?.ToLowerInvariant() switch
    {
        "zh" or "zh-cn" or "chinese" or "chinesesimplified" => AppLanguage.ChineseSimplified,
        "en" or "en-us" or "english" => AppLanguage.English,
        _ => AppLanguage.System,
    };

    public static string LanguageToSetting(AppLanguage language) => language switch
    {
        AppLanguage.ChineseSimplified => "zh-CN",
        AppLanguage.English => "en",
        _ => "system",
    };
}

/// <summary>Convenience accessor.</summary>
public static class L
{
    public static string T(string key) => Localizer.Instance.Get(key);
    public static string F(string key, params object?[] args) => Localizer.Instance.Format(key, args);
}
