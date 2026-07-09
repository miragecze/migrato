using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Migrato.Core.Transfer;

namespace Migrato.Core.Modules;

/// <summary>
/// Vzhled: tapeta plochy (cesta z registru) a uživatelská písma
/// (LocalAppData\Microsoft\Windows\Fonts — instalované bez oprávnění správce).
/// </summary>
public static class LookModule
{
    public static TransferGroup? Scan()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var files = new List<ScannedFile>();
        bool wallpaper = false;

        string? wallpaperPath = ReadWallpaperPath();
        if (wallpaperPath is not null && File.Exists(wallpaperPath))
        {
            files.Add(new ScannedFile(
                wallpaperPath, Categories.Look,
                "wallpaper" + Path.GetExtension(wallpaperPath).ToLowerInvariant(),
                new FileInfo(wallpaperPath).Length));
            wallpaper = true;
        }

        string fontsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");
        int fontCount = 0;
        if (Directory.Exists(fontsDir))
        {
            foreach (string font in Directory.EnumerateFiles(fontsDir))
            {
                string ext = Path.GetExtension(font).ToLowerInvariant();
                if (ext is not (".ttf" or ".otf" or ".ttc")) continue;
                files.Add(new ScannedFile(
                    font, Categories.Look, "fonts/" + Path.GetFileName(font), new FileInfo(font).Length));
                fontCount++;
            }
        }

        if (files.Count == 0) return null;
        return new TransferGroup
        {
            Key = "look",
            Kind = "look",
            Title = S.LookTitle,
            Description = S.LookDesc(wallpaper, fontCount),
            Files = files,
            PostActionType = Protocol.ActionType.ApplyLook,
        };
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadWallpaperPath()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", null) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Na cíli: nastaví tapetu a zaregistruje písma pro aktuálního uživatele.</summary>
    [SupportedOSPlatform("windows")]
    public static (bool WallpaperSet, int FontsRegistered) Apply(IEnumerable<string> itemPaths)
    {
        bool wallpaperSet = false;
        int fonts = 0;

        using RegistryKey fontsKey = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows NT\CurrentVersion\Fonts");

        foreach (string path in itemPaths)
        {
            if (!File.Exists(path)) continue;
            string name = Path.GetFileName(path);

            if (Path.GetFileNameWithoutExtension(name).Equals("wallpaper", StringComparison.OrdinalIgnoreCase))
            {
                // SPI_SETDESKWALLPAPER + uložit do profilu + rozeslat změnu.
                wallpaperSet = SystemParametersInfo(0x0014, 0, path, 0x01 | 0x02);
            }
            else
            {
                // Per-user písmo: registrace plnou cestou (jak to dělá Windows 1809+)
                // + načtení do aktuální relace. Název odvozený ze souboru stačí.
                fontsKey.SetValue($"{Path.GetFileNameWithoutExtension(name)} (TrueType)", path);
                AddFontResource(path);
                fonts++;
            }
        }
        return (wallpaperSet, fonts);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResource(string lpFileName);
}
