using SteamScrup.Core;

namespace SteamScrup.UI;

/// <summary>
/// One entry in the sidebar navigation.
///
/// The label is resolved on every read rather than cached in the constructor, so a
/// language switch is picked up immediately; the previous cached version was the reason
/// the sidebar stayed Chinese after switching to English.
/// </summary>
public sealed class CategoryFilter : LocalizedViewModel
{
    public required string Key { get; init; }

    /// <summary>Localization key for the label (not the translated text).</summary>
    public required string LabelKey { get; init; }

    /// <summary>Resource key of the icon glyph in Icons.xaml.</summary>
    public required string GlyphKey { get; init; }

    public ScanCategory? Category { get; init; }
    public bool IsAdvanced { get; init; }
    public bool IsSettings { get; init; }

    /// <summary>Resolved fresh on each read so it follows language changes.</summary>
    public string Label => L.T(LabelKey);

    /// <summary>
    /// Same text as <see cref="Label"/> but with change notification, for targets that
    /// cache their content. A WPF ContentPresenter detects a plain string change only when
    /// the property raises PropertyChanged, which is how the sidebar re-localizes.
    /// </summary>
    public string DisplayName => L.T(LabelKey);

    public string Glyph => IconGlyph.Get(GlyphKey);

    /// <summary>Total candidates in this category; kept up to date by the view model.</summary>
    private int _count;

    public int Count
    {
        get => _count;
        set
        {
            if (_count == value) return;
            _count = value;
            RaiseCountChanged();
        }
    }

    /// <summary>How many of <see cref="Count"/> are currently ticked.</summary>
    private int _selectedCount;

    public int SelectedCount
    {
        get => _selectedCount;
        set
        {
            if (_selectedCount == value) return;
            _selectedCount = value;
            RaiseCountChanged();
        }
    }

    private void RaiseCountChanged()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>"3/55" once something is ticked, otherwise just "55".</summary>
    public string CountText => _count <= 0
        ? string.Empty
        : _selectedCount > 0
            ? $"{_selectedCount}/{_count}"
            : _count.ToString();

    public bool HasItems => _count > 0;

    public bool HasSelection => _selectedCount > 0;

    /// <summary>Hidden while the corresponding advanced option is off.</summary>
    private bool _isAvailable = true;

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable == value) return;
            _isAvailable = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    public bool IsVisible => _isAvailable;

    protected override void OnLocalizationChanged()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(CountText));
    }
}
