using System.Windows;
using System.Windows.Media;
using SteamScrup.Core;

namespace SteamScrup.UI;

public partial class CleanupResultWindow : Window
{
    public CleanupResultWindow(MainViewModel vm, IReadOnlyList<DeleteResult> results)
    {
        InitializeComponent();

        var ok = results.Count(r => r.Success);
        var failed = results.Count - ok;

        var text = L.F("res.summary", ok, failed);

        if (results.Any(r => !r.Success && r.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)))
            text += Environment.NewLine + L.T("res.deniedHint");

        SummaryText.Text = text.TrimEnd();

        ResultList.ItemsSource = results
            .OrderBy(r => r.Success)
            .Select(r => new ResultRow(r))
            .ToList();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>One row in the result list, with theme-aware status colours.</summary>
    private sealed class ResultRow
    {
        private readonly DeleteResult _result;

        public ResultRow(DeleteResult result) => _result = result;

        public string Path => _result.Path;

        public string Message => _result.Message;

        public string StatusText => L.T(_result.Success ? "res.success" : "res.failed");

        public Brush StatusBackground => Resolve(_result.Success ? "OkBg" : "DangerBg");

        public Brush StatusForeground => Resolve(_result.Success ? "OkFg" : "DangerFg");

        private static Brush Resolve(string key) =>
            Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }
}
