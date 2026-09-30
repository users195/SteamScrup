namespace SteamScrup.Core;

/// <summary>How strongly the evidence says "this is leftover junk".</summary>
public enum Confidence
{
    /// <summary>Never deletable in the UI; shown for transparency only.</summary>
    Protected = 0,

    /// <summary>Real content, possibly user data. Never pre-selected.</summary>
    Review = 1,

    /// <summary>Almost certainly safe, but the user should still look.</summary>
    Likely = 2,

    /// <summary>Empty or trivially small; safe to remove.</summary>
    Safe = 3,
}

public enum ScanCategory
{
    CommonFolder,
    ShaderCache,
    WorkshopCache,
    SteamCache,
    DirectXCache,
    CrashDumps,
    UserData,
    Info,
}

public static class CategoryInfo
{
    public static bool IsAdvanced(ScanCategory category) => category == ScanCategory.UserData;

    /// <summary>Categories that are hidden entirely until advanced mode is enabled.</summary>
    public static bool IsDestructive(ScanCategory category) =>
        category is ScanCategory.UserData;
}

public enum ItemKind
{
    Directory,
    File,
}

/// <summary>One row in the preview list.</summary>
public sealed class ScanItem
{
    public required string Path { get; init; }
    public required ScanCategory Category { get; init; }
    public required Confidence Confidence { get; init; }
    public ItemKind Kind { get; init; } = ItemKind.Directory;

    /// <summary>Short explanation shown in the list, already localized.</summary>
    public required string ReasonId { get; init; }

    /// <summary>Optional extra detail (e.g. the game name an appid maps to).</summary>
    public string? Detail { get; init; }

    /// <summary>Which library this belongs to, when applicable.</summary>
    public string? LibraryRoot { get; init; }

    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public DateTimeOffset? LastWriteTime { get; set; }
    public int? AppId { get; init; }

    /// <summary>
    /// Resolved game name for this appid, or null when it is unknown. Filled in after the
    /// scan (from the manifest, the local cache, or the store lookup), which is why the row
    /// view model re-reads it rather than copying it once.
    /// </summary>
    public string? GameName { get; set; }

    /// <summary>
    /// True for items whose title is the appid itself, so a game-name bubble makes sense.
    /// Folder-name candidates are excluded: their names come from the filesystem and an
    /// appid lookup would not correspond to the title shown.
    /// </summary>
    public bool WantsGameNameBubble => Category is ScanCategory.ShaderCache
        or ScanCategory.WorkshopCache
        or ScanCategory.UserData;

    /// <summary>
    /// True when the Steam client must be closed before this can be removed.
    ///
    /// Only four categories are actually held by the client: its own scratch files, the
    /// shader cache it writes to, the workshop content it re-verifies, and userdata which it
    /// syncs. The Windows/GPU shader caches, crash dumps left behind by games, and orphaned
    /// game folders are not owned by Steam, so they can be removed while it runs.
    /// </summary>
    public bool RequiresSteamClosed => Category is ScanCategory.SteamCache
        or ScanCategory.ShaderCache
        or ScanCategory.WorkshopCache
        or ScanCategory.UserData;

    /// <summary>True when deleting this cannot break an installed game.</summary>
    public bool RegeneratesAutomatically { get; init; }

    public string FileName
    {
        get
        {
            var trimmed = Path.TrimEnd('\\', '/');
            var idx = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            return idx >= 0 && idx < trimmed.Length - 1 ? trimmed[(idx + 1)..] : trimmed;
        }
    }

    public string SizeText => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:N1} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / 1048576.0:N1} MB",
        _ => $"{SizeBytes / 1073741824.0:N2} GB",
    };
}

/// <summary>Outcome of one deletion attempt.</summary>
public sealed record DeleteResult(string Path, bool Success, string Message);
