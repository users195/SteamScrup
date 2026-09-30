using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SteamScrup.Core;

namespace SteamScrup.UI;

/// <summary>Drives the whole window: discovery, scanning, filtering and cleanup.</summary>
public sealed class MainViewModel : LocalizedViewModel
{
    private readonly AppSettings _settings;
    private readonly OperationLog _log;
    private readonly ThemeService _theme;
    private readonly GameNameResolver _names;

    private readonly List<ScanRowViewModel> _allRows = new();
    private CancellationTokenSource? _scanCts;
    private DispatcherTimer? _steamWatcher;

    private SteamInstallation? _installation;
    private bool _isScanning;
    private bool _isDeleting;
    private double _progressPercent;
    private bool _steamRunning;
    private CategoryFilter _selectedCategory;
    private string _searchText = string.Empty;

    /// <summary>
    /// Status is stored as a key plus arguments rather than as finished text, so a
    /// language switch re-renders it instead of leaving the old language behind.
    /// </summary>
    private string? _statusKey;
    private object?[] _statusArgs = Array.Empty<object?>();

    /// <summary>Set while a scan triggered by a Steam state change is running.</summary>
    private bool _autoRescanPending;

    public MainViewModel(AppSettings settings, OperationLog log, ThemeService theme)
    {
        _settings = settings;
        _log = log;
        _theme = theme;
        _names = new GameNameResolver(log);

        Categories = BuildCategories();
        _selectedCategory = Categories[0];

        Rows = new ObservableCollection<ScanRowViewModel>();
        Libraries = new ObservableCollection<string>();
        CategoryStats = new ObservableCollection<CategoryStat>();
        PieSlices = new ObservableCollection<ChartBuilder.PieSlice>();

        ScanCommand = new RelayCommand(() => _ = ScanAsync(), () => CanScan);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        SelectAllCommand = new RelayCommand(() => SelectAll(true), () => CanSelect);
        SelectNoneCommand = new RelayCommand(() => SelectAll(false), () => CanSelect);
        SelectSafeCommand = new RelayCommand(SelectSafeOnly, () => CanSelect);
        SelectAllSafeCommand = new RelayCommand(SelectSafeEverywhere, () => HasAnySelectableSafe);

        // Clears the tick boxes everywhere, including categories that are not on screen.
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => SelectedCount > 0);

