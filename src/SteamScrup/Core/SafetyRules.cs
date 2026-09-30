namespace SteamScrup.Core;

/// <summary>
/// Safety rules that keep the cleaner away from things it must never touch.
/// Every entry here was validated against a real Steam installation.
/// </summary>
public static class SafetyRules
{
    /// <summary>
    /// appids that are redistributables or runtimes rather than games. They carry a normal
    /// manifest, so counting them as "installed games" inflates the number and contradicts
    /// the cleanup list, where their folder is protected.
    ///
    /// Matched by appid rather than by name: the names are localised and inconsistent.
    /// </summary>
    private static readonly HashSet<int> ToolAppIds = new()
    {
        228980, // Steamworks Common Redistributables  (installdir "Steamworks Shared")
        228990, // Steamworks Common Redistributables  (alternate key)
        1070560, // Steam Linux Runtime
        1391110, // Steam Linux Runtime - Soldier
        1628350, // Steam Linux Runtime - Sniper
        2180100, // Proton Experimental
        1493710, // Proton Experimental (older key)
        961940, // Proton 7.0
        1070561, // Steam Linux Runtime (helper)
    };

    /// <summary>True for redistributables and runtimes that should not count as games.</summary>
    public static bool IsToolApp(int appId, string? name = null)
    {
        if (ToolAppIds.Contains(appId)) return true;

        // Belt and braces for redistributables that occasionally ship under new appids.
        if (name is null) return false;
        return name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("Proton ", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Folder names inside steamapps\common that belong to the Steam client itself
    /// rather than to a game. They are always protected even when no manifest exists.
    /// "Steam Controller Configs" holds per-account controller layout data.
    /// </summary>
    public static readonly HashSet<string> ProtectedCommonFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Steamworks Shared",
        "Steam Controller Configs",
        "screenshots",
        "sourcemods",
    };

    /// <summary>
    /// Directory names that mean "this holds saved data". When one of these appears
    /// anywhere inside a candidate folder the folder is demoted to Review and never
    /// pre-selected, because deleting it would destroy progress.
    /// </summary>
    public static readonly string[] SaveDataMarkers =
    {
        "save", "saves", "saved", "savegame", "savegames", "savegamebackup", "savedata", "savegame_data",
        "bkp", "backup", "profiles", "profile", "userdata", "remote", "win64_save", "slot",
    };

    /// <summary>
    /// Mods, overlays and injectors a user installed on purpose. Never pre-selected.
    /// </summary>
    public static readonly string[] UserModMarkers =
    {
        "reshade", "reshade-shaders", "dxgi.dll", "d3d11.dll", "dinput8.dll",
        "enbseries", "enb", "sweetfx", "mods", "mod", "addon", "addons",
        "install", "setup", "patch", "trainer", "cheat",
    };

    /// <summary>
    /// File-name fragments that look like a redistributable installer a user kept.
    /// </summary>
    public static readonly string[] InstallerFileMarkers =
    {
        ".exe", ".msi", ".bat", ".cmd", ".zip", ".7z", ".rar",
    };

    /// <summary>A file at or below this size is considered trivial for tiering purposes.</summary>
    public const long TrivialFileBytes = 64 * 1024;

    /// <summary>Folders at or above this size are never treated as Safe.</summary>
    public const long LargeFolderBytes = 100L * 1024 * 1024;

    public static bool IsProtectedCommonFolder(string folderName) =>
        ProtectedCommonFolderNames.Contains(folderName);

    public static bool LooksLikeSaveData(string name)
    {
        var lower = name.ToLowerInvariant();
        foreach (var marker in SaveDataMarkers)
            if (lower.Contains(marker, StringComparison.Ordinal)) return true;
        return false;
    }

    public static bool LooksLikeUserMod(string name)
    {
        var lower = name.ToLowerInvariant();
        foreach (var marker in UserModMarkers)
            if (lower == marker || lower.Contains(marker, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// Windows shader / pipeline caches that are rebuilt automatically.
    /// Whitelist only: game content directories are never matched by name alone,
    /// because names such as "materials\temp" are real game data.
    /// </summary>
    public static IReadOnlyList<string> SystemCachePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var paths = new List<string>();

        void Add(params string[] parts)
        {
            var p = parts.Length == 1 ? parts[0] : Path.Combine(parts);
            if (p.Length > 0) paths.Add(p);
        }

        if (!string.IsNullOrEmpty(local))
        {
            Add(local, "D3DSCache");
            Add(local, "NVIDIA", "DXCache");
            Add(local, "NVIDIA", "GLCache");
            Add(local, "NVIDIA", "ComputeCache");
            Add(local, "NVIDIA", "VkCache");
            Add(local, "AMD", "DxCache");
            Add(local, "AMD", "DxcCache");
            Add(local, "AMD", "VkCache");
            Add(local, "Intel", "ShaderCache");
            Add(local, "Microsoft", "DirectX Shader Cache");
        }

        if (!string.IsNullOrEmpty(appData))
        {
            Add(appData, "NVIDIA", "ComputeCache");
        }

        return paths;
    }

    /// <summary>
    /// Cache directory names recognised inside a game folder. Applied only during a
    /// bounded walk of a folder already known to be a leftover, never as a blind
    /// recursive match over game content.
    /// </summary>
    public static readonly HashSet<string> GameCacheFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "D3DSCache",
        "DXCache",
        "DxcCache",
        "GLCache",
        "VkCache",
        "NV_Cache",
        "ShaderCache",
        "shadercache",
        "CrashReport",
        "CrashReports",
        "CrashDumps",
        "CrashDump",
        "Crash Logs",
        "CrashLogs",
        "crashdumps",
        "minidumps",
        "MiniDumps",
        ".crashdata",
    };

    /// <summary>Steam's own userdata folders that are per-account state, not leftovers.</summary>
    public static readonly HashSet<string> ProtectedUserDataFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "config",
        "ugc",
        "ugcmsgcache",
        "inventorymsgcache",
        "gamerecordings",
    };
}
