using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SteamScrup.Core;

namespace SteamScrup.UI;

public partial class MainWindow : Window
{
    private bool _initialised;
    private RadioButton? _themeLightRadio;
    private RadioButton? _themeDarkRadio;
    private RadioButton? _themeSystemRadio;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private MainViewModel Vm => (MainViewModel)DataContext;

    /// <summary>
    /// Test hooks for the smoke run: drive the real settings controls instead of poking
    /// the services directly, so the event wiring is covered too.
    /// </summary>
    internal void SmokeSelectTheme(string tag)
    {
        var target = tag switch
        {
            "Light" => _themeLightRadio,
            "Dark" => _themeDarkRadio,
            _ => _themeSystemRadio,
        };

        Vm.Log.Info($"smoke: selecting theme '{tag}' " +
                    $"(radio found={target is not null} " +
                    $"alreadyChecked={target?.IsChecked} " +
                    $"currentSetting={Vm.Settings.Theme})");

        if (target is null) return;

        if (target.IsChecked == true)
        {
            // Setting IsChecked to the value it already holds raises no Checked event, so
            // move the clock on first to guarantee a real transition is exercised.
            var other = tag == "Dark" ? _themeLightRadio : _themeDarkRadio;
            if (other is not null) other.IsChecked = true;
        }

        target.IsChecked = true;
        Vm.Log.Info($"smoke: after selecting '{tag}' setting={Vm.Settings.Theme} " +
                    $"serviceTheme={Vm.Theme.Theme} effectiveDark={Vm.Theme.IsDarkEffective}");
    }

    internal void SmokeSetSearch(string text, bool focused)
    {
        SearchBox.Text = text;
        Vm.SearchFocused = focused;
    }

    /// <summary>Switches the visible page so the smoke run can photograph each one.</summary>
    internal bool SmokeShowPage(string key)
    {
        var target = Vm.Categories.FirstOrDefault(c => c.Key == key);
        if (target is null) return false;

        // Setting IsChecked on a RadioButton that is already checked raises no Checked
        // event, so the view model has to be updated directly in that case.
        foreach (var rb in EnumerateRadioButtons(this))
        {
            if (rb.GroupName != "nav") continue;
            if (!ReferenceEquals(rb.Tag, target)) continue;

            if (rb.IsChecked == true) Vm.SelectedCategory = target;
            else rb.IsChecked = true;

            return true;
        }

        Vm.SelectedCategory = target;
        return true;
    }

    internal string SmokeVisualSummary() =>
        $"theme radios found: light={_themeLightRadio is not null} " +
        $"dark={_themeDarkRadio is not null} system={_themeSystemRadio is not null}\n" +
        $"selected nav: {Vm.SelectedCategory.LabelKey}\n" +
        $"sidebar labels: {string.Join(" | ", Vm.Categories.Select(c => c.DisplayName))}\n" +
        $"status text: {Vm.StatusMessage}";

    /// <summary>Reports the divider and column state so a broken resize shows up in the log.</summary>
    internal string SmokeSidebarReport()
    {
        var columnWidth = SidebarColumn.ActualWidth;
        var splitterVisible = SidebarSplitter.IsVisible;
        var clamping = $"{Core.AppSettings.MinSidebarWidth:0}..{Core.AppSettings.MaxSidebarWidth:0}";

        return $"column width: {columnWidth:0}\n" +
               $"splitter present: {splitterVisible}\n" +
               $"allowed range: {clamping}\n" +
               (columnWidth >= Core.AppSettings.MinSidebarWidth ? "RESULT: sidebar width applied" : "RESULT: SIDEBAR WIDTH NOT APPLIED");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncSettingsControls();
        CacheSettingsControls();
        _initialised = true;
        Vm.Log.Info("MainWindow loaded; settings controls synchronised");
    }

