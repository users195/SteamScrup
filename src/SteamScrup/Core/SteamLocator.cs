using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SteamScrup.Core;

/// <summary>
/// Locates the Steam installation and every library folder without asking the user,
/// reading only local configuration (registry + libraryfolders.vdf). No network access.
/// </summary>
public static class SteamLocator
{
    private const string SteamRegistryKey = @"Software\Valve\Steam";

    /// <summary>Matches exactly appmanifest_&lt;digits&gt;.acf; temp files such as
    /// "appmanifest_10.acf.3628266204.tmp" are deliberately excluded.</summary>
    private static readonly Regex ManifestRegex =
        new(@"^appmanifest_(\d+)\.acf$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Finds the Steam root. Returns null when Steam cannot be located.</summary>
    public static string? FindSteamRoot()
    {
        foreach (var candidate in RegistryCandidates())
            if (IsSteamRoot(candidate)) return Normalize(candidate);

        foreach (var candidate in WellKnownCandidates())
            if (IsSteamRoot(candidate)) return Normalize(candidate);

        return null;
    }

    private static IEnumerable<string> RegistryCandidates()
    {
        // SteamPath is written as e.g. "d:/steam" (forward slashes).
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            string? steamPath = null, installPath = null;
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                using var key = baseKey.OpenSubKey(SteamRegistryKey);
                steamPath = key?.GetValue("SteamPath") as string;
            }
            catch { /* registry unavailable: fall through to other candidates */ }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(@"SOFTWARE\Valve\Steam");
                installPath = key?.GetValue("InstallPath") as string;
            }
            catch { /* ignore */ }