        RefreshSteamState();
        StartSteamWatcher();
    }

    // ------------------------------------------------------------ collections

    public ObservableCollection<ScanRowViewModel> Rows { get; }

    public ObservableCollection<string> Libraries { get; }

    public List<CategoryFilter> Categories { get; }

    public ObservableCollection<CategoryStat> CategoryStats { get; }

    public ObservableCollection<ChartBuilder.PieSlice> PieSlices { get; }

    // ---------------------------------------------------------------- commands

    public RelayCommand ScanCommand { get; }
    public RelayCommand CancelScanCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand SelectSafeCommand { get; }

    /// <summary>
    /// Footer action: ticks every safe candidate across all categories, not just the ones
    /// visible in the current view.
    /// </summary>
    public RelayCommand SelectAllSafeCommand { get; }

    /// <summary>Footer action: untick everything, in every category.</summary>
    public RelayCommand ClearSelectionCommand { get; }

    public bool HasAnySelectableSafe =>
        !IsBusy && _allRows.Any(r => r.CanSelect && r.Confidence == Confidence.Safe);

    /// <summary>True while at least one item is ticked; gates the clear-selection action.</summary>
    public bool HasSelection => _allRows.Any(r => r.IsSelected);

    /// <summary>
    /// Scanning is read-only, so a running Steam client does not block it. Only the deletion
    /// path is gated on Steam being closed, and only for the categories Steam actually holds.
    /// </summary>
    public bool CanScan => !IsBusy;

    /// <summary>
    /// The selection commands need a non-empty, visible list. Previously their
    /// CanExecute was a bare IsListVisible check that was never re-evaluated, which is
    /// why the buttons appeared dead.
    /// </summary>
    public bool CanSelect => IsListVisible && Rows.Count > 0 && !IsBusy;

    // ------------------------------------------------------------------ state

    public SteamInstallation? Installation
    {
        get => _installation;
        private set
        {
            _installation = value;
            Raise(nameof(Installation), nameof(SteamRootText), nameof(GameCount),
                  nameof(CandidateCount), nameof(LibraryCountText), nameof(HasCandidates),
                  nameof(HasInstallation), nameof(ShowNoSteamHint));
        }
    }

    public bool HasInstallation => _installation is not null;

    /// <summary>Shown on the summary page when Steam could not be located at all.</summary>
    public bool ShowNoSteamHint => _installation is null && !IsScanning;

    public string SteamRootText => _installation?.SteamRoot ?? L.T("sum.notFound");

    public string LibraryCountText => _installation is null ? "0" : _installation.Libraries.Count.ToString();

    /// <summary>
    /// Installed games, excluding redistributables and runtimes: those carry a manifest too,
    /// but counting them as games inflates the number and conflicts with the cleanup list
    /// where their folder is protected.
    /// </summary>
    public int GameCount => _installation is null
        ? 0
        : _installation.AllManifests.Count(m => !SafetyRules.IsToolApp(m.AppId, m.Name));

    public int CandidateCount => _allRows.Count;

    public bool HasCandidates => _allRows.Count > 0;

    public string StatusMessage => _statusKey is null ? string.Empty : L.F(_statusKey, _statusArgs);

    private void SetStatus(string? key, params object?[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        OnPropertyChanged(nameof(StatusMessage));
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (_isScanning == value) return;
            _isScanning = value;
            Raise(nameof(IsScanning), nameof(IsBusy), nameof(CanScan), nameof(CanSelect),
                  nameof(ShowNoSteamHint));
            RefreshCommands();
        }
    }

    public bool IsBusy => IsScanning || IsDeleting;

    public bool IsDeleting
    {
        get => _isDeleting;
        private set
        {
            if (_isDeleting == value) return;
            _isDeleting = value;
            Raise(nameof(IsDeleting), nameof(IsBusy), nameof(CanClean), nameof(CanScan),
                  nameof(CanSelect), nameof(IsProgressVisible));
            RefreshCommands();
        }
    }

    private void RefreshCommands()
    {
        ScanCommand.RaiseCanExecuteChanged();
        CancelScanCommand.RaiseCanExecuteChanged();
        SelectAllCommand.RaiseCanExecuteChanged();
        SelectNoneCommand.RaiseCanExecuteChanged();
        SelectSafeCommand.RaiseCanExecuteChanged();
        SelectAllSafeCommand.RaiseCanExecuteChanged();
        ClearSelectionCommand.RaiseCanExecuteChanged();
        Raise(nameof(HasAnySelectableSafe), nameof(HasSelection));
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            _progressPercent = value;
            Raise(nameof(ProgressPercent), nameof(ScanProgressColumn), nameof(ProgressDetailText));
        }
    }

    /// <summary>
    /// Progress as a star width for the custom bar. The track is a two-column Grid: the
    /// first column holds the filled part, the second the remainder, so the bar scales
    /// with the window and never needs a pixel calculation.
    ///
    /// This exists because ProgressBar cannot be used: its Value metadata sets
    /// BindsTwoWayByDefault and its template writes back into Value, which throws when the
    /// source property is read-only. GridLength is never written back to.
    /// </summary>
    public GridLength ScanProgressColumn
    {
        get
        {
            var fraction = Math.Max(0, Math.Min(100, _progressPercent)) / 100.0;
            return new GridLength(Math.Max(fraction, 0.0001), GridUnitType.Star);
        }
    }

    public bool IsProgressVisible => IsScanning || IsDeleting;

    public string ProgressDetailText
    {
        get
        {
            if (IsDeleting) return $"{Math.Round(_progressPercent)}%";
            return _progressPercent >= 100 ? string.Empty : $"{Math.Round(_progressPercent)}%";
        }
    }

    // ------------------------------------------------------------ steam state

    /// <summary>True while a Steam client process exists; blocks every deletion.</summary>
    public bool IsSteamRunning
    {
        get => _steamRunning;
        private set
        {
            if (_steamRunning == value) return;
            _steamRunning = value;
            Raise(nameof(IsSteamRunning), nameof(CanClean), nameof(CanScan),
                  nameof(SteamStateText), nameof(SteamStateBrush),
                  nameof(IsCleanBlockedBySteam), nameof(ShowSteamBanner),
                  nameof(SteamBlockedBannerText), nameof(SteamBlockedCategoriesText));
            RefreshCommands();
        }
    }

    public string SteamStateText => L.T(IsSteamRunning ? "steam.running" : "steam.notRunning");

    public Brush SteamStateBrush =>
        Application.Current?.TryFindResource(IsSteamRunning ? "WarnFg" : "OkFg") as Brush
        ?? Brushes.Gray;

    // ------------------------------------------------------------- icon glyphs
    // Exposed as ordinary properties rather than reached through an indexer: glyphs are
    // XAML resources, not localization strings, and the previous indexer route silently
    // returned the key name (which the icon font then drew as a row of boxes).

    public string GlyphScan => IconGlyph.Get("GlyphScan");
    public string GlyphClean => IconGlyph.Get("GlyphClean");
    public string GlyphStop => IconGlyph.Get("GlyphStop");
    public string GlyphSelectAll => IconGlyph.Get("GlyphSelectAll");
    public string GlyphSelectNone => IconGlyph.Get("GlyphSelectNone");
    public string GlyphClearSelection => IconGlyph.Get("GlyphClearSelection");
    public string GlyphSafeOnly => IconGlyph.Get("GlyphSafeOnly");
    public string GlyphWarning => IconGlyph.Get("GlyphWarning");
    public string GlyphInfo => IconGlyph.Get("GlyphInfo");

    /// <summary>
    /// Polls for the Steam process so the banner and buttons follow reality. Without this
    /// the state was only sampled at startup and when cleanup was clicked, so exiting Steam
    /// left the UI claiming Steam was still running.
    /// </summary>
    private void StartSteamWatcher()
    {
        _steamWatcher = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        _steamWatcher.Tick += OnSteamWatcherTick;
        _steamWatcher.Start();
    }

    public void StopSteamWatcher()
    {
        if (_steamWatcher is null) return;
        _steamWatcher.Tick -= OnSteamWatcherTick;
        _steamWatcher.Stop();
        _steamWatcher = null;
    }

    private void OnSteamWatcherTick(object? sender, EventArgs e)
    {
        if (IsDeleting) return; // never race a cleanup run

        var running = SteamLocator.IsSteamRunning();
        if (running == _steamRunning) return;

        _log.Info($"steam state changed: running={_steamRunning} -> {running}");
        IsSteamRunning = running;

        // Rescan so the list reflects reality, but not while a scan is already running.
        if (IsScanning || _installation is null) return;

        _autoRescanPending = true;
        SetStatus("steam.stateChanged");
        _ = ScanAsync();
    }

    // ------------------------------------------------------------ categories

    public CategoryFilter SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedCategory)) return;
            _selectedCategory = value;
            Raise(nameof(SelectedCategory), nameof(IsSettingsSelected), nameof(IsSummarySelected),
                  nameof(IsListVisible), nameof(CanSelect));
            RefreshCommands();
            ApplyFilter();
        }
    }

    public bool IsSettingsSelected => _selectedCategory.IsSettings;

    public bool IsSummarySelected => _selectedCategory.Category is null && !_selectedCategory.IsSettings;

    public bool IsListVisible => !IsSettingsSelected && !IsSummarySelected;

    public string SearchText
    {
        get => _searchText;
        set
        {
            var text = value ?? string.Empty;
            if (_searchText == text) return;
            _searchText = text;
            Raise(nameof(SearchText), nameof(ShowSearchHint));
            ApplyFilter();
        }
    }

    /// <summary>Placeholder visibility: shown only while the box is empty and unfocused.</summary>
    private bool _searchFocused;

    public bool SearchFocused
    {
        get => _searchFocused;
        set
        {
            if (_searchFocused == value) return;
            _searchFocused = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowSearchHint));
        }
    }

    public bool ShowSearchHint => !_searchFocused && _searchText.Length == 0;

    // -------------------------------------------------------------- selection

    public int SelectedCount => _allRows.Count(r => r.IsSelected);

    public long SelectedBytes => _allRows.Where(r => r.IsSelected).Sum(r => r.SizeBytes);

    public string SelectedSizeText => FormatSize(SelectedBytes);

    public int SelectedReviewCount =>
        _allRows.Count(r => r.IsSelected && r.Confidence == Confidence.Review);

    public bool CanClean =>
        !IsBusy
        && SelectedCount > 0
        && _installation is not null
        // Only the categories Steam actually holds are blocked while it runs. Everything
        // else (system/GPU shader caches, crash dumps, orphaned game folders) can be
        // removed with Steam open.
        && !(IsSteamRunning && SelectedNeedingSteamClosedCount > 0);

    /// <summary>Selected items the Steam client must be closed for.</summary>
    public int SelectedNeedingSteamClosedCount =>
        _allRows.Count(r => r.IsSelected && r.Item.RequiresSteamClosed);

    /// <summary>Selected items that can be removed while Steam keeps running.</summary>
    public int SelectedSafeWithSteamOpenCount =>
        _allRows.Count(r => r.IsSelected && !r.Item.RequiresSteamClosed);

    /// <summary>
    /// True when the current selection is blocked purely by Steam running. The banner is
    /// still shown whenever Steam runs, because the wording covers partial availability.
    /// </summary>
    public bool IsCleanBlockedBySteam =>
        IsSteamRunning && SelectedNeedingSteamClosedCount > 0;

    /// <summary>The banner is visible for as long as Steam is running.</summary>
    public bool ShowSteamBanner => IsSteamRunning;

    /// <summary>Shown next to the clean button when Steam may be left open.</summary>
    public bool ShowSteamNotRequiredHint =>
        !IsBusy && SelectedCount > 0 && SelectedNeedingSteamClosedCount == 0;

    /// <summary>
    /// Banner line. It names the count only when part of the selection is actually blocked,
    /// so a user cleaning exempt items is not told the whole selection is unavailable.
    /// </summary>
    public string SteamBlockedBannerText =>
        SelectedNeedingSteamClosedCount > 0
            ? L.F("steam.blockedSelection", SelectedNeedingSteamClosedCount)
            : L.T("steam.running");

    /// <summary>Categories present in the current blocked selection, for the banner detail.</summary>
    public string SteamBlockedCategoriesText
    {
        get
        {
            var names = _allRows
                .Where(r => r.IsSelected && r.Item.RequiresSteamClosed)
                .Select(r => L.T(ChartBuilder.LabelKey(r.Category)))
                .Distinct()
                .ToList();

            return names.Count == 0 ? string.Empty : string.Join(" · ", names);
        }
    }

    public string CleanButtonText => SelectedCount > 0
        ? $"{L.T("tb.clean")} ({SelectedCount})"
        : L.T("tb.clean");

    public string SelectionSummaryText => L.F("sum.selection", SelectedCount, SelectedSizeText);

    /// <summary>Share of the selected bytes relative to every candidate.</summary>
    public double SelectedShare =>
        _allRows.Sum(r => r.SizeBytes) is var total && total > 0 ? (double)SelectedBytes / total : 0;

    // ------------------------------------------------------------ discovery

    public void RefreshSteamState() => IsSteamRunning = SteamLocator.IsSteamRunning();

    public async Task LoadInstallationAsync()
    {
        Installation = await Task.Run(() =>
        {
            try
            {
                var inst = SteamLocator.Discover(_settings.SteamRootOverride);
                if (inst is null) _log.Warn("Steam installation not found.");
                else
                {
                    _log.Info($"Steam root: {inst.SteamRoot}");
                    _log.Info($"libraryfolders.vdf: {inst.LibraryFoldersVdfPath ?? "(not found)"}");
                    foreach (var lib in inst.Libraries)
                        _log.Info($"  library: {lib.RootPath}  manifests={lib.Manifests.Count}");
                }
                return inst;
            }
            catch (Exception ex)
            {
                _log.Exception("Steam discovery failed", ex);
                return null;
            }
        });

        Libraries.Clear();
        if (Installation is not null)
        {
            foreach (var lib in Installation.Libraries)
                Libraries.Add(lib.RootPath);

            // Manifest names need no lookup; seeding them means the store is only asked
            // about appids no local manifest covers.
            _names.SeedFromManifests(Installation.AllManifests);
        }

        SetStatus(Installation is null ? "sum.noSteamHint" : null);
    }

    /// <summary>
    /// Fills in game names for candidates whose title is an appid. Runs after a scan, on a
    /// background thread, and only for appids that are not already cached; the UI keeps
    /// working meanwhile and the bubbles refresh when the answers arrive.
    ///
    /// The public endpoint only accepts one appid per request, so large userdata sets take
    /// a while: the shader-cache and workshop entries are resolved first, then the rest.
    /// </summary>
    private async Task ResolveNamesAsync(IReadOnlyList<ScanItem> items)
    {
        try
        {
            var wanted = Scanner.CollectAppIds(items);
            var missing = wanted.Where(id => _names.TryGet(id) is null).ToList();

            if (missing.Count == 0)
            {
                Raise(nameof(NameResolutionSummary));
                return;
            }

            // Items visible in the smaller categories first, so the list looks right early.
            var prioritized = items
                .Where(i => i.WantsGameNameBubble && i.AppId is > 0)
                .OrderBy(i => i.Category == ScanCategory.UserData ? 1 : 0)
                .Select(i => i.AppId!.Value)
                .Where(missing.Contains)
                .Distinct()
                .ToList();

            NameResolutionText = L.F("set.nameLookupProgress", 0, prioritized.Count);

            var progress = new Progress<(int Done, int Total)>(p =>
                NameResolutionText = L.F("set.nameLookupProgress", p.Done, p.Total));

            _isResolvingNames = true;
            Raise(nameof(IsResolvingNames));

            var resolved = await _names.ResolveAsync(prioritized, progress, CancellationToken.None);

            if (resolved.Count > 0)
            {
                Scanner.ApplyResolvedNames(items, _names);
                foreach (var row in _allRows) row.RefreshGameName();
                _log.Info($"name resolution applied to {resolved.Count} candidate name(s)");
            }

            Raise(nameof(NameResolutionSummary));
        }
        catch (Exception ex)
        {
            _log.Warn($"name resolution aborted: {ex.Message}");
        }
        finally
        {
            _isResolvingNames = false;
            Raise(nameof(IsResolvingNames), nameof(NameResolutionSummary));
        }
    }

    private bool _isResolvingNames;

    /// <summary>True while the background lookup is running, so the settings page can say so.</summary>
    public bool IsResolvingNames => _isResolvingNames;

    private string? _nameResolutionText;

    /// <summary>Progress line for the settings page: "12/80 names resolved".</summary>
    public string NameResolutionText
    {
        get => _nameResolutionText ?? NameResolutionSummary;
        private set
        {
            _nameResolutionText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>One-line note for the settings page describing where names come from.</summary>
    public string NameResolutionSummary =>
        $"{L.T("set.nameLookup")}: {_names.CachedCount} {L.T("set.nameLookupCached")}";

    // -------------------------------------------------------------- scanning

    public async Task ScanAsync()
    {
        if (IsScanning) return;

        if (Installation is null)
            await LoadInstallationAsync();

        if (Installation is null)
        {
            SetStatus("sum.noSteamHint");
            return;
        }

        _scanCts = new CancellationTokenSource();
        IsScanning = true;
        ProgressPercent = 0;

        if (!_autoRescanPending) SetStatus("tb.scanning");
        _autoRescanPending = false;

        var ct = _scanCts.Token;
        var scanner = new Scanner(Installation);
        var wasCancelled = false;

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                if (p.Total > 0) ProgressPercent = p.Done * 100.0 / p.Total;
            });

            var items = await Task.Run(() =>
            {
                var list = scanner.Enumerate(progress, ct);

                // userdata is always included. It used to be hidden behind an advanced
                // switch that had to be re-toggled after every restart; the risk is now
                // stated in the confirmation dialog instead.
                scanner.MeasureSizes(list, progress, ct);
                return list;
            }, ct);

            _allRows.Clear();
            foreach (var item in items)
                _allRows.Add(new ScanRowViewModel(item, IsPreSelected(item), _names));

            _log.Info($"scan finished: {items.Count} candidate(s)");
            foreach (var group in items.GroupBy(i => i.Category))
                _log.Info($"  {group.Key}: {group.Count()} item(s), " +
                          $"{FormatSize(group.Sum(i => i.SizeBytes))}");

            ApplyFilter();
            SetStatus("msg.scanComplete", items.Count);
            ProgressPercent = 100;

            // Names for appid-titled candidates are fetched after the list is usable, so a
            // slow or blocked network never delays the scan result.
            _ = ResolveNamesAsync(items);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            _log.Warn("scan cancelled by user");
            SetStatus("msg.scanCancelled");
        }
        catch (Exception ex)
        {
            _log.Exception("scan failed", ex);
            SetStatus("msg.scanFailed", ex.Message);
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
            if (!wasCancelled) ProgressPercent = 0;
            RaiseSelectionSummary();
        }
    }

    /// <summary>
    /// Only genuinely safe, regenerable categories are pre-ticked; anything that could be
    /// user data stays opt-in.
    /// </summary>
    private static bool IsPreSelected(ScanItem item) => item.Confidence switch
    {
        Confidence.Safe => item.Category is ScanCategory.ShaderCache
            or ScanCategory.WorkshopCache
            or ScanCategory.SteamCache
            or ScanCategory.DirectXCache
            or ScanCategory.CrashDumps,
        _ => false,
    };

    public void CancelScan()
    {
        try { _scanCts?.Cancel(); }
        catch { }
    }

    // -------------------------------------------------------------- filtering

    private void ApplyFilter()
    {
        var query = _allRows.AsEnumerable();

        if (_selectedCategory.Category is { } category)
            query = query.Where(r => r.Category == category);

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.Trim();
            query = query.Where(r =>
                r.Path.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Reason.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // Heaviest first inside a category: that is what the user is hunting for.
        var filtered = query
            .OrderByDescending(r => r.Confidence)
            .ThenByDescending(r => r.SizeBytes)
            .ToList();

        Rows.Clear();
        foreach (var row in filtered) Rows.Add(row);

        UpdateCategoryCounts();
        RebuildCharts();

        Raise(nameof(VisibleCountText), nameof(CanSelect),
              nameof(EmptyStateText), nameof(ShowEmptyState));
        RefreshCommands();
        RaiseSelectionSummary();
    }

    public string VisibleCountText => L.F("msg.itemCount", Rows.Count);

    // ------------------------------------------------------------ empty state

    public bool ShowEmptyState => IsListVisible && Rows.Count == 0;

    /// <summary>Distinguishes "not scanned yet" from "nothing here" from "no search hit".</summary>
    public string EmptyStateText
    {
        get
        {
            if (_allRows.Count == 0)
                return IsScanning ? L.T("tb.scanning") : L.T("msg.emptyNotScanned");

            if (!string.IsNullOrWhiteSpace(_searchText))
                return L.T("msg.emptySearch");

            return L.T("msg.emptyCategory");
        }
    }

    // --------------------------------------------------------------- counts

    private void UpdateCategoryCounts()
    {
        foreach (var category in Categories)
        {
            if (category.IsSettings)
            {
                category.Count = 0;
                category.SelectedCount = 0;
                continue;
            }

            var inCategory = category.Category is { } cat
                ? _allRows.Where(r => r.Category == cat)
                : _allRows;

            var rows = inCategory.ToList();
            category.Count = rows.Count;
            category.SelectedCount = rows.Count(r => r.IsSelected);
        }
    }

    // ---------------------------------------------------------------- charts

    private void RebuildCharts()
    {
        var stats = ChartBuilder.Build(_allRows.Select(r => r.Item));
        CategoryStats.Clear();
        foreach (var stat in stats) CategoryStats.Add(stat);

        PieSlices.Clear();
        foreach (var slice in ChartBuilder.BuildPieSlices(stats)) PieSlices.Add(slice);

        // CandidateCount and friends are derived from _allRows. Without these notifications
        // the summary cards stayed at whatever they were when the binding was first
        // evaluated, which is why "candidates found" showed 0 after a successful scan.
        Raise(nameof(TotalCandidateSizeText), nameof(SelectedShareText), nameof(HasChartData),
              nameof(InsightText), nameof(CandidateCount), nameof(HasCandidates),
              nameof(GameCount), nameof(LibraryCountText), nameof(ShowNoSteamHint),
              nameof(HasAnySelectableSafe));
        SelectAllSafeCommand.RaiseCanExecuteChanged();
    }

    public bool HasChartData => CategoryStats.Count > 0;

    public string TotalCandidateSizeText => FormatSize(_allRows.Sum(r => r.SizeBytes));

    public string SelectedShareText =>
        $"{SelectedSizeText} ({SelectedShare * 100:0.#}%)";

    /// <summary>One-line conclusion: which category dominates the leftovers.</summary>
    public string InsightText
    {
        get
        {
            if (CategoryStats.Count == 0) return string.Empty;
            var top = CategoryStats[0];
            return L.F("sum.insight", top.Label, top.SizeText, top.PercentText);
        }
    }

    // -------------------------------------------------------------- selection

    public void SelectAll(bool value)
    {
        foreach (var row in Rows.Where(r => r.CanSelect))
            row.IsSelected = value;
        RaiseSelectionSummary();
    }

    public void SelectSafeOnly()
    {
        foreach (var row in Rows)
            row.IsSelected = row.CanSelect && row.Confidence == Confidence.Safe;
        RaiseSelectionSummary();
    }

    /// <summary>
    /// Selects safe candidates in every category, ignoring the current filter. Used by the
    /// footer button so the user does not have to visit each category to tick the safe ones.
    /// </summary>
    public void SelectSafeEverywhere()
    {
        foreach (var row in _allRows)
            row.IsSelected = row.CanSelect && row.Confidence == Confidence.Safe;

        _log.Info($"select all safe: {_allRows.Count(r => r.IsSelected)} item(s) selected " +
                  $"across all categories");
        RaiseSelectionSummary();
    }

    /// <summary>
    /// Unticks every item in every category, including ones the current filter hides.
    /// The mirror of <see cref="SelectSafeEverywhere"/>.
    /// </summary>
    public void ClearSelection()
    {
        var cleared = _allRows.Count(r => r.IsSelected);

        foreach (var row in _allRows) row.IsSelected = false;

        _log.Info($"clear selection: {cleared} item(s) unticked");
        RaiseSelectionSummary();
    }

    public void RaiseSelectionSummary()
    {
        Raise(nameof(SelectedCount), nameof(SelectedBytes), nameof(SelectedSizeText),
              nameof(SelectedReviewCount), nameof(CanClean), nameof(CleanButtonText),
              nameof(SelectionSummaryText), nameof(SelectedShare), nameof(SelectedShareText),
              nameof(HasSelection), nameof(SelectedNeedingSteamClosedCount),
              nameof(SelectedSafeWithSteamOpenCount), nameof(IsCleanBlockedBySteam),
              nameof(ShowSteamBanner), nameof(ShowSteamNotRequiredHint),
              nameof(SteamBlockedBannerText), nameof(SteamBlockedCategoriesText));
        ClearSelectionCommand.RaiseCanExecuteChanged();

        // The sidebar badges and the summary counters are derived from the same state, so
        // they have to be refreshed on every selection change too.
        UpdateCategoryCounts();
        RebuildCharts();
    }

    // ---------------------------------------------------------------- cleanup

    /// <summary>
    /// Removes the selected items. The mode is always supplied by the caller: the
    /// confirmation dialog asks the user on every run, so there is no stored preference to
    /// fall back to (and no way for a stale setting to contradict the pressed button).
    /// </summary>
    public async Task<IReadOnlyList<DeleteResult>> CleanSelectedAsync(bool useRecycleBin)
    {
        var recycle = useRecycleBin;
        var targets = _allRows.Where(r => r.IsSelected).ToList();
        if (targets.Count == 0) return Array.Empty<DeleteResult>();

        // Refuse to act only when the selection contains something Steam is holding.
        RefreshSteamState();
        if (IsCleanBlockedBySteam)
        {
            _log.Warn($"cleanup blocked: Steam is running and " +
                      $"{SelectedNeedingSteamClosedCount} selected item(s) need it closed " +
                      $"({SteamBlockedCategoriesText})");
            return Array.Empty<DeleteResult>();
        }

        IsDeleting = true;
        var cleaner = new Cleaner(_log);
        var results = new List<DeleteResult>();

        _log.Info($"----- cleanup started: {targets.Count} item(s), " +
                  $"{(recycle ? "recycle-bin" : "permanent")} -----");

        try
        {
            foreach (var row in targets)
            {
                var result = await Task.Run(() => cleaner.Delete(row.Item, recycle));
                results.Add(result);
                ProgressPercent = results.Count * 100.0 / targets.Count;

                if (result.Success) _allRows.Remove(row);
            }
        }
        finally
        {
            IsDeleting = false;
            ProgressPercent = 0;
            ApplyFilter();
            RaiseSelectionSummary();
        }

        var ok = results.Count(r => r.Success);
        _log.Info($"----- cleanup finished: {ok} ok, {results.Count - ok} failed -----");
        return results;
    }

    // -------------------------------------------------------------- settings

    public AppSettings Settings => _settings;

    public ThemeService Theme => _theme;

    public OperationLog Log => _log;

    public string DataLocationText => AppPaths.DescribeLocation();

    public bool IsPortable => AppPaths.IsPortable;

    /// <summary>
    /// Sidebar width, bound to the navigation column. Dragging the divider between the
    /// sidebar and the content updates it; double-clicking the divider resets it.
    /// </summary>
    public double SidebarWidth
    {
        get => AppSettings.ClampSidebarWidth(_settings.SidebarWidth);
        set
        {
            var clamped = AppSettings.ClampSidebarWidth(value);
            if (Math.Abs(_settings.SidebarWidth - clamped) < 0.5) return;

            _settings.SidebarWidth = clamped;
            OnPropertyChanged();
        }
    }

    /// <summary>Persists the current width once the drag has finished.</summary>
    public void CommitSidebarWidth()
    {
        _settings.SidebarWidth = SidebarWidth;
        _settings.Save();
        _log.Info($"sidebar width saved: {_settings.SidebarWidth:0}");
    }

    /// <summary>Restores the default width; wired to a double-click on the divider.</summary>
    public void ResetSidebarWidth()
    {
        _settings.SidebarWidth = AppSettings.DefaultSidebarWidth;
        _settings.Save();
        OnPropertyChanged(nameof(SidebarWidth));
        _log.Info($"sidebar width reset to {AppSettings.DefaultSidebarWidth:0}");
    }

    public string LogDirectory
    {
        get => _settings.LogDirectory ?? OperationLog.DefaultDirectory();
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            var effective = trimmed.Length == 0 ? null : trimmed;
            if (string.Equals(_settings.LogDirectory, effective, StringComparison.OrdinalIgnoreCase)) return;

            _settings.LogDirectory = effective;
            _settings.Save();
            var ok = _log.SetDirectory(LogDirectory);
            OnPropertyChanged();
            SetStatus(ok ? "set.saved" : "msg.logDirFailed");
        }
    }

    public string SteamRootOverride
    {
        get => _settings.SteamRootOverride ?? string.Empty;
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            var effective = trimmed.Length == 0 ? null : trimmed;
            if (string.Equals(_settings.SteamRootOverride, effective, StringComparison.OrdinalIgnoreCase)) return;

            _settings.SteamRootOverride = effective;
            _settings.Save();
            OnPropertyChanged();
            SetStatus("set.saved");
        }
    }

    public void SaveSettings()
    {
        _settings.Save();
        _log.Info($"settings saved: theme={_settings.Theme} language={_settings.Language} " +
                  $"steamRoot={_settings.SteamRootOverride ?? "(auto)"} logDir={_settings.LogDirectory ?? "(default)"}");
    }

    // --------------------------------------------------------------- helpers

    protected override void OnLocalizationChanged()
    {
        foreach (var row in _allRows) row.RefreshText();
        foreach (var category in Categories) category.RefreshLocalization();

        // Re-render chart labels in the new language.
        RebuildCharts();

        Raise(nameof(SteamStateText), nameof(CleanButtonText), nameof(VisibleCountText),
              nameof(SteamRootText), nameof(StatusMessage), nameof(EmptyStateText),
              nameof(SelectionSummaryText), nameof(InsightText));
        ApplyFilter();
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1048576.0:N1} MB",
        _ => $"{bytes / 1073741824.0:N2} GB",
    };

    private static List<CategoryFilter> BuildCategories() => new()
    {
        new CategoryFilter { Key = "summary", LabelKey = "cat.summary", GlyphKey = "GlyphOverview" },
        new CategoryFilter
        {
            Key = "common", LabelKey = "cat.common", GlyphKey = "GlyphCommon",
            Category = ScanCategory.CommonFolder,
        },
        new CategoryFilter
        {
            Key = "directX", LabelKey = "cat.directX", GlyphKey = "GlyphDirectX",
            Category = ScanCategory.DirectXCache,
        },
        new CategoryFilter
        {
            Key = "shader", LabelKey = "cat.shader", GlyphKey = "GlyphShader",
            Category = ScanCategory.ShaderCache,
        },
        new CategoryFilter
        {
            Key = "workshop", LabelKey = "cat.workshop", GlyphKey = "GlyphWorkshop",
            Category = ScanCategory.WorkshopCache,
        },
        new CategoryFilter
        {
            Key = "steamCache", LabelKey = "cat.steamCache", GlyphKey = "GlyphSteamCache",
            Category = ScanCategory.SteamCache,
        },
        new CategoryFilter
        {
            Key = "crash", LabelKey = "cat.crash", GlyphKey = "GlyphCrash",
            Category = ScanCategory.CrashDumps,
        },
        new CategoryFilter
        {
            Key = "userdata", LabelKey = "cat.userdata", GlyphKey = "GlyphUserData",
            Category = ScanCategory.UserData, IsAdvanced = true,
        },
        new CategoryFilter { Key = "settings", LabelKey = "cat.settings", GlyphKey = "GlyphSettings", IsSettings = true },
    };

    public void OpenInExplorer(string path)
    {
        try
        {
            var target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(target)) return;
            _log.Info($"open in explorer: {target}");
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Exception("open in explorer failed", ex);
        }
    }

    public void CopyToClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch (Exception ex) { _log.Exception("clipboard failed", ex); }
    }
}
