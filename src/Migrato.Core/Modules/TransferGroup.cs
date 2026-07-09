namespace Migrato.Core.Modules;

/// <summary>Soubor nalezený při skenování zdroje, připravený do manifestu.</summary>
public sealed record ScannedFile(string SourcePath, string Category, string RelativePath, long Length);

/// <summary>
/// Jedna zaškrtávací položka v UI — složka, profil aplikace, seznam programů, Wi-Fi.
/// </summary>
public sealed class TransferGroup
{
    public required string Key { get; init; }
    public required string Kind { get; init; } // "folder" | "profile" | "winget" | "wifi"
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public List<ScannedFile> Files { get; init; } = [];
    public long TotalBytes => Files.Sum(f => f.Length);
    public int FileCount => Files.Count;

    /// <summary>Název procesu, který by během přenosu neměl běžet (thunderbird, firefox…).</summary>
    public string? WarnIfProcessRunning { get; init; }

    /// <summary>Typ akce, kterou má cíl po přenosu provést (ActionType.*), pokud nějakou má.</summary>
    public string? PostActionType { get; set; }
    public string? WingetId { get; init; }

    /// <summary>U skupiny „Nainstalované programy“: winget id všech nalezených balíčků (pro výběr v UI).</summary>
    public List<string> WingetPackages { get; init; } = [];

    /// <summary>Původní (nefiltrovaný) winget export — výběr programů se aplikuje vždy z něj.</summary>
    public string? WingetExportPath { get; set; }
}
