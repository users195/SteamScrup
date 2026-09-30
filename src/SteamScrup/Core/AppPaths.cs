namespace SteamScrup.Core;

/// <summary>
/// Resolves where the portable build keeps its settings and logs.
///
/// The portable copy prefers to keep everything next to the executable so the whole
/// folder can be moved or copied between machines with its configuration intact.
/// When that folder is not writable (Program Files, a read-only share, a locked-down
/// device) it silently falls back to %LOCALAPPDATA% instead of failing.
/// </summary>
public static class AppPaths
{
    private const string FolderName = "SteamScrup";

    private static readonly Lazy<string> _dataDirectory = new(ResolveDataDirectory, isThreadSafe: true);
    private static bool _usingPortableLocation;

    /// <summary>Directory that holds settings.json and the logs folder.</summary>
    public static string DataDirectory => _dataDirectory.Value;

    /// <summary>True when settings and logs live next to the executable.</summary>
    public static bool IsPortable => _usingPortableLocation;

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>Always the per-user location; used as the fallback and by the uninstaller.</summary>
    public static string RoamingFallbackDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

    /// <summary>Where the executable lives, so the user can be told what got written where.</summary>
    public static string ExecutableDirectory
    {
        get
        {
            try
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path))
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) return dir;
                }
            }
            catch
            {
                // Fall through to the AppContext value.
            }

            return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    private static string ResolveDataDirectory()
    {
        var appDir = ExecutableDirectory;

        // Do not litter the app's own folder when it happens to be a temp extraction
        // directory (single-file publishing) - that would silently lose the settings.
        var isTempLocation = appDir.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase);

        if (!isTempLocation && IsWritable(appDir))
        {
            _usingPortableLocation = true;
            return appDir;
        }

        _usingPortableLocation = false;
        return RoamingFallbackDirectory;
    }

    /// <summary>Checks writability by actually writing, which is the only reliable test.</summary>
    private static bool IsWritable(string directory)
    {
        var probe = Path.Combine(directory, ".steamscrup-write-test");
        try
        {
            if (!Directory.Exists(directory)) return false;
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { if (File.Exists(probe)) File.Delete(probe); } catch { }
        }
    }

    /// <summary>Human-readable description for the settings screen.</summary>
    public static string DescribeLocation() =>
        IsPortable ? DataDirectory : $"{DataDirectory} (fallback)";
}
