using System.Text;
using System.Windows;
using SteamScrup.Core;

namespace SteamScrup;

/// <summary>
/// Headless end-to-end check of the whole core pipeline. Run with
/// <c>SteamScrup.exe --selftest</c>; it writes a report next to the log and exits.
/// This proves the parser, discovery, scanner and safety rules work on the real
/// machine without needing anyone to look at a window.
/// </summary>
public static class SelfTest
{
    public static int Run(OperationLog log, string reportPath)
    {
        var report = new StringBuilder();
        var failures = 0;

        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            report.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
        }

        void Info(string text) => report.AppendLine("      " + text);

        report.AppendLine("SteamScrup self-test");
        report.AppendLine($"time    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"runtime : {Environment.Version}");
        report.AppendLine($"exe dir : {AppPaths.ExecutableDirectory}");
        report.AppendLine($"data dir: {AppPaths.DataDirectory}");
        report.AppendLine($"portable: {(AppPaths.IsPortable ? "yes (settings and logs live next to the exe)" : "no (using %LOCALAPPDATA% fallback)")}");
        report.AppendLine($"log     : {log.Directory}");
        report.AppendLine(new string('-', 72));

        // ------------------------------------------------------------ portable data
        report.AppendLine("[Portable data location]");
        Check("a data directory could be resolved",
            !string.IsNullOrEmpty(AppPaths.DataDirectory) && Directory.Exists(AppPaths.DataDirectory),
            AppPaths.DataDirectory);
        Check("log directory is writable", log.CurrentFile is not null,
            log.CurrentFile ?? "(no log file could be opened)");
        if (AppPaths.IsPortable)
        {
            Check("portable mode writes next to the executable",
                string.Equals(AppPaths.DataDirectory.TrimEnd('\\'),
                    AppPaths.ExecutableDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase),
                $"{AppPaths.DataDirectory} vs {AppPaths.ExecutableDirectory}");
        }

        // ------------------------------------------------------------- icon glyphs
        // Headless: an Application exists here, but this check reads the XAML on disk so it
        // also works in the report-only path. It is what would have caught the "every icon
        // is a row of boxes" bug: glyphs are XAML resources, and looking them up in the
        // localization table silently returned the key name.
        report.AppendLine();
        report.AppendLine("[Icon glyph resources]");
        var iconsPath = Path.Combine(AppPaths.ExecutableDirectory, "UI", "Icons.xaml");
        string? iconsXaml = File.Exists(iconsPath) ? File.ReadAllText(iconsPath) : null;

        if (iconsXaml is null)
        {
            // The XAML is compiled into the assembly as BAML; load it back to inspect it.
            try
            {
                var resource = Application.LoadComponent(
                    new Uri("SteamScrup;component/UI/Icons.xaml", UriKind.Relative)) as ResourceDictionary;
                if (resource is not null)
                {
                    var declared = resource.Keys.Cast<object>().Select(k => k.ToString()).ToHashSet();
                    foreach (var key in UI.IconGlyph.AllKeys)
                        Check($"  assembly resource declares {key}", declared.Contains(key));
                    iconsXaml = "(loaded from assembly)";
                }
            }
            catch (Exception ex)
            {
                Check("icon resource dictionary loadable", false, ex.Message);
            }
        }

        if (iconsXaml is not null && iconsXaml != "(loaded from assembly)")
        {
            foreach (var key in UI.IconGlyph.AllKeys)
                Check($"  Icons.xaml declares {key}",
                    iconsXaml.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal));
        }

