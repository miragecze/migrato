namespace Migrato.Core.Transfer;

/// <summary>
/// Kategorie položek určuje, kam se soubor na cílovém počítači uloží.
/// Cesty se nikdy nepřenášejí absolutně — cíl si kořeny rozkládá sám,
/// takže funguje i migrace mezi různými uživatelskými jmény a OneDrive přesměrováním.
/// </summary>
public static class Categories
{
    /// <summary>"folder:Desktop" apod. — známé složky Windows.</summary>
    public const string FolderPrefix = "folder:";

    /// <summary>"profile:thunderbird:Roaming" — profil aplikace, kořen Roaming/Local/Documents/Home.</summary>
    public const string ProfilePrefix = "profile:";

    /// <summary>Export seznamu programů pro winget import (jde do dočasné složky cíle).</summary>
    public const string Winget = "winget";

    /// <summary>XML profily Wi-Fi sítí pro netsh (jdou do dočasné složky cíle).</summary>
    public const string Wifi = "wifi";

    /// <summary>Vlastní složka vybraná uživatelem — na cíli přistane na ploše v „Přenesených složkách“.</summary>
    public const string Custom = "custom";

    /// <summary>Vzhled: tapeta (rel „wallpaper…“) a uživatelská písma (rel „fonts/…“).</summary>
    public const string Look = "look";

    public static readonly string[] KnownFolderNames =
        ["Desktop", "Documents", "Downloads", "Pictures", "Music", "Videos", "Favorites"];
}
