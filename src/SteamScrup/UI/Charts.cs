using System.Windows;
using System.Windows.Media;
using SteamScrup.Core;

namespace SteamScrup.UI;

/// <summary>One slice / bar in the summary charts.</summary>
public sealed class CategoryStat
{
    public required ScanCategory Category { get; init; }
    public required string Label { get; init; }
    public required string Glyph { get; init; }
    public required Brush Fill { get; init; }
    public required long Bytes { get; init; }
    public required int Count { get; init; }

    /// <summary>Share of the total, 0..1.</summary>
    public double Fraction { get; init; }

    public string SizeText => MainViewModel.FormatSize(Bytes);

    public string PercentText => $"{Fraction * 100:0.#}%";

    /// <summary>Pixel width of the bar, relative to the widest category.</summary>
    public double BarWidth { get; init; }

    public string CountText => Count.ToString();

    public bool IsEmpty => Bytes == 0 && Count == 0;
}

/// <summary>
/// Builds the summary chart data: per-category totals, pie geometry and bar widths.
///
/// The pie is drawn with plain WPF geometry (PathGeometry + ArcSegment) rather than a
/// charting library, to keep the portable build dependency-free.
/// </summary>
public static class ChartBuilder
{
    /// <summary>Fixed colour per category, readable on both the light and dark surface.</summary>
    private static readonly Dictionary<ScanCategory, string> Palette = new()
    {
        [ScanCategory.CommonFolder] = "#66C0F4",
        [ScanCategory.WorkshopCache] = "#9B8CF4",
        [ScanCategory.UserData] = "#E8A33D",
        [ScanCategory.DirectXCache] = "#4FC3A1",
        [ScanCategory.SteamCache] = "#7A8A99",
        [ScanCategory.CrashDumps] = "#E0685F",
        [ScanCategory.ShaderCache] = "#B8C95A",
    };

    /// <summary>Icon glyph resource key per category, matching Icons.xaml.</summary>
    private static readonly Dictionary<ScanCategory, string> Glyphs = new()
    {
        [ScanCategory.CommonFolder] = "GlyphCommon",
        [ScanCategory.WorkshopCache] = "GlyphWorkshop",
        [ScanCategory.UserData] = "GlyphUserData",
        [ScanCategory.DirectXCache] = "GlyphDirectX",
        [ScanCategory.SteamCache] = "GlyphSteamCache",
        [ScanCategory.CrashDumps] = "GlyphCrash",
        [ScanCategory.ShaderCache] = "GlyphShader",
    };

    /// <summary>Order used when two categories tie on size.</summary>
    private static readonly ScanCategory[] Order =
    {
        ScanCategory.CommonFolder,
        ScanCategory.WorkshopCache,
        ScanCategory.DirectXCache,
        ScanCategory.UserData,
        ScanCategory.SteamCache,
        ScanCategory.CrashDumps,
        ScanCategory.ShaderCache,
    };

    public static string LabelKey(ScanCategory category) => category switch
    {
        ScanCategory.CommonFolder => "cat.common",
        ScanCategory.ShaderCache => "cat.shader",
        ScanCategory.WorkshopCache => "cat.workshop",
        ScanCategory.SteamCache => "cat.steamCache",
        ScanCategory.DirectXCache => "cat.directX",
        ScanCategory.CrashDumps => "cat.crash",
        ScanCategory.UserData => "cat.userdata",
        _ => "cat.all",
    };

