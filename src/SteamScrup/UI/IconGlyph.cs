using System.Windows;

namespace SteamScrup.UI;

/// <summary>
/// Resolves icon glyphs from the resource dictionaries in <c>UI/Icons.xaml</c>.
///
/// Glyphs are XAML resources (<c>&lt;sys:String x:Key="GlyphScan"&gt;</c>), NOT localization
/// strings. Looking them up through the localization indexer returned the key name itself
/// when the key was missing, and because the result was then drawn with the icon font, each
/// letter of the key name rendered as a missing-glyph box — the "everything is a row of
/// boxes" bug. Keeping the two lookups separate is what prevents a repeat.
/// </summary>
internal static class IconGlyph
{
    /// <summary>Shown when a glyph resource is missing, so the UI never renders a box row.</summary>
    private const string Fallback = "\u2022"; // bullet

    /// <summary>All resource keys defined in Icons.xaml. Used by the self-test.</summary>
    internal static readonly string[] AllKeys =
    {
        "GlyphOverview", "GlyphCommon", "GlyphDirectX", "GlyphShader", "GlyphWorkshop",
        "GlyphSteamCache", "GlyphCrash", "GlyphUserData", "GlyphSettings",
        "GlyphScan", "GlyphClean", "GlyphStop", "GlyphSelectAll", "GlyphSelectNone",
        "GlyphClearSelection", "GlyphSafeOnly", "GlyphSearch", "GlyphOpen", "GlyphRefresh",
        "GlyphInfo", "GlyphWarning", "GlyphCheck", "GlyphError", "GlyphShield",
    };

    /// <summary>
    /// Looks a glyph up in the application resources. Returns a harmless bullet rather than
    /// a long string when the key is absent, so a mistake degrades to one dot instead of a
    /// row of boxes.
    /// </summary>
    internal static string Get(string key)
    {
        var value = Application.Current?.TryFindResource(key) as string;
        return string.IsNullOrEmpty(value) ? Fallback : value!;
    }

    /// <summary>True when the key resolves to a single character, i.e. a usable glyph.</summary>
    internal static bool IsValidGlyph(string key, out string detail)
    {
        var value = Application.Current?.TryFindResource(key) as string;

        if (string.IsNullOrEmpty(value))
        {
            detail = "resource missing";
            return false;
        }

        if (value.Length != 1)
        {
            detail = $"expected 1 char, got {value.Length} ({value})";
            return false;
        }

        // Icon fonts put their glyphs in the Private Use Area; a plain letter would mean the
        // key name leaked through again.
        var cp = value[0];
        var inPua = cp is >= '\uE000' and <= '\uF8FF';
        detail = $"U+{(int)cp:X4}{(inPua ? "" : " (NOT in the Private Use Area)")}";
        return inPua;
    }
}
