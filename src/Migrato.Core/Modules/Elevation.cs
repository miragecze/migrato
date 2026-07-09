using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Migrato.Core.Modules;

/// <summary>
/// Zvýšení oprávnění na příjemci: jediná UAC výzva na začátku (uživatel u počítače
/// sedí, právě kliknul), místo výzev u každé instalace na konci přenosu — ty se
/// po ~2 minutách bez odpovědi samy zavřou a instalace jimi selžou.
/// </summary>
public static class Elevation
{
    public static bool IsElevated
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return true; // mimo Windows není co řešit
            return IsElevatedWindows();
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsElevatedWindows()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Spustí novou instanci aplikace jako správce (vyvolá UAC výzvu).
    /// True = nová instance běží a tato má skončit; false = uživatel odmítl
    /// nebo spuštění selhalo — pokračuje se bez zvýšených práv.
    /// </summary>
    public static bool TryRelaunchElevated(string arguments)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (Environment.ProcessPath is not { } exe) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch
        {
            // Typicky odmítnutá UAC výzva (Win32Exception 1223).
            return false;
        }
    }
}
