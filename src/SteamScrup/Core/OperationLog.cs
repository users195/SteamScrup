using System.IO;
using System.Text;

namespace SteamScrup.Core;

public enum LogLevel { Debug, Info, Warn, Error }

/// <summary>
/// Append-only operation log written to a user-selectable folder, flushed after every
/// line so a crash never loses the record of what was deleted.
/// </summary>
public sealed class OperationLog : IDisposable
{
    private readonly object _gate = new();
    private readonly bool _echoToConsole;
    private StreamWriter? _writer;
    private string? _currentFile;
    private DateOnly _currentDate;

    public OperationLog(string? directory = null, bool echoToConsole = false)
    {
        _echoToConsole = echoToConsole;
        Directory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory() : directory!;
        TryOpen();
    }

    public string Directory { get; private set; }

    public string? CurrentFile => _currentFile;

    public static string DefaultDirectory() => AppPaths.LogDirectory;

    /// <summary>Points the log at a different folder and reopens the stream.</summary>
    public bool SetDirectory(string directory)
    {
        lock (_gate)
        {
            if (string.Equals(directory, Directory, StringComparison.OrdinalIgnoreCase)) return true;
            Close();
            Directory = directory;
            return TryOpen();
        }
    }

    private bool TryOpen()
    {
        lock (_gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                _currentDate = DateOnly.FromDateTime(DateTime.Now);
                _currentFile = Path.Combine(Directory, $"{_currentDate:yyyy-MM-dd}.log");
                var stream = new FileStream(_currentFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                return true;
            }
            catch
            {
                _writer = null;
                _currentFile = null;
                return false;
            }
        }
    }

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warn, message);
    public void Error(string message) => Write(LogLevel.Error, message);

    public void Exception(string context, Exception ex) =>
        Write(LogLevel.Error, $"{context}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    public void Write(LogLevel level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level.ToString().ToUpperInvariant(),-5}] {message}";

        lock (_gate)
        {
            try
            {
                // Roll over at midnight without restarting the app.
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (today != _currentDate) { Close(); TryOpen(); }

                _writer?.WriteLine(line);
            }
            catch
            {
                // A broken log must never break a cleanup run.
            }
        }

        if (_echoToConsole) Console.WriteLine(line);
    }

    private void Close()
    {
        try { _writer?.Flush(); _writer?.Dispose(); }
        catch { }
        _writer = null;
    }

    public void Dispose()
    {
        lock (_gate) Close();
    }
}
