using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using SteamScrup.Core;
using SteamScrup.UI;

namespace SteamScrup;

/// <summary>
/// Manual entry point (no App.xaml auto-generated Main) so startup problems can be
/// logged and reported instead of silently killing the process.
/// </summary>
public static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    private const int AttachParentProcess = -1;

    [STAThread]
    public static int Main(string[] args)
    {
        var log = new OperationLog();

        // Apply the stored language before any user-facing text is produced.
        var settings = AppSettings.Load();
        Localizer.Instance.Language = Localizer.ParseLanguage(settings.Language);

        // "SteamScrup.exe --uninstall [--silent]" removes the application.
        if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            var silent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
            return Uninstaller.Run(log, silent);
        }

        // "--selftest [reportPath]" runs the whole core pipeline headlessly and exits.
        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            AttachConsole(AttachParentProcess);
            var path = args.SkipWhile(a => !a.Equals("--selftest", StringComparison.OrdinalIgnoreCase))
                           .Skip(1)
                           .FirstOrDefault();
            return SelfTest.Run(log, string.IsNullOrWhiteSpace(path) ? SelfTest.DefaultReportPath : path!);
        }

        log.Info("==========================================================");
        log.Info($"SteamScrup starting. version={typeof(Program).Assembly.GetName().Version}");
        log.Info($"OS={Environment.OSVersion} 64bit={Environment.Is64BitOperatingSystem}");
        log.Info($"Runtime={Environment.Version}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) log.Exception("UnhandledException", ex);
            else log.Error($"UnhandledException: {e.ExceptionObject}");
        };

        var app = new App(log, args);
        app.DispatcherUnhandledException += (_, e) =>
        {
            log.Exception("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(e.Exception.Message, "SteamScrup", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        try
        {
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception ex)
        {
            log.Exception("Startup failed", ex);
            MessageBox.Show(ex.ToString(), "SteamScrup — startup failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally
        {
            log.Info("SteamScrup exiting.");
            log.Dispose();
        }
    }
}
