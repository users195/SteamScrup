using System.Diagnostics;

namespace SteamScrup.Core;

/// <summary>Progress report emitted while scanning.</summary>
public sealed record ScanProgress(string StageId, int Done, int Total, string? CurrentItem);

/// <summary>
/// Enumerates every cleanup candidate from a Steam installation and the machine-wide
/// shader caches. Enumeration is cheap; directory sizes are measured in a separate,
/// cancellable pass so the UI can stay responsive.
/// </summary>
public sealed class Scanner
{
    private readonly SteamInstallation _installation;
    private readonly GameNameResolver? _names;

    public Scanner(SteamInstallation installation, GameNameResolver? names = null)
    {
        _installation = installation;
        _names = names;
    }

    public SteamInstallation Installation => _installation;

    /// <summary>Attaches a resolved name to every item that shows an appid as its title.</summary>
    public static void ApplyResolvedNames(IEnumerable<ScanItem> items, GameNameResolver resolver)
    {
        foreach (var item in items)
        {
            if (item.AppId is not { } appId) continue;
            item.GameName = resolver.TryGet(appId);
        }
    }

    /// <summary>Every appid the UI would like a name for, gathered from a scan result.</summary>
    public static IReadOnlyList<int> CollectAppIds(IEnumerable<ScanItem> items) =>
        items.Where(i => i.WantsGameNameBubble && i.AppId is > 0)
             .Select(i => i.AppId!.Value)
             .Distinct()
             .ToList();

    /// <summary>Fast pass: builds the candidate list without measuring sizes.</summary>
    public List<ScanItem> Enumerate(IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        var items = new List<ScanItem>();

        EnumerateCommonFolders(items, ct);
        EnumerateShaderCache(items);
        EnumerateWorkshop(items);
        EnumerateSteamCaches(items, ct);
        EnumerateSystemCaches(items);
        EnumerateGameCrashData(items, ct);
        EnumerateUserData(items, ct);

        progress?.Report(new ScanProgress("enum.done", items.Count, items.Count, null));
        return items;
    }

    // ---------------------------------------------------------------- common

    private void EnumerateCommonFolders(List<ScanItem> items, CancellationToken ct)
    {
        var allInstallDirs = _installation.AllInstallDirNames();

        foreach (var lib in _installation.Libraries)
        {
            ct.ThrowIfCancellationRequested();
            if (!lib.CommonExists) continue;

            string[] dirs;
            try { dirs = Directory.GetDirectories(lib.CommonPath); }
            catch { continue; }

            foreach (var dir in dirs)
            {
                ct.ThrowIfCancellationRequested();
                var folder = Path.GetFileName(dir);

                // Steam's own folders are protected even though they have no manifest.
                if (SafetyRules.IsProtectedCommonFolder(folder))
                {
                    items.Add(new ScanItem
                    {
                        Path = dir,
                        Category = ScanCategory.CommonFolder,
                        Confidence = Confidence.Protected,
                        ReasonId = "reason.protectedSteamFolder",
                        LibraryRoot = lib.RootPath,
                        LastWriteTime = SafeLastWrite(dir),
                    });
                    continue;
                }

                // A manifest anywhere in any library means it is an installed game.
                if (allInstallDirs.Contains(folder)) continue;

                var probe = ProbeDirectory(dir);
                items.Add(new ScanItem
                {
                    Path = dir,
                    Category = ScanCategory.CommonFolder,
                    Confidence = ClassifyLeftover(probe),
                    ReasonId = ReasonFor(probe),
                    Detail = probe.Note,
                    LibraryRoot = lib.RootPath,
                    SizeBytes = probe.TotalBytes,
                    FileCount = probe.FileCount,
                    LastWriteTime = SafeLastWrite(dir),
                });
            }
        }
    }

    private static Confidence ClassifyLeftover(DirectoryProbe p)
    {
        // Anything holding save data or a user-installed mod must never be pre-selected.
        if (p.HasSaveMarker || p.HasModMarker) return Confidence.Review;

        if (p.FileCount == 0) return Confidence.Safe;

        // A folder that is only a tiny stub (e.g. just steam_appid.txt) is safe.
        if (p.TotalBytes <= SafetyRules.TrivialFileBytes && p.ExecutableCount == 0)
            return Confidence.Safe;

        // Big and still contains executables: definitely needs a human.
        if (p.ExecutableCount > 0 || p.TotalBytes >= SafetyRules.LargeFolderBytes)
            return Confidence.Review;

        return Confidence.Likely;
    }

