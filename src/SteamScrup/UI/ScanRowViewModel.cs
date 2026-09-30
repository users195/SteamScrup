using System.ComponentModel;
using System.Runtime.CompilerServices;
using SteamScrup.Core;

namespace SteamScrup.UI;

/// <summary>Observable wrapper for one candidate card in the preview list.</summary>
public sealed class ScanRowViewModel : INotifyPropertyChanged
{
    private readonly ScanItem _item;
    private readonly Core.GameNameResolver? _nameResolver;
    private bool _isSelected;

    public ScanRowViewModel(ScanItem item, bool isSelected, Core.GameNameResolver? nameResolver = null)
    {
        _item = item;
        _isSelected = isSelected;
        _nameResolver = nameResolver;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScanItem Item => _item;

    public string Path => _item.Path;

    public string Name => _item.FileName;

    public string? Detail => _item.Detail;

    public ScanCategory Category => _item.Category;

    public Confidence Confidence => _item.Confidence;

    public long SizeBytes => _item.SizeBytes;

    public int FileCount => _item.FileCount;

    public DateTimeOffset? LastWriteTime => _item.LastWriteTime;

    /// <summary>Localized reason text, recomputed when the language changes.</summary>
    public string Reason => _item.ReasonId.Length == 0 ? string.Empty : L.T(_item.ReasonId);

    /// <summary>
    /// Game name shown in a bubble next to the title. The bubble exists only for the
    /// appid-titled categories; folder-name candidates keep their own title and get no
    /// bubble.
    /// </summary>
    public string GameNameBubble =>
        _item.WantsGameNameBubble && _item.AppId is { } appId
            ? _nameResolver?.Describe(appId) ?? $"AppID {appId}"
            : string.Empty;

    public bool HasGameNameBubble => GameNameBubble.Length > 0;

    /// <summary>True when the bubble carries a real game name rather than an AppID marker.</summary>
    public bool HasResolvedName =>
        _item.WantsGameNameBubble && _item.AppId is { } appId &&
        _nameResolver?.TryGet(appId) is { Length: > 0 };

    /// <summary>Re-reads the resolved name; called when the lookup completes.</summary>
    public void RefreshGameName()
    {
        OnPropertyChanged(nameof(GameNameBubble));
        OnPropertyChanged(nameof(HasGameNameBubble));
        OnPropertyChanged(nameof(HasResolvedName));
    }

    /// <summary>Rows that must never be selectable.</summary>
    public bool CanSelect => _item.Confidence != Confidence.Protected;

    /// <summary>True for protected rows so the card can be visually de-emphasised.</summary>
    public bool IsProtected => _item.Confidence == Confidence.Protected;

    public bool IsSelected
    {
        get => _isSelected && CanSelect;
        set
        {
            if (!CanSelect) return;
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string SizeText => _item.SizeText;

    public string FileCountText => _item.FileCount > 0
        ? L.F("msg.fileCount", _item.FileCount)
        : string.Empty;

    public string ModifiedText => _item.LastWriteTime is { } t
        ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : string.Empty;

    /// <summary>Icon glyph for the item's category, resolved from Icons.xaml.</summary>
    public string Glyph => IconGlyph.Get(ChartBuilder.GlyphKeyFor(_item.Category));

    /// <summary>Accent colour of the item's category, used for the card icon block.</summary>
    public System.Windows.Media.Brush CategoryBrush => ChartBuilder.ColorFor(_item.Category);

    /// <summary>True when the item regenerates itself, so the card can say so.</summary>
    public bool Regenerates => _item.RegeneratesAutomatically;

    public void RefreshText()
    {
        OnPropertyChanged(nameof(Reason));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(FileCountText));
        OnPropertyChanged(nameof(ModifiedText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