            if (!string.IsNullOrWhiteSpace(steamPath)) yield return steamPath!;
            if (!string.IsNullOrWhiteSpace(installPath)) yield return installPath!;
        }
    }

    private static IEnumerable<string> WellKnownCandidates()
    {
        foreach (var pf in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 })
        {
            if (!string.IsNullOrEmpty(pf)) yield return Path.Combine(pf, "Steam");
        }

        foreach (var drive in DriveLetters())
        {
            yield return $@"{drive}:\Steam";
            yield return $@"{drive}:\SteamLibrary";
            yield return $@"{drive}:\Games\Steam";
        }
    }

    private static IEnumerable<string> DriveLetters()
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch { yield break; }

        foreach (var d in drives)
        {
            string name;
            try
            {
                if (d.DriveType != DriveType.Fixed) continue;
                name = d.Name;
            }
            catch { continue; }

            if (name.Length >= 1 && char.IsLetter(name[0]))
                yield return char.ToUpperInvariant(name[0]).ToString();
        }
    }

    private static bool IsSteamRoot(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) &&
        (File.Exists(Path.Combine(path, "steam.exe")) ||
         File.Exists(Path.Combine(path, "steamapps", "libraryfolders.vdf")));

    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path.Replace('/', '\\').Trim());
        return full.Length > 3 ? full.TrimEnd('\\') : full;
    }

    /// <summary>Builds the full picture: Steam root, libraries, and manifests.</summary>
    public static SteamInstallation? Discover(string? explicitSteamRoot = null)
    {
        var root = explicitSteamRoot is not null ? Normalize(explicitSteamRoot) : FindSteamRoot();
        if (root is null) return null;

        var steamApps = Path.Combine(root, "steamapps");

        var vdfPath = FirstExisting(
            Path.Combine(steamApps, "libraryfolders.vdf"),
            Path.Combine(root, "config", "libraryfolders.vdf"));

        var installation = new SteamInstallation
        {
            SteamRoot = root,
            SteamAppsPath = steamApps,
            LibraryFoldersVdfPath = vdfPath,
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The library that holds Steam itself is always present.
        AddLibrary(installation, root, seen);

        if (vdfPath is not null)
        {
            foreach (var libRoot in ParseLibraryPaths(vdfPath))
                AddLibrary(installation, libRoot, seen);
        }

        foreach (var lib in installation.Libraries)
            lib.Manifests.AddRange(ReadManifests(lib.SteamAppsPath));

        return installation;
    }

    private static void AddLibrary(SteamInstallation installation, string libraryRoot, HashSet<string> seen)
    {
        var normalized = Normalize(libraryRoot);

        // Windows paths are case-insensitive: "D:\steam" and "D:\Steam" are the same
        // library and must not be listed (or scanned) twice.
        if (!seen.Add(normalized)) return;
        if (!Directory.Exists(normalized)) return;

        var steamApps = Path.Combine(normalized, "steamapps");
        if (!Directory.Exists(steamApps)) return;

        installation.Libraries.Add(new SteamLibrary
        {
            RootPath = normalized,
            SteamAppsPath = steamApps,
        });
    }

    /// <summary>Reads every "path" value out of libraryfolders.vdf (modern and legacy layouts).</summary>
    public static IReadOnlyList<string> ParseLibraryPaths(string vdfPath)
    {
        var paths = new List<string>();
        VdfNode root;
        try { root = VdfParser.ParseFile(vdfPath); }
        catch { return paths; }

        var container = root.GetChild("libraryfolders") ?? root;

        foreach (var key in container.Keys)
        {
            var child = container.GetChild(key);
            if (child is not null)
            {
                var path = child.GetString("path");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path!);
                    continue;
                }

                // Legacy format: "1" "D:\\SteamLibrary"
                var direct = child.GetString("0") ?? child.GetString("1");
                if (!string.IsNullOrWhiteSpace(direct)) paths.Add(direct!);
                continue;
            }

            var value = container.GetString(key);
            if (string.IsNullOrWhiteSpace(value)) continue;

            // Legacy numeric keys map straight to a library path.
            if (int.TryParse(key, out _)) paths.Add(value!);
        }

        // Also accept root-level "path" entries in case the file is unwrapped.
        if (paths.Count == 0)
        {
            foreach (var node in root.Descendants())
            {
                var p = node.GetString("path");
                if (!string.IsNullOrWhiteSpace(p)) paths.Add(p!);
            }
        }

        return paths;
    }

    /// <summary>Reads every appmanifest_&lt;appid&gt;.acf in a steamapps folder.</summary>
    public static IEnumerable<ManifestInfo> ReadManifests(string steamAppsPath)
    {
        if (!Directory.Exists(steamAppsPath)) yield break;

        string[] files;
        try { files = Directory.GetFiles(steamAppsPath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly); }
        catch { yield break; }

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var match = ManifestRegex.Match(fileName);
            if (!match.Success) continue; // skips .acf.<id>.tmp update artefacts

            if (!int.TryParse(match.Groups[1].Value, out var appId)) continue;

            var info = TryParseManifest(file, fileName, appId);
            if (info is not null) yield return info;
        }
    }

    private static ManifestInfo? TryParseManifest(string path, string fileName, int appId)
    {
        VdfNode root;
        try { root = VdfParser.ParseFile(path); }
        catch { return null; }

        var state = root.GetChild("AppState") ?? root;

        // Prefer the appid inside the file when present; the filename is the fallback.
        var appIdValue = state.GetInt("appid") ?? appId;

        return new ManifestInfo
        {
            AppId = appIdValue,
            ManifestPath = path,
            ManifestFileName = fileName,
            Name = state.GetString("name"),
            InstallDir = state.GetString("installdir"),
            StateFlags = state.GetInt("StateFlags"),
            SizeOnDisk = state.GetLong("SizeOnDisk"),
            LastUpdatedUnix = state.GetLong("LastUpdated"),
        };
    }

    public static string? FirstExisting(params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) return c;
        return null;
    }

    /// <summary>True while a Steam client process is running.</summary>
    public static bool IsSteamRunning()
    {
        try
        {
            return Process.GetProcessesByName("steam").Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
