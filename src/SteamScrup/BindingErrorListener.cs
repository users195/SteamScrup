using System.Diagnostics;
using System.Text;
using SteamScrup.Core;

namespace SteamScrup;

/// <summary>
/// Captures WPF data-binding trace output.
///
/// WPF reports binding problems (wrong mode for a read-only property, missing
/// property, converter failures) as trace messages rather than exceptions, so they
/// are invisible unless the debugger is attached. This listener routes them into the
/// operation log and, in smoke-test mode, into a report file.
/// </summary>
public sealed class BindingErrorListener : TraceListener
{
    private readonly OperationLog _log;
    private readonly List<string> _messages = new();
    private readonly StringBuilder _pending = new();

    public BindingErrorListener(OperationLog log) => _log = log;

    public IReadOnlyList<string> Messages => _messages;

    /// <summary>Attaches the listener to the WPF binding trace source.</summary>
    public static BindingErrorListener Attach(OperationLog log)
    {
        var listener = new BindingErrorListener(log);

        // PresentationTraceSources must be initialised before it can be configured.
        var source = PresentationTraceSources.DataBindingSource;
        source.Listeners.Add(listener);
        source.Switch.Level = SourceLevels.Warning;

        return listener;
    }

    public override void Write(string? message)
    {
        if (message is null) return;
        _pending.Append(message);

        // WPF writes one logical message across several Write calls; flush on nothing
        // here and let WriteLine decide when a record is complete.
    }

    public override void WriteLine(string? message)
    {
        if (message is not null) _pending.Append(message);

        var text = _pending.ToString().Trim();
        _pending.Clear();
        if (text.Length == 0) return;

        // Trace output often contains the same problem repeated per binding; keep each
        // distinct line once so the report stays readable.
        if (!_messages.Contains(text))
        {
            _messages.Add(text);
            _log.Warn("BINDING  " + text);
        }
    }

    public string BuildReport(string? extraContext = null)
    {
        var report = new StringBuilder();
        report.AppendLine("SteamScrup binding diagnostics");
        report.AppendLine($"time    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"listeners attached to: PresentationTraceSources.DataBindingSource");
        if (!string.IsNullOrEmpty(extraContext)) report.AppendLine(extraContext);
        report.AppendLine(new string('-', 72));

        if (_messages.Count == 0)
        {
            report.AppendLine("RESULT: no data-binding warnings were reported");
        }
        else
        {
            report.AppendLine($"RESULT: {_messages.Count} distinct data-binding warning(s)");
            report.AppendLine();
            foreach (var message in _messages) report.AppendLine(message);
        }

        return report.ToString();
    }
}
