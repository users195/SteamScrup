using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamScrup.Core;

public enum AppTheme { System, Light, Dark }

/// <summary>User preferences, persisted as JSON next to the log folder.</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string Language { get; set; } = "system";

    // The deletion mode is deliberately not a setting: the confirmation dialog asks for it
    // on every run (Move to Recycle Bin / Delete permanently), so a stored preference would
    // only be able to contradict what the user picked.

    // The userdata category is always scanned. It used to sit behind an advanced switch, but
    // that switch had to be re-toggled after every restart and hid the category otherwise.
    // The risk is now stated per cleanup, in the confirmation dialog, which is where the
    // decision is actually made.

    /// <summary>Null means "auto-detect".</summary>
    public string? SteamRootOverride { get; set; }

    public string? LogDirectory { get; set; }

    /// <summary>
    /// Sidebar width in device-independent pixels. Some category labels are long enough to
    /// be clipped at the default, so the user can drag the divider between the sidebar and
    /// the content area and the choice is remembered.
    /// </summary>
    public double SidebarWidth { get; set; } = DefaultSidebarWidth;

    public const double DefaultSidebarWidth = 268;
    public const double MinSidebarWidth = 190;
    public const double MaxSidebarWidth = 520;

    /// <summary>Clamps a width into the supported range, falling back to the default.</summary>
    public static double ClampSidebarWidth(double value) =>
        double.IsNaN(value) || value <= 0
            ? DefaultSidebarWidth
            : Math.Max(MinSidebarWidth, Math.Min(MaxSidebarWidth, value));

    public bool HasSeenRiskDisclaimer { get; set; }

    [JsonIgnore]
    public static string DefaultConfigPath => AppPaths.SettingsFile;

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultConfigPath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Corrupt settings must never block startup.
        }

        return new AppSettings();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultConfigPath;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // Read-only profile: keep running with in-memory settings.
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
