using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Migrato.Core.Modules;

public sealed record InstalledApp(string Name, string? Version, string? Publisher);

/// <summary>
/// Seznam nainstalovaných programů z registru (klíče Uninstall) —
/// pro lidsky čitelný přehled; samotnou reinstalaci řeší winget export/import.
/// </summary>
public static class InstalledApps
{
    public static List<InstalledApp> List()
    {
        if (!OperatingSystem.IsWindows()) return [];
        return ListWindows();
    }

    [SupportedOSPlatform("windows")]
    private static List<InstalledApp> ListWindows()
    {
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        var sources = new (RegistryKey Hive, string Path)[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach ((RegistryKey hive, string path) in sources)
        {
            using RegistryKey? root = hive.OpenSubKey(path);
            if (root is null) continue;

            foreach (string subName in root.GetSubKeyNames())
            {
                using RegistryKey? sub = root.OpenSubKey(subName);
                if (sub is null) continue;

                string? name = sub.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (sub.GetValue("SystemComponent") is int sc && sc == 1) continue;
                if (sub.GetValue("ParentKeyName") is string) continue; // aktualizace, ne program

                apps[name] = new InstalledApp(
                    name.Trim(),
                    (sub.GetValue("DisplayVersion") as string)?.Trim(),
                    (sub.GetValue("Publisher") as string)?.Trim());
            }
        }

        return apps.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Textový přehled programů — přenáší se na plochu nového PC.</summary>
    public static string RenderReport(List<InstalledApp> apps, string machineName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(S.ProgramsReportHeader(machineName));
        sb.AppendLine(S.ProgramsReportGenerated(DateTime.Now));
        sb.AppendLine(new string('=', 60));
        foreach (InstalledApp app in apps)
        {
            sb.Append(app.Name);
            if (!string.IsNullOrEmpty(app.Version)) sb.Append($"  {S.ProgramsReportVersion(app.Version)}");
            if (!string.IsNullOrEmpty(app.Publisher)) sb.Append($"  — {app.Publisher}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
