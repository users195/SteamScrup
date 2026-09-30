using System.Text;
using System.Windows;
using SteamScrup.Core;

namespace SteamScrup;

/// <summary>
/// Removes the application. Invoked from the Windows "Apps &amp; features" entry via
/// <c>SteamScrup.exe --uninstall</c>, so no separate uninstaller binary is needed.
/// Only files belonging to SteamScrup are removed; logs and settings survive unless
/// the user chooses to delete them.
/// </summary>
public static class Uninstaller
{
    private const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamScrup";

    public static int Run(OperationLog log, bool silent)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        log.Info("==== uninstall requested ====");
        log.Info($"install dir: {installDir}");
        log.Info($"silent: {silent}");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataDir = AppPaths.DataDirectory;
        var portable = AppPaths.IsPortable;

        var summary = new StringBuilder();
        summary.AppendLine("SteamScrup 将被移除 / SteamScrup will be removed:");
        summary.AppendLine();
        summary.AppendLine(installDir);
        summary.AppendLine(Path.Combine(dataDir, "settings.json"));
        summary.AppendLine(Path.Combine(dataDir, "logs"));
        summary.AppendLine(Path.Combine(userProfile, "Desktop", "SteamScrup.lnk"));
        summary.AppendLine();

        var proceed = silent;
        if (!silent)
        {
            var text = summary + L.T("uninstall.confirm");
            var answer = MessageBox.Show(text, L.T("uninstall.title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            proceed = answer == MessageBoxResult.Yes;
        }

        if (!proceed)
        {
            log.Info("uninstall cancelled by user");
            return 1;
        }

        RemoveShortcuts(log);
        RemoveUninstallEntry(log);

        // In portable mode settings and logs sit inside the app folder, which is
        // deleted below along with everything else.
        if (!portable) RemoveSettingsAndLogs(log, dataDir);

        // Deleting the running executable has to happen after this process exits.
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath is not null &&
                Path.GetDirectoryName(exePath)?.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(installDir, StringComparison.OrdinalIgnoreCase) == true)
            {
                ScheduleDirectoryRemoval(installDir);
                log.Info("scheduled removal of install directory after exit");
            }
            else
            {
                log.Info("not running from the install directory; leaving it untouched");
            }
        }
        catch (Exception ex)
        {
            log.Exception("scheduling removal failed", ex);
        }

        log.Info("==== uninstall finished ====");

        if (!silent)
        {
            MessageBox.Show(L.T("uninstall.done"), L.T("uninstall.title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return 0;
    }

    private static void RemoveShortcuts(OperationLog log)
    {
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.DesktopDirectory,
                     Environment.SpecialFolder.Programs,
                     Environment.SpecialFolder.CommonDesktopDirectory,
                     Environment.SpecialFolder.CommonPrograms,
                 })
        {
            try
            {
                var dir = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(dir)) continue;
                var link = Path.Combine(dir, "SteamScrup.lnk");
                if (!File.Exists(link)) continue;
                File.Delete(link);
                log.Info($"shortcut removed: {link}");
            }
            catch (Exception ex)
            {
                log.Warn($"could not remove shortcut in {folder}: {ex.Message}");
            }
        }
    }

    private static void RemoveUninstallEntry(OperationLog log)
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
            log.Info("uninstall registry entry removed");
        }
        catch (Exception ex)
        {
            log.Warn($"could not remove uninstall entry: {ex.Message}");
        }
    }

    private static void RemoveSettingsAndLogs(OperationLog log, string configDir)
    {
        // The log stream must be closed before its folder is deleted.
        log.Info("closing log before removing settings");
        log.Dispose();

        try
        {
            if (Directory.Exists(configDir))
            {
                Directory.Delete(configDir, recursive: true);
            }
        }
        catch
        {
            // Files may be locked; leaving them is harmless.
        }
    }

    /// <summary>
    /// Removes the install directory once this process is gone, using a detached
    /// cmd.exe so nothing is deleted while it is still mapped.
    /// </summary>
    private static void ScheduleDirectoryRemoval(string installDir)
    {
        var script = $"ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{installDir}\"";
        var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + script)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        };
        System.Diagnostics.Process.Start(psi);
    }
}
