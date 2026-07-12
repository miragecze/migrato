namespace Migrato.Core.Modules;

/// <summary>
/// Export a import větví registru přes nástroj reg.exe. Pracuje jen s HKCU
/// (nastavení aktuálního uživatele) — nevyžaduje oprávnění správce.
/// </summary>
public static class RegistryModule
{
    /// <summary>Povolené kořeny — aplikace nikdy neexportuje mimo uživatelský registr.</summary>
    private static readonly string[] AllowedRoots =
        ["HKCU\\", "HKEY_CURRENT_USER\\"];

    public static bool IsAllowedKey(string key)
        => AllowedRoots.Any(r => key.StartsWith(r, StringComparison.OrdinalIgnoreCase));

    /// <summary>Bezpečný název .reg souboru odvozený z klíče registru.</summary>
    public static string RegFileName(string key)
    {
        var sb = new System.Text.StringBuilder(key.Length);
        foreach (char c in key)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        string name = sb.ToString().Trim('_');
        return (name.Length > 80 ? name[..80] : name) + ".reg";
    }

    /// <summary>
    /// Vyexportuje klíč do .reg souboru. Vrací true, když soubor vznikl
    /// (klíč existuje). Neexistující klíč není chyba — jen se nepřenese.
    /// </summary>
    public static async Task<bool> ExportAsync(string key, string outPath, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows() || !IsAllowedKey(key)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        (int code, _) = await ExternalTools.RunAsync(
            "reg", $"export \"{key}\" \"{outPath}\" /y", 120, ct).ConfigureAwait(false);
        return code == 0 && File.Exists(outPath) && new FileInfo(outPath).Length > 0;
    }

    /// <summary>Naimportuje .reg soubor. Vrací true při úspěchu.</summary>
    public static async Task<bool> ImportAsync(string regPath, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(regPath)) return false;
        (int code, _) = await ExternalTools.RunAsync(
            "reg", $"import \"{regPath}\"", 120, ct).ConfigureAwait(false);
        return code == 0;
    }
}