    private static string ReasonFor(DirectoryProbe p)
    {
        if (p.HasSaveMarker) return "reason.containsSaveData";
        if (p.HasModMarker) return "reason.containsMod";
        if (p.FileCount == 0) return "reason.emptyFolder";
        if (p.ExecutableCount > 0) return "reason.containsExecutable";
        if (p.TotalBytes <= SafetyRules.TrivialFileBytes) return "reason.stubOnly";
        if (p.TotalBytes >= SafetyRules.LargeFolderBytes) return "reason.largeLeftover";
        return "reason.noManifest";
    }

    private sealed class DirectoryProbe
    {
        public long TotalBytes;
        public int FileCount;
        public int ExecutableCount;
        public bool HasSaveMarker;
        public bool HasModMarker;
        public string? Note;
    }

    /// <summary>
    /// Bounded inspection of one candidate folder: total size, file count, executable
    /// count and whether anything looks like saves or mods.
    /// </summary>
    private static DirectoryProbe ProbeDirectory(string path)
    {
        var probe = new DirectoryProbe();

        // Top level entries are enough to spot saves/mods and are cheap.
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                var name = Path.GetFileName(entry);
                if (SafetyRules.LooksLikeSaveData(name))
                {
                    probe.HasSaveMarker = true;
                    probe.Note ??= name;
                }

                if (SafetyRules.LooksLikeUserMod(name))
                {
                    probe.HasModMarker = true;
                    probe.Note ??= name;
                }
            }
        }
        catch { /* denied: treated as unknown, classification stays conservative */ }

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                probe.FileCount++;
                try
                {
                    var fi = new FileInfo(file);
                    probe.TotalBytes += fi.Length;
                    if (fi.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                        probe.ExecutableCount++;
                }
                catch { }
            }
        }
        catch { }

        return probe;
    }

    // ---------------------------------------------------------- shadercache

    private void EnumerateShaderCache(List<ScanItem> items)
    {
        foreach (var lib in _installation.Libraries)
        {
            if (!Directory.Exists(lib.ShaderCachePath)) continue;

            string[] dirs;
            try { dirs = Directory.GetDirectories(lib.ShaderCachePath); }
            catch { continue; }

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);
                if (!int.TryParse(name, out var appId)) continue;

                var manifest = _installation.AllManifests.FirstOrDefault(m => m.AppId == appId);
                var orphan = manifest is null;

                items.Add(new ScanItem
                {
                    Path = dir,
                    Category = ScanCategory.ShaderCache,
                    Confidence = orphan ? Confidence.Safe : Confidence.Likely,
                    ReasonId = orphan ? "reason.orphanShaderCache" : "reason.installedShaderCache",
                    Detail = manifest?.Name,
                    AppId = appId,
                    GameName = manifest?.Name ?? _names?.TryGet(appId),
                    LibraryRoot = lib.RootPath,
                    RegeneratesAutomatically = true,
                    LastWriteTime = SafeLastWrite(dir),
                });
            }
        }
    }

    // ------------------------------------------------------------ workshop

    private void EnumerateWorkshop(List<ScanItem> items)
    {
        foreach (var lib in _installation.Libraries)
        {
            if (!Directory.Exists(lib.WorkshopPath)) continue;

            // workshop\content\<appid> — orphaned when the game is gone.
            var contentPath = Path.Combine(lib.WorkshopPath, "content");
            if (Directory.Exists(contentPath))
            {
                string[] dirs;
                try { dirs = Directory.GetDirectories(contentPath); }
                catch { dirs = Array.Empty<string>(); }

                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir);
                    if (!int.TryParse(name, out var appId)) continue;

                    var manifest = _installation.AllManifests.FirstOrDefault(m => m.AppId == appId);
                    var orphan = manifest is null;

                    items.Add(new ScanItem
                    {
                        Path = dir,
                        Category = ScanCategory.WorkshopCache,
                        Confidence = orphan ? Confidence.Safe : Confidence.Likely,
                        ReasonId = orphan ? "reason.orphanWorkshop" : "reason.installedWorkshop",
                        Detail = manifest?.Name,
                        AppId = appId,
                        GameName = manifest?.Name ?? _names?.TryGet(appId),
                        LibraryRoot = lib.RootPath,
                        LastWriteTime = SafeLastWrite(dir),
                    });
                }
            }

            // workshop\temp and workshop\downloads are scratch space.
            foreach (var scratch in new[] { "temp", "downloads" })
            {
                var path = Path.Combine(lib.WorkshopPath, scratch);
                if (!Directory.Exists(path)) continue;
                if (!HasAnyEntry(path)) continue;

                items.Add(new ScanItem
                {
                    Path = path,
                    Category = ScanCategory.WorkshopCache,
                    Confidence = Confidence.Safe,
                    ReasonId = "reason.workshopScratch",
                    LibraryRoot = lib.RootPath,
                    RegeneratesAutomatically = true,
                    LastWriteTime = SafeLastWrite(path),
                });
            }

            // Orphaned appworkshop_<appid>.acf metadata (never an appmanifest).
            string[] acfFiles;
            try { acfFiles = Directory.GetFiles(lib.WorkshopPath, "appworkshop_*.acf"); }
            catch { acfFiles = Array.Empty<string>(); }

            foreach (var file in acfFiles)
            {
                var fileName = Path.GetFileName(file);
                var digits = new string(fileName.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                if (!int.TryParse(digits, out var appId)) continue;

                if (_installation.AllManifests.Any(m => m.AppId == appId)) continue;
                if (Directory.Exists(Path.Combine(contentPath, appId.ToString()))) continue;

                items.Add(new ScanItem
                {
                    Path = file,
                    Category = ScanCategory.WorkshopCache,
                    Confidence = Confidence.Safe,
                    Kind = ItemKind.File,
                    ReasonId = "reason.orphanWorkshopMeta",
                    AppId = appId,
                    LibraryRoot = lib.RootPath,
                    LastWriteTime = SafeLastWrite(file),
                });
            }
        }
    }

    // -------------------------------------------------------- steam scratch

    private void EnumerateSteamCaches(List<ScanItem> items, CancellationToken ct)
    {
        foreach (var lib in _installation.Libraries)
        {
            ct.ThrowIfCancellationRequested();

            // "corrupt" holds damaged download chunks; Steam re-downloads on demand.
            var corrupt = Path.Combine(lib.SteamAppsPath, "corrupt");
            AddScratch(items, corrupt, ScanCategory.SteamCache, "reason.corruptChunks", lib.RootPath);

            var temp = Path.Combine(lib.SteamAppsPath, "temp");
            AddScratch(items, temp, ScanCategory.SteamCache, "reason.steamTemp", lib.RootPath);

            var downloading = Path.Combine(lib.SteamAppsPath, "downloading");
            AddScratch(items, downloading, ScanCategory.SteamCache, "reason.steamDownloading", lib.RootPath);

            // Stale appmanifest update artefacts: appmanifest_<id>.acf.<n>.tmp
            string[] tmpFiles;
            try { tmpFiles = Directory.GetFiles(lib.SteamAppsPath, "appmanifest_*.acf.*.tmp"); }
            catch { tmpFiles = Array.Empty<string>(); }

            foreach (var file in tmpFiles)
            {
                items.Add(new ScanItem
                {
                    Path = file,
                    Category = ScanCategory.SteamCache,
                    Confidence = Confidence.Safe,
                    Kind = ItemKind.File,
                    ReasonId = "reason.tempManifest",
                    LibraryRoot = lib.RootPath,
                    LastWriteTime = SafeLastWrite(file),
                });
            }
        }
    }

    private static void AddScratch(List<ScanItem> items, string path, ScanCategory category,
        string reasonId, string libraryRoot)
    {
        if (!Directory.Exists(path)) return;
        if (!HasAnyEntry(path)) return;

        items.Add(new ScanItem
        {
            Path = path,
            Category = category,
            Confidence = Confidence.Safe,
            ReasonId = reasonId,
            LibraryRoot = libraryRoot,
            RegeneratesAutomatically = true,
            LastWriteTime = SafeLastWrite(path),
        });
    }

    // ------------------------------------------------------- system caches

    private void EnumerateSystemCaches(List<ScanItem> items)
    {
        foreach (var path in SafetyRules.SystemCachePaths())
        {
            if (!Directory.Exists(path)) continue;
            if (!HasAnyEntry(path)) continue;

            var isNvidiaOrAmd = path.Contains(@"\NVIDIA\", StringComparison.OrdinalIgnoreCase) ||
                                path.Contains(@"\AMD\", StringComparison.OrdinalIgnoreCase);

            items.Add(new ScanItem
            {
                Path = path,
                Category = ScanCategory.DirectXCache,
                Confidence = Confidence.Safe,
                ReasonId = isNvidiaOrAmd ? "reason.gpuShaderCache" : "reason.directXShaderCache",
                RegeneratesAutomatically = true,
                LastWriteTime = SafeLastWrite(path),
            });
        }
    }

    // --------------------------------------------------------- crash dumps

    private void EnumerateGameCrashData(List<ScanItem> items, CancellationToken ct)
    {
        foreach (var lib in _installation.Libraries)
        {
            if (!lib.CommonExists) continue;

            string[] gameDirs;
            try { gameDirs = Directory.GetDirectories(lib.CommonPath); }
            catch { continue; }

            foreach (var gameDir in gameDirs)
            {
                ct.ThrowIfCancellationRequested();

                List<string> cacheDirs;
                try
                {
                    cacheDirs = Directory.EnumerateDirectories(gameDir, "*", SearchOption.AllDirectories)
                        .Where(d => SafetyRules.GameCacheFolderNames.Contains(Path.GetFileName(d)))
                        .ToList();
                }
                catch { continue; }

                // Keep the shallowest matches: a nested cache inside a crash folder
                // would otherwise be listed twice.
                var filtered = cacheDirs
                    .Where(d => !cacheDirs.Any(other =>
                        !ReferenceEquals(other, d) &&
                        other.Length < d.Length &&
                        d.StartsWith(other + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var dir in filtered)
                {
                    if (!HasAnyEntry(dir)) continue;

                    items.Add(new ScanItem
                    {
                        Path = dir,
                        Category = ScanCategory.CrashDumps,
                        Confidence = Confidence.Safe,
                        ReasonId = "reason.crashData",
                        LibraryRoot = lib.RootPath,
                        LastWriteTime = SafeLastWrite(dir),
                    });
                }
            }
        }
    }

    // ------------------------------------------------------------ userdata

    private void EnumerateUserData(List<ScanItem> items, CancellationToken ct)
    {
        var userData = _installation.UserDataPath;
        if (!Directory.Exists(userData)) return;

        string[] accounts;
        try { accounts = Directory.GetDirectories(userData); }
        catch { return; }

        foreach (var account in accounts)
        {
            ct.ThrowIfCancellationRequested();
            var accountId = Path.GetFileName(account);

            string[] children;
            try { children = Directory.GetDirectories(account); }
            catch { continue; }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);

                // config/ugc/ugcmsgcache/... are live per-account state, never leftovers.
                if (SafetyRules.ProtectedUserDataFolderNames.Contains(name)) continue;
                if (!int.TryParse(name, out var appId)) continue;

                var manifest = _installation.AllManifests.FirstOrDefault(m => m.AppId == appId);
                var orphan = manifest is null;

                var probe = ProbeDirectory(child);
                var hasSaves = probe.FileCount > 0;

                items.Add(new ScanItem
                {
                    Path = child,
                    Category = ScanCategory.UserData,
                    Confidence = !orphan
                        ? Confidence.Protected
                        : hasSaves
                            ? Confidence.Review
                            : Confidence.Safe,
                    ReasonId = !orphan
                        ? "reason.activeUserData"
                        : hasSaves
                            ? "reason.orphanUserDataWithSaves"
                            : "reason.orphanUserDataEmpty",
                    Detail = manifest?.Name ?? accountId,
                    AppId = appId,
                    GameName = manifest?.Name ?? _names?.TryGet(appId),
                    SizeBytes = probe.TotalBytes,
                    FileCount = probe.FileCount,
                    LastWriteTime = SafeLastWrite(child),
                });
            }
        }
    }

    // --------------------------------------------------------------- sizes

    /// <summary>
    /// Measures size / file count / last-write time for items that do not have them yet.
    /// Cancellable; results are written straight onto the items so the UI can update live.
    /// </summary>
    public void MeasureSizes(IReadOnlyList<ScanItem> items, IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var pending = items.Where(i => i.SizeBytes == 0 && i.Kind == ItemKind.Directory).ToList();
        var sw = Stopwatch.StartNew();
        var done = 0;

        foreach (var item in pending)
        {
            ct.ThrowIfCancellationRequested();
            var (bytes, files) = MeasureDirectory(item.Path);
            item.SizeBytes = bytes;
            item.FileCount = files;
            item.LastWriteTime ??= SafeLastWrite(item.Path);

            done++;
            if (done % 4 == 0 || done == pending.Count)
                progress?.Report(new ScanProgress("measure", done, pending.Count, item.FileName));
        }

        progress?.Report(new ScanProgress("measure.done", done, pending.Count, null));
    }

    public static (long Bytes, int Files) MeasureDirectory(string path)
    {
        long bytes = 0;
        var files = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                files++;
                try { bytes += new FileInfo(file).Length; }
                catch { }
            }
        }
        catch { }

        return (bytes, files);
    }

    public static long MeasureFile(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    // -------------------------------------------------------------- helpers

    private static bool HasAnyEntry(string path)
    {
        try
        {
            using var e = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            return e.MoveNext();
        }
        catch { return false; }
    }

    private static DateTimeOffset? SafeLastWrite(string path)
    {
        try { return new DirectoryInfo(path).LastWriteTimeUtc; }
        catch { return null; }
    }
}
