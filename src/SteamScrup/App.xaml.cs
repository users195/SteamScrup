using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SteamScrup.Core;

namespace SteamScrup;

public partial class App : Application
{
    private readonly OperationLog _log;
    private readonly string[] _args;
    private BindingErrorListener? _bindingListener;
    private AppSettings _settings = new();
    private ThemeService _theme = null!;
    private UI.MainViewModel _viewModel = null!;
    private UI.MainWindow _window = null!;

    public App(OperationLog log, string[]? args = null)
    {
        _log = log;
        _args = args ?? Array.Empty<string>();
    }

    public AppSettings Settings => _settings;

    public ThemeService Theme => _theme;

    public UI.MainViewModel ViewModel => _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = AppSettings.Load();
        _log.Info($"settings loaded: theme={_settings.Theme} language={_settings.Language}");

        // The log folder can be relocated; honour the stored preference immediately.
        if (!string.IsNullOrWhiteSpace(_settings.LogDirectory))
        {
            if (_log.SetDirectory(_settings.LogDirectory!))
                _log.Info($"log directory set to {_settings.LogDirectory}");
            else
                _log.Warn($"could not use log directory {_settings.LogDirectory}; falling back to {_log.Directory}");
        }

        // Language must be chosen before the window binds any text.
        Localizer.Instance.Language = Localizer.ParseLanguage(_settings.Language);
        Localizer.Instance.LanguageChanged += OnLanguageChanged;

        _theme = new ThemeService { Theme = _settings.Theme };
        _theme.ThemeChanged += (_, _) => ApplyTheme();
        _theme.StartWatching();
        ApplyTheme();

        _viewModel = new UI.MainViewModel(_settings, _log, _theme);
        _window = new UI.MainWindow { DataContext = _viewModel };
        MainWindow = _window;

        // Binding problems surface as trace messages, not exceptions, so they would
        // otherwise only be visible to a debugger. Capture them from the start.
        // Diagnostic mode builds the window without ever showing it, so it works even
        // when the binding engine throws during HwndSource creation.
        var dumpPath = ArgValue("--dump-bindings=");
        if (dumpPath is not null)
        {
            RunBindingDump(dumpPath);
            Shutdown(0);
            return;
        }

        _bindingListener = BindingErrorListener.Attach(_log);

        // Showing the window is where invalid bindings detonate: the visual tree is not
        // built until a PresentationSource exists. In smoke mode the failure must not
        // abort the run - it gets recorded and reported instead.
        var smoke = ArgValue("--exit-after=") is not null;
        Exception? showFailure = null;
        try
        {
            _window.Show();
        }
        catch (Exception ex)
        {
            showFailure = ex;
            _log.Exception("window.Show() failed", ex);
            if (!smoke) throw;
        }

        if (showFailure is null)
        {
            _viewModel.RefreshLocalization();

            // Discovery and the first scan run after the window is up so the UI is responsive.
            _ = StartInitialScanAsync();

            // In smoke mode, exercise the runtime theme switch automatically: a silent
            // failure here is exactly what was reported, and it is invisible without this.
            if (smoke) _smokeExercise = ExerciseThemeSwitchAsync();
        }

