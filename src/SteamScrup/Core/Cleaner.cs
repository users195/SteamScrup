using System.Runtime.Versioning;
using Microsoft.VisualBasic.FileIO;

namespace SteamScrup.Core;

public enum DeleteMode
{
    /// <summary>Move to the Recycle Bin so the user can restore it.</summary>
    RecycleBin,

    /// <summary>Delete permanently. Never the default; requires explicit opt-in.</summary>
    Permanent,
}

/// <summary>
/// Performs the actual removal. Always one item at a time, never silent, and never
/// touching an appmanifest: Steam must stay able to uninstall games itself.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Cleaner
{
    private readonly OperationLog _log;

    public Cleaner(OperationLog log) => _log = log;

    /// <summary>Matches a real appmanifest file name, but not "appmanifest_1.acf.tmp".</summary>
    private static readonly System.Text.RegularExpressions.Regex ManifestFileName =
        new(@"^appmanifest_\d+\.acf$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Deletes one item. Never throws: failures are converted into a result so a single
    /// locked file cannot abort a whole batch.
    /// </summary>
    public DeleteResult Delete(ScanItem item, bool useRecycleBin)
    {
        // Hard guard: an appmanifest must never be deleted by this tool.
        var fileName = Path.GetFileName(item.Path);
        if (ManifestFileName.IsMatch(fileName))
        {
            var refused = "refused: appmanifest files are never deleted by SteamScrup";
            _log.Warn($"SKIP  {item.Path}  ({refused})");
            return new DeleteResult(item.Path, false, refused);
        }

        if (!File.Exists(item.Path) && !Directory.Exists(item.Path))
        {
            _log.Warn($"SKIP  {item.Path}  (already gone)");
            return new DeleteResult(item.Path, false, "already gone");
        }

        try
        {
            if (item.Kind == ItemKind.File)
            {
                FileSystem.DeleteFile(item.Path,
                    UIOption.OnlyErrorDialogs,
                    useRecycleBin ? RecycleOption.SendToRecycleBin : RecycleOption.DeletePermanently);
            }
            else
            {
                FileSystem.DeleteDirectory(item.Path,
                    UIOption.OnlyErrorDialogs,
                    useRecycleBin ? RecycleOption.SendToRecycleBin : RecycleOption.DeletePermanently);
            }

            _log.Info($"DELETED  {item.Path}  ({item.SizeText}, {item.FileCount} files, " +
                      $"{(useRecycleBin ? "recycle-bin" : "permanent")})");
            return new DeleteResult(item.Path, true, "ok");
        }
        catch (UnauthorizedAccessException)
        {
            _log.Error($"DENIED  {item.Path}  (access denied; try running elevated)");
            return new DeleteResult(item.Path, false, "access denied");
        }
        catch (IOException ex)
        {
            _log.Error($"FAILED  {item.Path}  ({ex.Message})");
            return new DeleteResult(item.Path, false, ex.Message);
        }
        catch (Exception ex)
        {
            _log.Error($"FAILED  {item.Path}  ({ex.GetType().Name}: {ex.Message})");
            return new DeleteResult(item.Path, false, ex.Message);
        }
    }
}