    /// <summary>Keeps references to the theme radios so the smoke run can click them.</summary>
    private void CacheSettingsControls()
    {
        foreach (var rb in EnumerateRadioButtons(this))
        {
            if (rb.GroupName != "theme") continue;
            switch (rb.Tag as string)
            {
                case "Light": _themeLightRadio = rb; break;
                case "Dark": _themeDarkRadio = rb; break;
                case "System": _themeSystemRadio = rb; break;
            }
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        // Stop polling so a closed window cannot keep a timer alive.
        Vm.StopSteamWatcher();
        Vm.Log.Info("MainWindow closed; steam watcher stopped");
    }

    /// <summary>Reflects stored settings onto the radio buttons without re-saving them.</summary>
    private void SyncSettingsControls()
    {
        foreach (var child in EnumerateRadioButtons(this))
        {
            switch (child.Tag as string)
            {
                case "System" when child.GroupName == "theme":
                    child.IsChecked = Vm.Settings.Theme == AppTheme.System;
                    break;
                case "Light" when child.GroupName == "theme":
                    child.IsChecked = Vm.Settings.Theme == AppTheme.Light;
                    break;
                case "Dark" when child.GroupName == "theme":
                    child.IsChecked = Vm.Settings.Theme == AppTheme.Dark;
                    break;
                case "system" when child.GroupName == "lang":
                    child.IsChecked = Localizer.ParseLanguage(Vm.Settings.Language) == AppLanguage.System;
                    break;
                case "zh-CN" when child.GroupName == "lang":
                    child.IsChecked = Localizer.ParseLanguage(Vm.Settings.Language) == AppLanguage.ChineseSimplified;
                    break;
                case "en" when child.GroupName == "lang":
                    child.IsChecked = Localizer.ParseLanguage(Vm.Settings.Language) == AppLanguage.English;
                    break;
            }
        }

        var first = FindFirstNavItem();
        if (first is not null) first.IsChecked = true;
    }

    private RadioButton? FindFirstNavItem()
    {
        foreach (var rb in EnumerateRadioButtons(this))
            if (rb.GroupName == "nav") return rb;
        return null;
    }

    private static IEnumerable<RadioButton> EnumerateRadioButtons(DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is RadioButton rb) yield return rb;
            foreach (var nested in EnumerateRadioButtons(child)) yield return nested;
        }
    }

    // ----------------------------------------------------------- navigation

    private void OnCategoryChecked(object sender, RoutedEventArgs e)
    {
        if (!_initialised) return;
        if (sender is RadioButton { Tag: CategoryFilter filter }) Vm.SelectedCategory = filter;
    }

    private void OnRowSelectionChanged(object sender, RoutedEventArgs e) => Vm.RaiseSelectionSummary();

    // -------------------------------------------------------------- search

    private void OnSearchGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Vm.SearchFocused = true;
    }

    private void OnSearchLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Vm.SearchFocused = false;
    }

    // ------------------------------------------------------------ sidebar width

    /// <summary>Persists the sidebar width once the user finishes dragging the divider.</summary>
    private void OnSidebarSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (SidebarColumn.ActualWidth > 0) Vm.SidebarWidth = SidebarColumn.ActualWidth;
        Vm.CommitSidebarWidth();
    }

    /// <summary>Double-clicking the divider restores the default sidebar width.</summary>
    private void OnSidebarSplitterDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Vm.ResetSidebarWidth();
        e.Handled = true;
    }

    // ------------------------------------------------------------- settings

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        // Logged before any early return: the previous report was that switching to dark
        // did nothing, so every decision on this path needs to be visible in the log.
        var tag = (sender as RadioButton)?.Tag as string;
        Vm.Log.Info($"theme radio checked: tag={tag ?? "(null)"} initialised={_initialised} " +
                    $"current={Vm.Settings.Theme}");

        if (!_initialised) return;
        if (tag is null) return;

        var theme = tag switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.System,
        };

        if (Vm.Settings.Theme == theme)
        {
            Vm.Log.Info($"theme unchanged ({theme}); nothing to do");
            return;
        }

        Vm.Settings.Theme = theme;

        // Push the change into the theme service. Without this the settings value changed
        // but ApplyTheme() was never reached, which is exactly why switching to dark
        // appeared to do nothing.
        Vm.Theme.Theme = theme;

        Vm.SaveSettings();
        Vm.Log.Info($"theme setting changed to {theme}; service theme is now {Vm.Theme.Theme} " +
                    $"effectiveDark={Vm.Theme.IsDarkEffective}");
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (!_initialised) return;
        if (sender is not RadioButton { Tag: string tag }) return;
        var language = Localizer.ParseLanguage(tag);
        if (Localizer.Instance.Language == language) return;
        Localizer.Instance.Language = language;
    }

    private void OnBrowseLogDirectory(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L.T("log.directory"),
            InitialDirectory = Directory.Exists(Vm.LogDirectory) ? Vm.LogDirectory : null,
        };

        if (dialog.ShowDialog(this) == true) Vm.LogDirectory = dialog.FolderName;
    }

    private void OnOpenLogDirectory(object sender, RoutedEventArgs e) => Vm.OpenInExplorer(Vm.LogDirectory);

    private void OnBrowseSteamRoot(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L.T("set.steamPath"),
        };

        if (dialog.ShowDialog(this) != true) return;
        Vm.SteamRootOverride = dialog.FolderName;
        _ = Vm.LoadInstallationAsync();
    }

    // -------------------------------------------------------------- row menu

    private ScanRowViewModel? SelectedRow()
    {
        if (RowList.SelectedItem is ScanRowViewModel row) return row;
        return RowList.Items.Count == 1 ? RowList.Items[0] as ScanRowViewModel : null;
    }

    private void OnOpenLocation(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is not null) Vm.OpenInExplorer(row.Path);
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow();
        if (row is not null) Vm.CopyToClipboard(row.Path);
    }

    // --------------------------------------------------------------- cleanup

    private async void OnCleanClicked(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedCount == 0)
        {
            MessageBox.Show(this, L.T("msg.nothingSelected"), L.T("app.shortTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Re-check right before acting, but only refuse when the selection actually contains
        // something the Steam client is holding.
        Vm.RefreshSteamState();
        if (Vm.IsCleanBlockedBySteam)
        {
            MessageBox.Show(this, Vm.SteamBlockedBannerText, L.T("app.shortTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Vm.Log.Warn("cleanup blocked at confirmation: selection needs Steam closed " +
                        $"({Vm.SteamBlockedCategoriesText})");
            return;
        }

        var confirm = new ConfirmCleanupWindow(Vm) { Owner = this };
        if (confirm.ShowDialog() != true || confirm.Action == CleanupAction.Cancel)
        {
            Vm.Log.Info("cleanup cancelled by user");
            return;
        }

        // The dialog decides the mode explicitly: Recycle Bin or permanent.
        var useRecycleBin = confirm.Action == CleanupAction.RecycleBin;
        var results = await Vm.CleanSelectedAsync(useRecycleBin);
        if (results.Count == 0) return;

        var resultWindow = new CleanupResultWindow(Vm, results) { Owner = this };
        resultWindow.ShowDialog();
    }
}
