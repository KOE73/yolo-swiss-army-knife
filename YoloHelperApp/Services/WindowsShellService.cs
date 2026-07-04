using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace YoloHelperApp.Services;

/// <summary>
/// Windows shell integration: taskbar identity (AppUserModelID), per-user *.ysak file
/// association (HKCU, no admin) and taskbar jump list "Recent" entries.
/// Every method is a no-op on non-Windows platforms.
/// </summary>
public static class WindowsShellService
{
    public const string AppUserModelId = "YSAK.YoloSwissArmyKnife";
    public const string ProgId = "YSAK.Project";

    private const uint ShardPathW = 0x00000003;          // SHARD_PATHW
    private const int ShcneAssocChanged = 0x08000000;    // SHCNE_ASSOCCHANGED
    private const uint ShcnfIdList = 0x0000;             // SHCNF_IDLIST

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    [DllImport("shell32.dll")]
    private static extern void SHAddToRecentDocs(uint uFlags, [MarshalAs(UnmanagedType.LPWStr)] string pv);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>
    /// Must run before any window is created, otherwise the jump list attaches
    /// to a generic taskbar identity instead of ours.
    /// </summary>
    public static void InitAppUserModelId()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { SetCurrentProcessExplicitAppUserModelID(AppUserModelId); } catch { }
    }

    /// <summary>Feeds the opened project into the shell's Recent items (taskbar jump list).</summary>
    public static void NotifyProjectOpened(string ysakPath)
    {
        if (!OperatingSystem.IsWindows()) return;
        try { SHAddToRecentDocs(ShardPathW, ysakPath); } catch { }
    }

    /// <summary>Exe path used for association; null when it cannot be determined (e.g. non-apphost run).</summary>
    public static string? GetExecutablePath()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return null;
        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        // "dotnet.exe" would associate .ysak with the bare runtime — refuse.
        if (string.Equals(Path.GetFileName(exe), "dotnet.exe", StringComparison.OrdinalIgnoreCase)) return null;
        return exe;
    }

    /// <summary>Registers the *.ysak association for the current user. Returns false when unsupported.</summary>
    public static bool RegisterFileAssociation()
    {
        if (!OperatingSystem.IsWindows()) return false;
        string? exe = GetExecutablePath();
        if (exe == null) return false;

        using (var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.ysak"))
        {
            extKey.SetValue(null, ProgId);
        }
        using (var progKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
        {
            progKey.SetValue(null, "YSAK Project");
            progKey.SetValue("AppUserModelID", AppUserModelId);
            using var iconKey = progKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue(null, $"\"{exe}\",0");
            using var cmdKey = progKey.CreateSubKey(@"shell\open\command");
            cmdKey.SetValue(null, $"\"{exe}\" \"%1\"");
        }

        SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    /// <summary>
    /// Returns the exe the association currently points at, or null when not registered.
    /// Callers compare against <see cref="GetExecutablePath"/> to detect stale registrations.
    /// </summary>
    public static string? GetRegisteredExecutable()
    {
        if (!OperatingSystem.IsWindows()) return null;

        using var extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.ysak");
        if (extKey?.GetValue(null) as string != ProgId) return null;

        using var cmdKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ProgId + @"\shell\open\command");
        string? command = cmdKey?.GetValue(null) as string;
        if (string.IsNullOrWhiteSpace(command)) return null;

        // Command format: "C:\path\app.exe" "%1"
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
        int space = command.IndexOf(' ');
        return space > 0 ? command[..space] : command;
    }

    public static bool IsFileAssociationRegistered()
    {
        string? registered = GetRegisteredExecutable();
        string? current = GetExecutablePath();
        return registered != null && current != null
            && string.Equals(registered, current, StringComparison.OrdinalIgnoreCase);
    }
}