    public static Brush ColorFor(ScanCategory category) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            Palette.TryGetValue(category, out var hex) ? hex : "#7A8A99"));

    public static string GlyphKeyFor(ScanCategory category) =>
        Glyphs.TryGetValue(category, out var key) ? key : "GlyphInfo";

    /// <summary>
    /// Aggregates scanned items by category, largest first. The bar width is normalised
    /// against the biggest category, and percentages against the grand total.
    /// </summary>
    public static List<CategoryStat> Build(IEnumerable<ScanItem> items, double maxBarWidth = 320)
    {
        var list = items as IReadOnlyCollection<ScanItem> ?? items.ToList();
        var total = list.Sum(i => i.SizeBytes);

        var stats = new List<CategoryStat>();
        foreach (var category in Order)
        {
            var slice = list.Where(i => i.Category == category).ToList();
            if (slice.Count == 0) continue;

            var bytes = slice.Sum(i => i.SizeBytes);
            stats.Add(new CategoryStat
            {
                Category = category,
                Label = L.T(LabelKey(category)),
                Glyph = IconGlyph.Get(GlyphKeyFor(category)),
                Fill = ColorFor(category),
                Bytes = bytes,
                Count = slice.Count,
                Fraction = total > 0 ? (double)bytes / total : 0,
            });
        }

        var ordered = stats.OrderByDescending(s => s.Bytes).ToList();
        var largest = ordered.Count > 0 ? ordered[0].Bytes : 0;
        if (largest > 0)
        {
            return ordered.Select(s => new CategoryStat
            {
                Category = s.Category,
                Label = s.Label,
                Glyph = s.Glyph,
                Fill = s.Fill,
                Bytes = s.Bytes,
                Count = s.Count,
                Fraction = s.Fraction,
                BarWidth = Math.Max(2, maxBarWidth * s.Bytes / largest),
            }).ToList();
        }

        return ordered;
    }

    /// <summary>
    /// One wedge of the pie, ready to bind: geometry plus the brush that fills it.
    /// Slices are produced individually (not merged into one GeometryGroup) because each
    /// wedge needs its own colour.
    /// </summary>
    public sealed class PieSlice
    {
        public required Geometry Geometry { get; init; }
        public required Brush Fill { get; init; }
        public required string Label { get; init; }
        public required string SizeText { get; init; }
        public required string PercentText { get; init; }
    }

    /// <summary>
    /// Builds one filled wedge per category. Slices smaller than
    /// <paramref name="minAngleDegrees"/> are widened so they stay visible; the resulting
    /// slight overlap is a deliberate readability trade-off, and the legend carries the
    /// exact numbers.
    /// </summary>
    public static List<PieSlice> BuildPieSlices(IReadOnlyList<CategoryStat> stats, double radius = 96)
    {
        var slices = new List<PieSlice>();
        var withBytes = stats.Where(s => s.Bytes > 0).ToList();
        var total = withBytes.Sum(s => s.Bytes);
        if (total <= 0) return slices;

        const double MinAngle = 1.2; // degrees
        var center = new Point(radius, radius);
        var startAngle = -90.0; // 12 o'clock

        foreach (var stat in withBytes)
        {
            var sweep = 360.0 * stat.Bytes / total;
            var drawn = Math.Max(sweep, MinAngle);

            var start = PointOnCircle(center, radius, startAngle);
            var end = PointOnCircle(center, radius, startAngle + drawn);

            var figure = new PathFigure { StartPoint = center, IsClosed = true, IsFilled = true };
            figure.Segments.Add(new LineSegment(start, false));

            if (drawn >= 359.99)
            {
                // A full circle cannot be expressed by a single ArcSegment.
                var opposite = PointOnCircle(center, radius, startAngle + 180);
                figure.Segments.Add(new ArcSegment(opposite, new Size(radius, radius), 0,
                    false, SweepDirection.Clockwise, false));
                figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0,
                    false, SweepDirection.Clockwise, false));
            }
            else
            {
                figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0,
                    drawn > 180, SweepDirection.Clockwise, false));
            }

            figure.Segments.Add(new LineSegment(center, false));

            slices.Add(new PieSlice
            {
                Geometry = new PathGeometry(new[] { figure }),
                Fill = stat.Fill,
                Label = stat.Label,
                SizeText = stat.SizeText,
                PercentText = stat.PercentText,
            });

            startAngle += drawn;
        }

        return slices;
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }
}
