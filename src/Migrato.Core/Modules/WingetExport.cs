using System.Text.Json;
using System.Text.Json.Nodes;
using Migrato.Core.Transfer;

namespace Migrato.Core.Modules;

/// <summary>
/// Práce s exportem wingetu: přečtení seznamu balíčků pro výběr v UI
/// a sestavení filtrovaného exportu jen s vybranými programy.
/// </summary>
public static class WingetExport
{
    public static List<string> ReadPackages(string exportPath)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(File.ReadAllText(exportPath));
            var ids = new List<string>();
            if (root?["Sources"] is JsonArray sources)
                foreach (JsonObject source in sources.OfType<JsonObject>())
                    if (source["Packages"] is JsonArray packages)
                        foreach (JsonNode? package in packages)
                            if (package?["PackageIdentifier"]?.GetValue<string>() is { Length: > 0 } id)
                                ids.Add(id);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }
        catch
        {
            // Poškozený/nečekaný formát exportu — UI prostě nenabídne výběr po programech.
            return [];
        }
    }

    /// <summary>
    /// Přepíše položku exportu ve skupině na verzi obsahující jen vybrané balíčky.
    /// Vždy vychází z původního exportu, takže opakované volání s jiným výběrem funguje.
    /// </summary>
    public static void ApplySelection(TransferGroup group, IReadOnlyCollection<string> keepIds)
    {
        if (group.WingetExportPath is null || !File.Exists(group.WingetExportPath)) return;

        int index = group.Files.FindIndex(f => f.Category == Categories.Winget);

        if (keepIds.Count == 0)
        {
            // Žádný program — export se neposílá a na cíli se nic neinstaluje.
            if (index >= 0) group.Files.RemoveAt(index);
            group.PostActionType = null;
            return;
        }

        var keep = new HashSet<string>(keepIds, StringComparer.OrdinalIgnoreCase);
        JsonObject root = JsonNode.Parse(File.ReadAllText(group.WingetExportPath))!.AsObject();
        if (root["Sources"] is JsonArray sources)
        {
            foreach (JsonObject source in sources.OfType<JsonObject>())
            {
                if (source["Packages"] is not JsonArray packages) continue;
                for (int i = packages.Count - 1; i >= 0; i--)
                {
                    string? id = packages[i]?["PackageIdentifier"]?.GetValue<string>();
                    if (id is null || !keep.Contains(id)) packages.RemoveAt(i);
                }
            }
        }

        string filteredPath = Path.Combine(
            Path.GetDirectoryName(group.WingetExportPath)!, "winget-export-selected.json");
        File.WriteAllText(filteredPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var entry = new ScannedFile(
            filteredPath, Categories.Winget, "winget-export.json", new FileInfo(filteredPath).Length);
        if (index >= 0) group.Files[index] = entry;
        else group.Files.Add(entry);
        group.PostActionType = Protocol.ActionType.WingetImport;
    }
}
