using Microsoft.Win32;

namespace SteamScrup.Core;

/// <summary>
/// Resolves the theme, including "follow Windows", by reading the per-user
/// Personalize key. Raises an event when the user flips the system theme.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private AppTheme _theme = AppTheme.System;

    public event EventHandler? ThemeChanged;

    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The concrete theme after resolving "follow system".</summary>
    public bool IsDarkEffective => _theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => IsSystemDark(),
    };

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue("AppsUseLightTheme") is int light) return light == 0;
        }
        catch
        {
            // Assume light when the key is unavailable.
        }

        return false;
    }

    public void StartWatching()
    {
        try { SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; }
        catch { /* non-interactive session */ }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            if (_theme == AppTheme.System) ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        try { SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; }
        catch { }
    }
}