        ScheduleSmokeExit(showFailure);
    }

    /// <summary>Result of the automated theme switch, included in the smoke report.</summary>
    public string ThemeSwitchReport { get; private set; } = "not exercised";

    /// <summary>Path of the last smoke screenshot, if one was taken.</summary>
    public string? LastScreenshotPath { get; private set; }

    /// <summary>Completes when the smoke exercise has finished mutating UI state.</summary>
    private Task _smokeExercise = Task.CompletedTask;

    /// <summary>
    /// Lets the binding engine flush and the render pass run before the next capture.
    /// Pumping the dispatcher alone proved insufficient here (four captures completed inside
    /// 95 ms and every one of them showed the previous page), so a short explicit delay is
    /// included; correctness of the captured frame matters more than the extra 250 ms.
    /// </summary>
    private async Task SettleAsync()
    {
        var frame = new DispatcherFrame();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);

        _window.UpdateLayout();
        await Task.Delay(250);
        _window.UpdateLayout();

        var probe = _viewModel.IsSummarySelected ? "summary"
            : _viewModel.IsSettingsSelected ? "settings"
            : "list:" + _viewModel.SelectedCategory.Key;
        _log.Info($"screenshot settle: visible view = {probe} " +
                  $"HasChartData={_viewModel.HasChartData} stats={_viewModel.CategoryStats.Count}");
    }

    /// <summary>
    /// Walks the live tree and reports the Visibility of the mutually exclusive summary
    /// blocks. Kept because a stale Visibility here was invisible in screenshots until the
    /// numbers were printed.
    /// </summary>
    private void LogSummaryVisibility()
    {
        if (!_viewModel.IsSummarySelected) return;

        void Walk(DependencyObject node)
        {
            if (node is TextBlock tb && tb.Text.StartsWith("还没有扫描数据", StringComparison.Ordinal))
                _log.Info($"  empty-state block: {tb.Visibility} " +
                          $"({(tb.IsVisible ? "VISIBLE" : "hidden")})");

            var n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < n; i++) Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
        }

        try { Walk(_window); }
        catch (Exception ex) { _log.Warn($"summary visibility probe failed: {ex.Message}"); }
    }

    /// <summary>
    /// Renders the live window to a PNG. RenderTargetBitmap works without a physical
    /// display, which is what makes visual verification possible in a headless session
    /// instead of relying on someone else to describe what they see.
    /// </summary>
    private void CaptureScreenshot(string path)
    {
        try
        {
            var width = (int)Math.Ceiling(_window.ActualWidth);
            var height = (int)Math.Ceiling(_window.ActualHeight);
            if (width <= 0 || height <= 0)
            {
                _log.Warn($"screenshot skipped: window size {width}x{height}");
                return;
            }

            var target = new System.Windows.Media.Imaging.RenderTargetBitmap(
                width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            target.Render(_window);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(target));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            encoder.Save(stream);

            LastScreenshotPath = path;
            _log.Info($"screenshot written: {path} ({width}x{height})");
        }
        catch (Exception ex)
        {
            _log.Exception("screenshot failed", ex);
        }
    }

    /// <summary>
    /// Drives the real Settings controls, not the services, so the event wiring is covered:
    /// flips Light -> Dark -> System through the radio buttons and records the accent colour
    /// that actually resolves afterwards. Also exercises the select commands, the language
    /// switch and the search placeholder.
    /// </summary>
    private async Task ExerciseThemeSwitchAsync()
    {
        var log = new System.Text.StringBuilder();
        try
        {
            await Task.Delay(2000); // let the initial scan settle

            string Accent() =>
                (Application.Current.TryFindResource("Accent") as System.Windows.Media.SolidColorBrush)
                ?.Color.ToString() ?? "(missing)";

            var original = _settings.Theme;
            var originalLanguage = Localizer.Instance.Language;

            // ---- theme, through the actual radio buttons
            _window.SmokeSelectTheme("Light");
            await Task.Delay(350);
            var afterLight = Accent();

            _window.SmokeSelectTheme("Dark");
            await Task.Delay(350);
            var afterDark = Accent();

            _window.SmokeSelectTheme("System");
            await Task.Delay(350);
            var afterSystem = Accent();

            log.AppendLine("[theme via settings radio]");
            log.AppendLine($"light  -> Accent={afterLight}");
            log.AppendLine($"dark   -> Accent={afterDark}");
            log.AppendLine($"system -> Accent={afterSystem}");
            log.AppendLine(afterLight != afterDark
                ? "RESULT: theme switch works (accent changed)"
                : "RESULT: THEME SWITCH BROKEN (accent did not change)");

            // ---- select commands, measured over the visible category only
            var category = _viewModel.Categories.FirstOrDefault(c => c.Category == ScanCategory.CommonFolder);
            if (category is not null) _viewModel.SelectedCategory = category;
            await Task.Delay(200);

            var rows = _viewModel.Rows.Count;
            int VisibleSelected() => _viewModel.Rows.Count(r => r.IsSelected);

            var visibleBefore = VisibleSelected();
            _viewModel.SelectAllCommand.Execute(null);
            var visibleAfterAll = VisibleSelected();
            _viewModel.SelectNoneCommand.Execute(null);
            var visibleAfterNone = VisibleSelected();
            _viewModel.SelectSafeCommand.Execute(null);
            var visibleAfterSafe = VisibleSelected();
            var safeRows = _viewModel.Rows.Count(r => r.Confidence == Confidence.Safe && r.CanSelect);
            var selectableRows = _viewModel.Rows.Count(r => r.CanSelect);
            var protectedRows = rows - selectableRows;

            log.AppendLine();
            log.AppendLine("[select commands]");
            log.AppendLine($"visible rows          : {rows} ({protectedRows} protected, {selectableRows} selectable)");
            log.AppendLine($"selected before       : {visibleBefore}");
            log.AppendLine($"after SelectAll       : {visibleAfterAll} (expect {selectableRows}, protected must stay off)");
            log.AppendLine($"after SelectNone      : {visibleAfterNone} (expect 0)");
            log.AppendLine($"after SelectSafe      : {visibleAfterSafe} (expect {safeRows})");
            var selectOk = rows > 0
                           && visibleAfterAll == selectableRows
                           && visibleAfterNone == 0
                           && visibleAfterSafe == safeRows;
            log.AppendLine(selectOk
                ? "RESULT: select commands work (protected rows are never selected)"
                : "RESULT: SELECT COMMANDS BROKEN");

            _viewModel.SelectNoneCommand.Execute(null);

            // ---- clear selection (must also clear categories that are filtered out)
            _viewModel.SelectAllSafeCommand.Execute(null);
            var afterSafeAll = _viewModel.SelectedCount;
            _viewModel.ClearSelectionCommand.Execute(null);
            var afterClear = _viewModel.SelectedCount;

            log.AppendLine();
            log.AppendLine("[clear selection]");
            log.AppendLine($"after SelectAllSafe : {afterSafeAll}");
            log.AppendLine($"after ClearSelection: {afterClear} (expect 0)");
            log.AppendLine(afterSafeAll > 0 && afterClear == 0
                ? "RESULT: clear selection works"
                : "RESULT: CLEAR SELECTION BROKEN");

            // ---- language completeness
            var zhLabels = string.Join("|", _viewModel.Categories.Select(c => c.DisplayName));
            Localizer.Instance.Language = AppLanguage.English;
            await Task.Delay(300);
            var enLabels = string.Join("|", _viewModel.Categories.Select(c => c.DisplayName));
            var enStatus = _viewModel.StatusMessage;

            log.AppendLine();
            log.AppendLine("[language switch]");
            log.AppendLine($"zh sidebar : {zhLabels}");
            log.AppendLine($"en sidebar : {enLabels}");
            log.AppendLine($"en status  : {enStatus}");
            var languageOk = zhLabels != enLabels && !ContainsCjk(enLabels) && !ContainsCjk(enStatus);
            log.AppendLine(languageOk
                ? "RESULT: language switch reaches sidebar and status"
                : "RESULT: LANGUAGE SWITCH INCOMPLETE");

            Localizer.Instance.Language = originalLanguage;
            await Task.Delay(200);

            // ---- search placeholder
            _window.SmokeSetSearch(string.Empty, focused: false);
            await Task.Delay(60);
            var hintWhenEmptyIdle = _viewModel.ShowSearchHint;
            _window.SmokeSetSearch(string.Empty, focused: true);
            await Task.Delay(60);
            var hintWhenFocused = _viewModel.ShowSearchHint;
            _window.SmokeSetSearch("zzz", focused: false);
            await Task.Delay(60);
            var hintWhenTyped = _viewModel.ShowSearchHint;
            _window.SmokeSetSearch(string.Empty, focused: false);

            log.AppendLine();
            log.AppendLine("[search placeholder]");
            log.AppendLine($"empty + unfocused : visible={hintWhenEmptyIdle}");
            log.AppendLine($"empty + focused   : visible={hintWhenFocused}");
            log.AppendLine($"typed             : visible={hintWhenTyped}");
            var hintOk = hintWhenEmptyIdle && !hintWhenFocused && !hintWhenTyped;
            log.AppendLine(hintOk
                ? "RESULT: placeholder behaves correctly"
                : "RESULT: PLACEHOLDER BROKEN");

            log.AppendLine();
            log.AppendLine("[icon glyphs]");
            var badGlyphs = new List<string>();
            foreach (var key in UI.IconGlyph.AllKeys)
            {
                if (UI.IconGlyph.IsValidGlyph(key, out var detail)) continue;
                badGlyphs.Add($"{key} ({detail})");
            }
            log.AppendLine($"checked {UI.IconGlyph.AllKeys.Length} glyph resource(s)");
            foreach (var key in UI.IconGlyph.AllKeys)
            {
                UI.IconGlyph.IsValidGlyph(key, out var detail);
                log.AppendLine($"  {key,-18} {detail}");
            }
            log.AppendLine(badGlyphs.Count == 0
                ? "RESULT: every glyph resolves to one Private Use Area character"
                : $"RESULT: {badGlyphs.Count} GLYPH(S) BROKEN: {string.Join("; ", badGlyphs)}");

            log.AppendLine();
            log.AppendLine("[localization coverage]");
            // Any key requested through the indexer that neither language defines.
            var missing = Localizer.Instance.MissingKeys.ToList();
            log.AppendLine($"missing keys: {(missing.Count == 0 ? "(none)" : string.Join(", ", missing))}");
            log.AppendLine(missing.Count == 0
                ? "RESULT: no unresolved localization keys"
                : $"RESULT: {missing.Count} UNRESOLVED KEY(S)");

            log.AppendLine();
            log.AppendLine("[window]");
            log.AppendLine(_window.SmokeVisualSummary());

            log.AppendLine();
            log.AppendLine("[sidebar width]");
            log.AppendLine($"view model  : {_viewModel.SidebarWidth:0}");
            log.AppendLine(_window.SmokeSidebarReport());

            // Scanning is read-only, so it must stay possible while Steam runs. Only the
            // deletion path is gated, and only for the categories Steam holds.
            log.AppendLine();
            log.AppendLine("[scan gating]");
            log.AppendLine($"CanScan                : {_viewModel.CanScan}");
            log.AppendLine($"selected needing Steam : {_viewModel.SelectedNeedingSteamClosedCount}");
            log.AppendLine($"clean blocked by Steam : {_viewModel.IsCleanBlockedBySteam}");

            // Force the "Steam is running" state through the private backing field so the
            // claim "rescan is not gated on Steam" is actually exercised rather than assumed.
            var steamField = typeof(UI.MainViewModel).GetField("_steamRunning",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var raised = typeof(UI.MainViewModel).GetProperty(nameof(UI.MainViewModel.IsSteamRunning));

            if (steamField is not null)
            {
                steamField.SetValue(_viewModel, true);
                raised?.GetSetMethod(nonPublic: true)?.Invoke(_viewModel, new object[] { true });

                var canScanWithSteam = _viewModel.CanScan;
                log.AppendLine($"with Steam forced running -> CanScan: {canScanWithSteam}");
                log.AppendLine(canScanWithSteam
                    ? "RESULT: rescan stays available while Steam runs"
                    : "RESULT: RESCAN INCORRECTLY GATED ON STEAM");

                steamField.SetValue(_viewModel, false);
                raised?.GetSetMethod(nonPublic: true)?.Invoke(_viewModel, new object[] { false });
            }
            else
            {
                log.AppendLine("RESULT: could not reach the steam state field; check skipped");
            }

            _settings.Theme = original;

            // Leave the UI on dark for the screenshot pass so both palettes get captured:
            // "system" would resolve to whatever Windows uses, hiding one of them.
            _window.SmokeSelectTheme("Dark");
            await Task.Delay(200);

            _log.Info("smoke exercise:\n" + log);
        }
        catch (Exception ex)
        {
            _log.Exception("smoke exercise failed", ex);
            log.AppendLine("exercise failed: " + ex.Message);
        }

        ThemeSwitchReport = log.ToString().TrimEnd();
    }

    private static bool ContainsCjk(string text) =>
        text.Any(c => c >= 0x4E00 && c <= 0x9FFF);

    /// <summary>Walks the live visual tree and lists every binding with its effective mode.</summary>
    private string WalkWindowBindings()
    {
        var report = new System.Text.StringBuilder();
        var twoWayBindings = new List<string>();
        var unwritable = new List<string>();
        var elementCount = 0;
        var bindingCount = 0;

        void Walk(System.Windows.DependencyObject node)
        {
            elementCount++;

            if (node is System.Windows.FrameworkElement or System.Windows.FrameworkContentElement)
            {
                foreach (var property in EnumerateBindableProperties(node))
                {
                    System.Windows.Data.BindingExpressionBase? expression;
                    try { expression = System.Windows.Data.BindingOperations.GetBindingExpressionBase(node, property); }
                    catch { continue; }

                    if (expression is not System.Windows.Data.BindingExpression be) continue;
                    bindingCount++;

                    var resolved = DescribeMode(be, property);
                    var pathText = be.ParentBinding.Path?.Path ?? "(none)";
                    report.AppendLine($"{resolved,-15} {node.GetType().Name,-26} {property.Name,-20} {pathText}");

                    if (!resolved.StartsWith("TwoWay", StringComparison.Ordinal)) continue;

                    twoWayBindings.Add($"{node.GetType().Name}.{property.Name} <- {pathText}  [{resolved}]");

                    // A two-way binding whose source has no setter would make the control
                    // silently unusable (typed text never reaching the view model), so it
                    // is worth flagging even though it no longer throws.
                    var isWritable = IsSourceWritable(be.DataItem, pathText);
                    if (isWritable == false)
                        unwritable.Add($"{node.GetType().Name}.{property.Name} <- {pathText}");
                }
            }

            var children = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < children; i++)
                Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
        }

        try { Walk(_window); }
        catch (Exception ex) { report.AppendLine("walk failed: " + ex.Message); }

        report.AppendLine(new string('-', 104));
        report.AppendLine($"elements={elementCount} bindings={bindingCount} twoWay={twoWayBindings.Count}");
        if (twoWayBindings.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("TwoWay bindings (these write back into the source):");
            foreach (var s in twoWayBindings) report.AppendLine("  " + s);
        }

        report.AppendLine();
        report.AppendLine(unwritable.Count == 0
            ? "RESULT: every TwoWay binding has a writable source (typed input works)"
            : $"RESULT: {unwritable.Count} TwoWay binding(s) have a READ-ONLY source (input would be lost):");
        foreach (var s in unwritable) report.AppendLine("  " + s);

        return report.ToString();
    }

    /// <summary>
    /// Verifies the source property of a two-way binding can actually be assigned.
    /// Returns null when the source is not a simple property that can be inspected.
    /// </summary>
    private static bool? IsSourceWritable(object? source, string? path)
    {
        if (source is null || string.IsNullOrWhiteSpace(path)) return null;
        if (path.StartsWith("[", StringComparison.Ordinal)) return true; // indexer setter checked by the engine

        var property = source.GetType().GetProperty(path);
        if (property is null) return null;

        return property.CanWrite;
    }

    private void RunBindingDump(string path)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("SteamScrup binding dump (window intentionally not shown)");
        report.AppendLine($"time : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine(new string('-', 104));

        // Without a PresentationSource the templates are not expanded, so this can only
        // inspect the window level. The smoke mode does the full-tree version.
        try { _window.Measure(new Size(1120, 720)); } catch (Exception ex) { report.AppendLine("measure failed: " + ex.Message); }
        try { _window.Arrange(new Rect(0, 0, 1120, 720)); } catch (Exception ex) { report.AppendLine("arrange failed: " + ex.Message); }
        try { _window.UpdateLayout(); } catch (Exception ex) { report.AppendLine("layout failed: " + ex.Message); }

        report.Append(WalkWindowBindings());

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, report.ToString());
        _log.Info($"binding dump written to {path}");
    }

    /// <summary>Reports the mode WPF will really use, accounting for control defaults.</summary>
    private static string DescribeMode(System.Windows.Data.BindingExpression be,
        System.Windows.DependencyProperty property)
    {
        // FrameworkPropertyMetadata.BindsTwoWayByDefault is what makes an unqualified
        // "{Binding X}" attempt to write back into the source.
        var metadata = property.GetMetadata(be.Target.GetType());
        var twoWayByDefault = metadata is System.Windows.FrameworkPropertyMetadata fpm &&
                              fpm.BindsTwoWayByDefault;

        return be.ParentBinding.Mode switch
        {
            System.Windows.Data.BindingMode.TwoWay => "TwoWay",
            System.Windows.Data.BindingMode.OneWayToSource => "OneWayToSource",
            System.Windows.Data.BindingMode.OneTime => "OneTime",
            System.Windows.Data.BindingMode.Default =>
                twoWayByDefault ? "TwoWay(default)" : "OneWay(default)",
            _ => be.ParentBinding.Mode.ToString(),
        };
    }

    private static IEnumerable<System.Windows.DependencyProperty> EnumerateBindableProperties(
        System.Windows.DependencyObject node)
    {
        var fields = node.GetType().GetFields(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.FlattenHierarchy);

        foreach (var field in fields)
        {
            if (field.FieldType != typeof(System.Windows.DependencyProperty)) continue;
            if (field.GetValue(null) is System.Windows.DependencyProperty dp) yield return dp;
        }
    }

    /// <summary>
    /// Smoke-test support: "--exit-after=8000 --binding-report=path" runs the real UI for
    /// a while, then writes a report (startup failure, binding warnings, full binding mode
    /// table) and quits. This makes binding regressions detectable without a human looking
    /// at the window.
    /// </summary>
    private void ScheduleSmokeExit(Exception? showFailure)
    {
        var exitAfter = ArgValue("--exit-after=");
        if (exitAfter is null) return;

        if (!int.TryParse(exitAfter, out var milliseconds) || milliseconds <= 0) milliseconds = 8000;
        var reportPath = ArgValue("--binding-report=") ?? Path.Combine(_log.Directory, "binding-report.txt");

        _log.Info($"smoke mode: exiting after {milliseconds} ms, binding report -> {reportPath}");

        var shotDir = ArgValue("--screenshot=");

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();

            // Wait for the exercise to finish touching UI state, or the screenshots would
            // capture a half-applied theme / language.
            try { await _smokeExercise; }
            catch (Exception ex) { _log.Exception("smoke exercise faulted", ex); }

            // Photograph each page while the window is still alive: this is the only way to
            // see the real rendered UI in a session with no display.
            if (shotDir is not null)
            {
                try
                {
                    foreach (var page in new[] { "summary", "common", "shader", "workshop", "userdata", "settings" })
                    {
                        if (!_window.SmokeShowPage(page))
                        {
                            _log.Warn($"screenshot: page '{page}' not found in navigation");
                            continue;
                        }

                        // Capturing immediately grabs the previous frame: layout and the
                        // render pass have to complete first, which needs a real yield.
                        await SettleAsync();
                        LogSummaryVisibility();
                        CaptureScreenshot(Path.Combine(shotDir, $"{page}.png"));
                    }

                    // Light theme pass. The theme is switched *before* the page loop so the
                    // render cache is rebuilt while the theme change is still settling;
                    // switching and capturing back to back produced a stale dark frame even
                    // though the window's brushes had already resolved to the light palette.
                    _window.SmokeSelectTheme("Light");
                    _window.SmokeShowPage("common");
                    await SettleAsync();
                    await SettleAsync();

                    var accent = Application.Current.TryFindResource("Accent")
                        as System.Windows.Media.SolidColorBrush;
                    var windowBg = _window.Background as System.Windows.Media.SolidColorBrush;

                    _log.Info($"light capture: requested={_theme.Theme} " +
                              $"serviceEffectiveDark={_theme.IsDarkEffective} " +
                              $"appAccent={accent?.Color.ToString() ?? "(missing)"} " +
                              $"windowBgBrush={windowBg?.Color.ToString() ?? "(null)"}");

                    CaptureScreenshot(Path.Combine(shotDir, "common-light.png"));
                }
                catch (Exception ex)
                {
                    _log.Exception("screenshot capture failed", ex);
                }
            }

            try
            {
                var report = new System.Text.StringBuilder();
                report.AppendLine("SteamScrup startup / binding diagnostics");
                report.AppendLine($"time : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine(new string('-', 104));
                report.AppendLine($"window shown : {_window.IsVisible}");
                report.AppendLine($"show() result: {(showFailure is null ? "OK" : "THREW " + showFailure.GetType().Name)}");
                if (showFailure is not null) report.AppendLine($"  message    : {showFailure.Message}");
                report.AppendLine($"candidates   : {_viewModel.CandidateCount}");
                report.AppendLine($"steam root   : {_viewModel.SteamRootText}");
                report.AppendLine($"status       : {_viewModel.StatusMessage}");
                report.AppendLine();
                report.AppendLine("--- runtime theme switch ---");
                report.AppendLine(ThemeSwitchReport);
                report.AppendLine();
                report.AppendLine("--- binding trace warnings ---");
                report.Append(_bindingListener?.BuildReport() ?? "listener not attached");
                report.AppendLine();
                report.AppendLine("--- all bindings in the live tree ---");
                report.Append(WalkWindowBindings());

                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                File.WriteAllText(reportPath, report.ToString());
                _log.Info($"binding report written to {reportPath}");
            }
            catch (Exception ex)
            {
                _log.Exception("writing binding report failed", ex);
            }

            Shutdown(showFailure is null ? 0 : 2);
        };
        timer.Start();
    }

    private string? ArgValue(string prefix)
    {
        foreach (var a in _args)
            if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = a[prefix.Length..].Trim().Trim('"');
                if (value.Length > 0) return value;
            }

        return null;
    }

    private async Task StartInitialScanAsync()
    {
        try
        {
            await _viewModel.LoadInstallationAsync();
            if (_viewModel.Installation is null)
            {
                _log.Warn("no Steam installation: skipping initial scan");
                return;
            }

            await _viewModel.ScanAsync();
        }
        catch (Exception ex)
        {
            _log.Exception("initial scan failed", ex);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _settings.Language = Localizer.LanguageToSetting(Localizer.Instance.Language);
        _settings.Save();
        _viewModel.RefreshLocalization();
    }

    /// <summary>Swaps the theme dictionary. Every themed brush is used via DynamicResource.</summary>
    public void ApplyTheme()
    {
        var dark = _theme.IsDarkEffective;
        var uri = new Uri(dark ? "UI/Themes/Dark.xaml" : "UI/Themes/Light.xaml", UriKind.Relative);

        ResourceDictionary dictionary;
        try
        {
            dictionary = new ResourceDictionary { Source = uri };
        }
        catch (Exception ex)
        {
            _log.Exception($"theme dictionary failed to load: {uri}", ex);
            return;
        }

        var merged = Resources.MergedDictionaries;
        var before = merged.Count > 0 ? merged[0].Source?.ToString() ?? "(no source)" : "(none)";

        if (merged.Count == 0) merged.Add(dictionary);
        else merged[0] = dictionary;

        var after = merged.Count > 0 ? merged[0].Source?.ToString() ?? "(no source)" : "(none)";

        // Instrumented because a runtime theme switch silently doing nothing was reported:
        // record what was requested, what the dictionary was before and after, and whether
        // a key that only exists in the theme files actually resolves.
        var accent = Resources["Accent"] as System.Windows.Media.SolidColorBrush;
        var accentText = accent is null ? "(Accent missing)" : accent.Color.ToString();

        _log.Info($"theme applied: requested={_theme.Theme} effective={(dark ? "dark" : "light")} " +
                  $"merged={merged.Count} before={before} after={after} Accent={accentText} " +
                  $"window={(MainWindow is null ? "none" : MainWindow.GetType().Name)}");
    }
}