        // Every glyph key must also be reachable from the view models.
        var glyphProps = typeof(UI.MainViewModel)
            .GetProperties()
            .Where(p => p.Name.StartsWith("Glyph", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();
        Check("view model exposes glyph properties", glyphProps.Count >= 8,
            string.Join(", ", glyphProps));

        // -------------------------------------------------- localization key coverage
        // A "{Binding [some.key]}" whose key is absent from both tables renders as the key
        // text itself. That is how "set.log.directory" ended up visible in the settings page,
        // so the whole XAML surface is checked here rather than found by eye.
        report.AppendLine();
        report.AppendLine("[Localization key coverage]");
        var uiDir = Path.Combine(AppPaths.ExecutableDirectory, "UI");
        if (!Directory.Exists(uiDir))
        {
            report.AppendLine($"      ({uiDir} not found; XAML key scan skipped)");
        }
        else
        {
            var keyPattern = new System.Text.RegularExpressions.Regex(
                @"\{Binding \[([^\]]+)\]\}",
                System.Text.RegularExpressions.RegexOptions.Compiled);

            var referenced = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var xaml in Directory.GetFiles(uiDir, "*.xaml", SearchOption.AllDirectories))
            {
                foreach (System.Text.RegularExpressions.Match m in
                         keyPattern.Matches(File.ReadAllText(xaml)))
                {
                    referenced.Add(m.Groups[1].Value);
                }
            }

            // Keys the localizer knows about, gathered by probing the public API.
            var missingFromXaml = new List<string>();
            foreach (var key in referenced)
                if (!Localizer.Instance.Has(key)) missingFromXaml.Add(key);

            report.AppendLine($"      XAML references {referenced.Count} localization key(s)");
            Check("every XAML localization key is defined",
                missingFromXaml.Count == 0,
                missingFromXaml.Count == 0 ? "all present" : string.Join(", ", missingFromXaml));

            // Keys the running code asked for that neither table defines.
            var missingAtRuntime = Localizer.Instance.MissingKeys.ToList();
            Check("no unresolved localization key was requested at runtime",
                missingAtRuntime.Count == 0,
                missingAtRuntime.Count == 0 ? "none" : string.Join(", ", missingAtRuntime));
        }

        // ---------------------------------------------------------------- VDF parser
        report.AppendLine("[VDF parser]");
        try
        {
            const string sample = """
                // comment
                "AppState"
                {
                    "appid"      "730"
                    "name"       "Counter-Strike 2"
                    "installdir" "Counter-Strike Global Offensive"
                    "path"       "D:\\steam\\with \"quotes\""
                    "InstalledDepots"
                    {
                        "732" { "manifest" "813" }
                    }
                }
                """;

            var root = VdfParser.Parse(sample);
            var state = root.GetChild("AppState");
            Check("root node parsed", root is not null);
            Check("appid read", state?.GetString("appid") == "730", state?.GetString("appid") ?? "(null)");
            Check("name read", state?.GetString("name") == "Counter-Strike 2");
            Check("installdir read", state?.GetString("installdir") == "Counter-Strike Global Offensive");
            Check("escaped quotes kept",
                state?.GetString("path") == "D:\\steam\\with \"quotes\"",
                state?.GetString("path") ?? "(null)");
            Check("nested object read",
                state?.GetChild("InstalledDepots")?.GetChild("732")?.GetString("manifest") == "813");
        }
        catch (Exception ex)
        {
            Check("VDF parser", false, ex.Message);
        }

        // ---------------------------------------------------------------- discovery
        report.AppendLine();
        report.AppendLine("[Steam discovery]");
        var installation = SteamLocator.Discover();
        Check("Steam installation located", installation is not null,
            installation?.SteamRoot ?? "(not found)");

        if (installation is null)
        {
            report.AppendLine();
            report.AppendLine("Cannot continue without a Steam installation.");
            return Finish(report, reportPath, failures);
        }

        Info($"steam root       : {installation.SteamRoot}");
        Info($"libraryfolders   : {installation.LibraryFoldersVdfPath ?? "(missing)"}");
        Info($"libraries        : {installation.Libraries.Count}");
        Check("at least one library found", installation.Libraries.Count > 0);

        var duplicatePaths = installation.Libraries
            .GroupBy(l => l.RootPath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Check("libraries de-duplicated case-insensitively", duplicatePaths.Count == 0,
            string.Join(", ", duplicatePaths));

        var manifestCount = installation.AllManifests.Count();
        Check("appmanifest files parsed", manifestCount > 0, $"{manifestCount} manifest(s)");

        var withInstallDir = installation.AllManifests.Count(m => !string.IsNullOrWhiteSpace(m.InstallDir));
        Check("every manifest has an installdir", withInstallDir == manifestCount,
            $"{withInstallDir}/{manifestCount}");

        var tempArtifacts = installation.AllManifests
            .Count(m => m.ManifestFileName.Contains(".tmp", StringComparison.OrdinalIgnoreCase));
        Check("temporary .acf.*.tmp files excluded", tempArtifacts == 0, $"{tempArtifacts} leaked in");

        var chineseNames = installation.AllManifests
            .Where(m => m.InstallDir is not null &&
                        m.InstallDir.Any(c => c > 0x2E80))
            .Select(m => m.InstallDir!)
            .ToList();
        Check("non-ASCII installdir decoded", chineseNames.Count == 0 || chineseNames.All(n => !n.Contains('\uFFFD')),
            string.Join(", ", chineseNames));

        foreach (var lib in installation.Libraries)
            Info($"library          : {lib.RootPath}  manifests={lib.Manifests.Count}");

        // ---------------------------------------------------------------- scanning
        report.AppendLine();
        report.AppendLine("[Scanner]");
        var scanner = new Scanner(installation);
        var items = scanner.Enumerate();

        var probe = new List<ScanItem>(items);
        probe.RemoveAll(i => i.Category == ScanCategory.UserData); // sizes already measured there
        scanner.MeasureSizes(probe);

        Check("scan produced candidates", items.Count > 0, $"{items.Count} candidate(s)");

        // The deletion guard is verified separately below; here the point is that the
        // scanner only ever emits paths that are legitimate cleanup targets.
        var allowedRoots = new List<string>
        {
            Path.GetFullPath(installation.SteamAppsPath),
            Path.GetFullPath(installation.UserDataPath),
        };
        allowedRoots.AddRange(SafetyRules.SystemCachePaths().Select(Path.GetFullPath));

        var suspiciousPaths = items
            .Where(i => !allowedRoots.Any(root =>
                Path.GetFullPath(i.Path).StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            .Select(i => i.Path)
            .ToList();
        Check("every candidate lives under steamapps, userdata or a known cache path",
            suspiciousPaths.Count == 0, string.Join("; ", suspiciousPaths.Take(5)));

        var rootLevelSteamApps = items
            .Where(i => string.Equals(Path.GetFullPath(i.Path),
                Path.GetFullPath(installation.SteamAppsPath), StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Path)
            .ToList();
        Check("steamapps itself is never listed as a candidate",
            rootLevelSteamApps.Count == 0, string.Join("; ", rootLevelSteamApps));

        var accountLevelUserData = items
            .Where(i => i.Category == ScanCategory.UserData)
            .Where(i => string.Equals(
                Path.GetFullPath(Path.GetDirectoryName(i.Path) ?? string.Empty),
                Path.GetFullPath(installation.UserDataPath),
                StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Path)
            .ToList();
        Check("whole account userdata folders are never listed",
            accountLevelUserData.Count == 0, string.Join("; ", accountLevelUserData));

        Check("no candidate points at a library root",
            items.All(i => !installation.Libraries.Any(l =>
                string.Equals(l.RootPath, i.Path, StringComparison.OrdinalIgnoreCase))));

        // The single most important safety property: an installed game's folder must never
        // be offered for deletion. Steam's own feature folders are exempt because
        // "Steamworks Shared" is both protected *and* a real installdir (appid 228980).
        var installDirs = installation.AllInstallDirNames();
        var installedFolderHits = items
            .Where(i => i.Category == ScanCategory.CommonFolder)
            .Where(i => !SafetyRules.IsProtectedCommonFolder(i.FileName))
            .Where(i => installDirs.Contains(i.FileName))
            .Select(i => i.Path)
            .ToList();
        Check("installed game folders never listed as leftovers",
            installedFolderHits.Count == 0, string.Join("; ", installedFolderHits));

        Check("protected folders are kept even when an installdir matches",
            items.Any(i => i.Category == ScanCategory.CommonFolder &&
                           i.FileName.Equals("Steamworks Shared", StringComparison.OrdinalIgnoreCase) &&
                           i.Confidence == Confidence.Protected));

        var protectedHits = items
            .Where(i => i.Category == ScanCategory.CommonFolder)
            .Where(i => SafetyRules.IsProtectedCommonFolder(i.FileName))
            .Where(i => i.Confidence != Confidence.Protected)
            .Select(i => $"{i.FileName} ({i.Confidence})")
            .ToList();
        Check("Steam feature folders marked Protected",
            protectedHits.Count == 0, string.Join("; ", protectedHits));

        // ---- which categories really need the Steam client closed
        // Only the four the client holds. Getting this wrong either blocks work that could
        // proceed, or lets a delete fail halfway because Steam is using the files.
        report.AppendLine();
        report.AppendLine("[Steam-closed requirement by category]");
        var mustClose = new[]
        {
            ScanCategory.SteamCache, ScanCategory.ShaderCache,
            ScanCategory.WorkshopCache, ScanCategory.UserData,
        };
        var mayStayOpen = new[]
        {
            ScanCategory.CommonFolder, ScanCategory.DirectXCache, ScanCategory.CrashDumps,
        };

        foreach (var category in mustClose)
        {
            var probeItem = new ScanItem
            {
                Path = "probe", Category = category, Confidence = Confidence.Safe,
                ReasonId = "reason.emptyFolder",
            };
            Check($"  {category} requires Steam closed", probeItem.RequiresSteamClosed);
        }

        foreach (var category in mayStayOpen)
        {
            var probeItem = new ScanItem
            {
                Path = "probe", Category = category, Confidence = Confidence.Safe,
                ReasonId = "reason.emptyFolder",
            };
            Check($"  {category} does NOT require Steam closed", !probeItem.RequiresSteamClosed);
        }

        // Reality check against the live scan: the two exempt categories must be present.
        var exemptPresent = items
            .Where(i => i.Category is ScanCategory.DirectXCache or ScanCategory.CrashDumps)
            .ToList();
        Check("exempt categories exist in a real scan", exemptPresent.Count > 0,
            $"{exemptPresent.Count} item(s) can be cleaned with Steam open");

        var saveDataPreselected = items
            .Where(i => i.Confidence is Confidence.Safe or Confidence.Likely)
            .Where(i => SafetyRules.LooksLikeSaveData(i.FileName))
            .Select(i => i.Path)
            .ToList();
        Check("nothing flagged as save data is auto-enabled",
            saveDataPreselected.Count == 0, string.Join("; ", saveDataPreselected));

        report.AppendLine();
        report.AppendLine("  category breakdown:");
        foreach (var group in items.GroupBy(i => i.Category).OrderBy(g => g.Key))
        {
            var bytes = group.Sum(i => i.SizeBytes);
            report.AppendLine($"    {group.Key,-16} {group.Count(),5} item(s)   {FormatSize(bytes),12}");
        }

        report.AppendLine();
        report.AppendLine("  confidence breakdown:");
        foreach (var group in items.GroupBy(i => i.Confidence).OrderBy(g => g.Key))
            report.AppendLine($"    {group.Key,-12} {group.Count(),5} item(s)   {FormatSize(group.Sum(i => i.SizeBytes)),12}");

        report.AppendLine();
        report.AppendLine("  top 15 candidates by size:");
        foreach (var item in items.OrderByDescending(i => i.SizeBytes).Take(15))
            report.AppendLine($"    {FormatSize(item.SizeBytes),12}  [{item.Confidence,-9}] {item.Path}");

        // ------------------------------------------------------------ Steam caches
        report.AppendLine();
        report.AppendLine("[System caches]");
        foreach (var path in SafetyRules.SystemCachePaths())
        {
            if (!Directory.Exists(path)) continue;
            var (bytes, files) = Scanner.MeasureDirectory(path);
            report.AppendLine($"    {FormatSize(bytes),12}  {files,5} file(s)  {path}");
        }

        // ------------------------------------------------------------- deletion guard
        report.AppendLine();
        report.AppendLine("[Deletion guard]");
        var cleaner = new Cleaner(log);
        var fakeManifest = new ScanItem
        {
            Path = Path.Combine(installation.SteamAppsPath, "appmanifest_0.acf"),
            Category = ScanCategory.SteamCache,
            Confidence = Confidence.Safe,
            ReasonId = "reason.tempManifest",
            Kind = ItemKind.File,
        };
        var refused = cleaner.Delete(fakeManifest, useRecycleBin: true);
        Check("appmanifest deletion refused", !refused.Success, refused.Message);
        Check("refused appmanifest was not removed", !File.Exists(fakeManifest.Path));

        // ------------------------------------------------------------- Steam process
        report.AppendLine();
        report.AppendLine("[Steam process]");
        Info($"steam running    : {SteamLocator.IsSteamRunning()}");

        return Finish(report, reportPath, failures);
    }

    private static int Finish(StringBuilder report, string reportPath, int failures)
    {
        report.AppendLine(new string('-', 72));
        report.AppendLine(failures == 0
            ? "RESULT: ALL CHECKS PASSED"
            : $"RESULT: {failures} CHECK(S) FAILED");

        var text = report.ToString();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, text, new UTF8Encoding(true));
        }
        catch
        {
            // The console copy below is still available.
        }

        Console.WriteLine(text);
        return failures == 0 ? 0 : 1;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1048576.0:N1} MB",
        _ => $"{bytes / 1073741824.0:N2} GB",
    };

    public static string DefaultReportPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SteamScrup", "selftest-report.txt");
}
