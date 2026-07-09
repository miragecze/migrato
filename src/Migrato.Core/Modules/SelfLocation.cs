namespace Migrato.Core.Modules;

/// <summary>
/// Ochrana před přenášením a přepisováním souborů samotné aplikace Přenos —
/// uživatelé ji typicky spouštějí z Plochy nebo Stažených souborů, tedy ze složek,
/// které se zároveň přenášejí.
/// </summary>
public static class SelfLocation
{
    public static string? Directory { get; } = Path.GetDirectoryName(Environment.ProcessPath);

    public static bool Contains(string path)
    {
        if (string.IsNullOrEmpty(Directory)) return false;
        string full = Path.GetFullPath(path);
        string dir = Path.GetFullPath(Directory);
        return string.Equals(full, dir, StringComparison.OrdinalIgnoreCase)
               || full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
