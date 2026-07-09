using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Migrato.Core.Modules;

public static class KnownFolders
{
    /// <summary>Přesměrování kořenů — pro testy a budoucí „obnovit jinam" režim.</summary>
    public static Func<string, string?>? Override { get; set; }

    /// <summary>
    /// Vrátí skutečnou cestu známé složky. Záměrně přes API, ne pevné cesty —
    /// Plocha i Dokumenty bývají přesměrované do OneDrive.
    /// </summary>
    public static string? Get(string name)
    {
        if (Override is not null) return Override(name);
        string path = name switch
        {
            "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "Videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "Favorites" => Environment.GetFolderPath(Environment.SpecialFolder.Favorites),
            "Downloads" => GetDownloads(),
            _ => "",
        };
        return string.IsNullOrEmpty(path) ? null : path;
    }

    private static string GetDownloads()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return GetDownloadsWindows();
            }
            catch
            {
                // Spadne jen na velmi neobvyklých systémech — použije se fallback níže.
            }
        }
        string fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(fallback) ? fallback : "";
    }

    // Downloads nemá Environment.SpecialFolder — jediná spolehlivá cesta je SHGetKnownFolderPath.
    [SupportedOSPlatform("windows")]
    private static string GetDownloadsWindows()
    {
        Guid downloads = new("374DE290-123F-4565-9164-39C4925E467B");
        int hr = SHGetKnownFolderPath(ref downloads, 0, IntPtr.Zero, out IntPtr pathPtr);
        try
        {
            return hr == 0 ? Marshal.PtrToStringUni(pathPtr) ?? "" : "";
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPtr);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}

/// <summary>Kořeny pro cesty profilů aplikací — stejné klíče na zdroji i cíli.</summary>
public static class ProfileRoots
{
    /// <summary>Přesměrování kořenů — pro testy a budoucí „obnovit jinam" režim.</summary>
    public static Func<string, string?>? Override { get; set; }

    public static string? Resolve(string root)
    {
        if (Override is not null) return Override(root);
        string path = root switch
        {
            "Roaming" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Local" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Home" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            _ => "",
        };
        return string.IsNullOrEmpty(path) ? null : path;
    }
}
