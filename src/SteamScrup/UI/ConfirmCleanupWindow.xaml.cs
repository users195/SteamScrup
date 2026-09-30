using System.Windows;
using SteamScrup.Core;

namespace SteamScrup.UI;

/// <summary>What the user chose in the confirmation dialog.</summary>
public enum CleanupAction
{
    Cancel,
    RecycleBin,
    Permanent,
}

/// <summary>
/// Second gate before anything is removed. Offers the two deletion modes as explicit
/// buttons rather than inferring the mode from a setting, so the destructive choice is
/// always deliberate.
///
/// All text is assigned in the constructor: the window has no DataContext, and binding its
/// contents previously produced buttons with no captions at all.
/// </summary>
public partial class ConfirmCleanupWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly List<ScanRowViewModel> _reviewItems;
    private readonly int _userDataCount;

    public ConfirmCleanupWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        _reviewItems = vm.Rows
            .Where(r => r.IsSelected && r.Confidence == Confidence.Review)
            .OrderByDescending(r => r.SizeBytes)
            .ToList();

        // Save data gets its own warning: losing progress is a different kind of mistake from
        // deleting a regenerable cache, and it is not reliably reversible.
        _userDataCount = vm.Rows.Count(r => r.IsSelected && r.Category == ScanCategory.UserData);

        ApplyText();
        PopulateUserDataWarning();
        PopulateRiskList();
        UpdateButtonStates();
    }

    /// <summary>The chosen outcome. Cancel unless a button was pressed.</summary>
    public CleanupAction Action { get; private set; } = CleanupAction.Cancel;

    private void ApplyText()
    {
        Title = L.T("dlg.confirmTitle");
        TitleText.Text = L.T("dlg.confirmTitle");
        SummaryText.Text = L.F("dlg.confirmBody", _vm.SelectedCount, _vm.SelectedSizeText);

        HintText.Text = L.T("dlg.pickAction");
        CancelButton.Content = L.T("dlg.cancel");
        RecycleButton.Content = L.T("dlg.moveToRecycle");
        PermanentButton.Content = L.T("dlg.deletePermanent");
    }

    private void PopulateUserDataWarning()
    {
        if (_userDataCount == 0) return;

        UserDataPanel.Visibility = Visibility.Visible;
        UserDataTitle.Text = L.T("dlg.userdataWarningTitle");
        UserDataBody.Text = L.F("dlg.userdataWarningBody", _userDataCount);
    }

    private void PopulateRiskList()
    {
        if (_reviewItems.Count == 0) return;

        RiskPanel.Visibility = Visibility.Visible;
        RiskTitle.Text = L.F("dlg.confirmRiskBody", _reviewItems.Count);
        RiskList.ItemsSource = _reviewItems
            .Take(60)
            .Select(r => $"{r.SizeText,-10}  {r.Path}")
            .ToList();
    }

    /// <summary>
    /// Which outcomes need the acknowledgement box.
    ///
    /// Going to the Recycle Bin is normally reversible and stays a single click, except when
    /// the selection contains save data: there the bin is not a dependable undo, because
    /// Steam Cloud can write saves back and some deletions never reach the bin at all.
    /// Permanent deletion is always gated when high-risk items are selected.
    /// </summary>
    private void UpdateButtonStates()
    {
        var ackRequiredForRecycle = _userDataCount > 0;
        var ackRequiredForPermanent = _userDataCount > 0 || _reviewItems.Count > 0;
        var acknowledgementVisible = ackRequiredForRecycle || ackRequiredForPermanent;

        if (acknowledgementVisible)
        {
            RiskAck.Visibility = Visibility.Visible;
            RiskAckLabel.Visibility = Visibility.Visible;
            RiskAckLabel.Text = _userDataCount > 0
                ? L.T("dlg.userdataAck")
                : L.T("dlg.confirmRiskAck");
        }

        var acknowledged = RiskAck.IsChecked == true;

        RecycleButton.IsEnabled = !ackRequiredForRecycle || acknowledged;
        PermanentButton.IsEnabled = !ackRequiredForPermanent || acknowledged;

        HintText.Text = acknowledgementVisible && !acknowledged
            ? L.T(_userDataCount > 0 ? "dlg.userdataNeedsAck" : "dlg.permanentNeedsAck")
            : L.T("dlg.pickAction");
    }

    private void OnAckChanged(object sender, RoutedEventArgs e) => UpdateButtonStates();

    private void OnCancel(object sender, RoutedEventArgs e) => Finish(CleanupAction.Cancel);

    private void OnRecycle(object sender, RoutedEventArgs e) => Finish(CleanupAction.RecycleBin);

    private void OnPermanent(object sender, RoutedEventArgs e)
    {
        // Setting this window's own delete mode would change the user's preference, so the
        // choice is carried out by the caller instead.
        Finish(CleanupAction.Permanent);
    }

    private void Finish(CleanupAction action)
    {
        Action = action;

        _vm.Log.Info($"cleanup confirmed: action={action} items={_vm.SelectedCount} " +
                     $"review={_reviewItems.Count} acknowledged={RiskAck.IsChecked == true}");

        if (action == CleanupAction.Permanent)
            foreach (var item in _reviewItems)
                _vm.Log.Warn($"  permanently deleting high-risk target: {item.Path} ({item.SizeText})");

        DialogResult = action != CleanupAction.Cancel;
        Close();
    }
}
