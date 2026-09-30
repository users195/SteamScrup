namespace SteamScrup.Core;

/// <summary>One installed application as described by an appmanifest_&lt;appid&gt;.acf file.</summary>
public sealed class ManifestInfo
{
    public required int AppId { get; init; }
    public required string ManifestPath { get; init; }
    public required string ManifestFileName { get; init; }
    public string? Name { get; init; }
    public string? InstallDir { get; init; }
    public int? StateFlags { get; init; }
    public long? SizeOnDisk { get; init; }
    public long? LastUpdatedUnix { get; init; }

    /// <summary>StateFlags bit 2 (value 4) means "fully installed".</summary>
    public bool IsFullyInstalled => StateFlags is null || (StateFlags.Value & 4) != 0;

    public DateTimeOffset? LastUpdated =>
        LastUpdatedUnix is > 0 ? DateTimeOffset.FromUnixTimeSeconds(LastUpdatedUnix.Value) : null;
}

/// <summary>One Steam library folder (the parent of a "steamapps" directory).</summary>
public sealed class SteamLibrary
{
    public required string RootPath { get; init; }
    public required string SteamAppsPath { get; init; }
    public string? Label { get; init; }
    public long? TotalSizeBytes { get; init; }
    public List<ManifestInfo> Manifests { get; } = new();

    public string CommonPath => Path.Combine(SteamAppsPath, "common");
    public string WorkshopPath => Path.Combine(SteamAppsPath, "workshop");
    public string ShaderCachePath => Path.Combine(SteamAppsPath, "shadercache");

    public bool CommonExists => Directory.Exists(CommonPath);

    public ManifestInfo? FindByAppId(int appId) => Manifests.FirstOrDefault(m => m.AppId == appId);

    /// <summary>Case-insensitive set of every installdir known in this library.</summary>
    public HashSet<string> InstallDirNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in Manifests)
            if (!string.IsNullOrWhiteSpace(m.InstallDir)) set.Add(m.InstallDir!);
        return set;
    }
}

/// <summary>The detected Steam installation and every library it knows about.</summary>
public sealed class SteamInstallation
{
    public required string SteamRoot { get; init; }
    public required string SteamAppsPath { get; init; }
    public string? LibraryFoldersVdfPath { get; init; }
    public List<SteamLibrary> Libraries { get; } = new();

    public string UserDataPath => Path.Combine(SteamRoot, "userdata");

    public IEnumerable<ManifestInfo> AllManifests => Libraries.SelectMany(l => l.Manifests);

    /// <summary>
    /// Every installdir across every library, keyed case-insensitively so a folder in one
    /// library is never reported as a leftover just because its manifest lives elsewhere.
    /// </summary>
    public HashSet<string> AllInstallDirNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lib in Libraries)
            foreach (var name in lib.InstallDirNames())
                set.Add(name);
        return set;
    }
}
