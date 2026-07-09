using Migrato.Core.Modules;
using Migrato.Core.Protocol;

namespace Migrato.Core.Transfer;

/// <summary>
/// Na straně příjemce překládá kategorii + relativní cestu položky na skutečnou
/// cílovou cestu. Relativní cesty z manifestu se validují — přijímající strana
/// nesmí dovolit zápis mimo určené kořeny.
/// </summary>
public sealed class DestinationResolver(string stagingDir)
{
    public string StagingDir { get; } = stagingDir;

    public string Resolve(TransferItem item)
    {
        string rel = SanitizeRelative(item.RelativePath);

        if (item.Category.StartsWith(Categories.FolderPrefix, StringComparison.Ordinal))
        {
            string name = item.Category[Categories.FolderPrefix.Length..];
            string root = KnownFolders.Get(name)
                          ?? throw new InvalidOperationException($"Neznámá složka: {name}");
            return Path.Combine(root, rel);
        }

        if (item.Category.StartsWith(Categories.ProfilePrefix, StringComparison.Ordinal))
        {
            string[] parts = item.Category.Split(':');
            if (parts.Length != 3)
                throw new InvalidDataException($"Neplatná kategorie: {item.Category}");
            string root = ProfileRoots.Resolve(parts[2])
                          ?? throw new InvalidOperationException($"Neznámý kořen profilu: {parts[2]}");
            return Path.Combine(root, rel);
        }

        if (item.Category == Categories.Custom)
        {
            string desktop = KnownFolders.Get("Desktop")
                             ?? throw new InvalidOperationException("Plocha nenalezena.");
            return Path.Combine(desktop, S.TransferredFoldersDir, rel);
        }

        return item.Category switch
        {
            Categories.Winget => Path.Combine(StagingDir, "winget", rel),
            Categories.Wifi => Path.Combine(StagingDir, "wifi", rel),
            _ => throw new InvalidDataException($"Neznámá kategorie: {item.Category}"),
        };
    }

    /// <summary>
    /// Normalizuje relativní cestu z manifestu a odmítne pokusy o únik
    /// ("..", absolutní cesty). Oddělovač na drátě je vždy '/'.
    /// </summary>
    public static string SanitizeRelative(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidDataException("Prázdná relativní cesta.");

        string[] segments = relativePath.Split('/');
        foreach (string segment in segments)
        {
            if (segment is "" or "." or ".." || segment.Contains('\\') || Path.IsPathRooted(segment))
                throw new InvalidDataException($"Nebezpečná relativní cesta: {relativePath}");
        }
        return Path.Combine(segments);
    }
}
