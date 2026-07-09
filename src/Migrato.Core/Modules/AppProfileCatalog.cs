using System.Reflection;
using System.Text.Json;

namespace Migrato.Core.Modules;

public sealed record AppProfilePath(string Root, string Rel);

public sealed record AppProfile(
    string Key,
    string Name,
    string Description,
    string DescriptionEn,
    string? ProcessName,
    string? WingetId,
    List<AppProfilePath> Paths)
{
    public string LocalizedDescription => Lang.IsCz ? Description : DescriptionEn;
}

internal sealed record AppProfileFile(List<AppProfile> Profiles);

/// <summary>
/// Katalog přenositelných profilů aplikací. Přidání další aplikace = nový
/// záznam v app-profiles.json, žádný nový kód.
/// </summary>
public static class AppProfileCatalog
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<AppProfile> Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Migrato.Core.Modules.app-profiles.json")
            ?? throw new InvalidOperationException("Chybí vestavěný katalog app-profiles.json.");
        var file = JsonSerializer.Deserialize<AppProfileFile>(stream, Options)
            ?? throw new InvalidOperationException("Katalog profilů se nepodařilo načíst.");
        return file.Profiles;
    }
}
