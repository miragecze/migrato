using Migrato.Core.Modules;
using Migrato.Core.Protocol;

namespace Migrato.Core.Tests;

public sealed class WingetExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "migrato-winget-" + Guid.NewGuid().ToString("N")[..8]);

    public WingetExportTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* úklid */ }
    }

    private string WriteExport()
    {
        string path = Path.Combine(_dir, "winget-export.json");
        File.WriteAllText(path, """
        {
          "CreationDate": "2026-07-09T12:00:00.000-00:00",
          "Sources": [
            {
              "Packages": [
                { "PackageIdentifier": "Mozilla.Thunderbird" },
                { "PackageIdentifier": "VideoLAN.VLC" },
                { "PackageIdentifier": "Notepad++.Notepad++" }
              ],
              "SourceDetails": { "Name": "winget" }
            }
          ],
          "WinGetVersion": "1.9.0"
        }
        """);
        return path;
    }

    private TransferGroup MakeGroup(string exportPath) => new()
    {
        Key = "winget", Kind = "winget", Title = "Programy",
        Files = [new ScannedFile(exportPath, "winget", "winget-export.json", new FileInfo(exportPath).Length)],
        PostActionType = ActionType.WingetImport,
        WingetPackages = WingetExport.ReadPackages(exportPath),
        WingetExportPath = exportPath,
    };

    [Fact]
    public void ReadPackages_ReturnsSortedIds()
    {
        var ids = WingetExport.ReadPackages(WriteExport());
        Assert.Equal(["Mozilla.Thunderbird", "Notepad++.Notepad++", "VideoLAN.VLC"], ids);
    }

    [Fact]
    public void ApplySelection_FiltersExportAndUpdatesEntry()
    {
        TransferGroup group = MakeGroup(WriteExport());
        WingetExport.ApplySelection(group, ["VideoLAN.VLC"]);

        ScannedFile entry = Assert.Single(group.Files);
        Assert.EndsWith("winget-export-selected.json", entry.SourcePath);
        Assert.Equal(new FileInfo(entry.SourcePath).Length, entry.Length);
        Assert.Equal(ActionType.WingetImport, group.PostActionType);

        string json = File.ReadAllText(entry.SourcePath);
        Assert.Contains("VideoLAN.VLC", json);
        Assert.DoesNotContain("Mozilla.Thunderbird", json);
        Assert.DoesNotContain("Notepad++.Notepad++", json);

        // Nový výběr z originálu — dřív odškrtnutý balíček se může vrátit.
        WingetExport.ApplySelection(group, ["Mozilla.Thunderbird", "VideoLAN.VLC"]);
        json = File.ReadAllText(Assert.Single(group.Files).SourcePath);
        Assert.Contains("Mozilla.Thunderbird", json);
    }

    [Fact]
    public void ApplySelection_EmptySelection_DropsExportAndAction()
    {
        TransferGroup group = MakeGroup(WriteExport());
        WingetExport.ApplySelection(group, []);
        Assert.Empty(group.Files);
        Assert.Null(group.PostActionType);
    }
}
