using Migrato.Core.Modules;
using Migrato.Core.Transfer;

namespace Migrato.Core.Tests;

public sealed class TransferGroupFilterTests
{
    private static TransferGroup MakeGroup() => new()
    {
        Key = "folder-documents", Kind = "folder", Title = "Dokumenty",
        Files =
        [
            new ScannedFile("/src/root.txt", "folder:Documents", "root.txt", 10),
            new ScannedFile("/src/Práce/a.docx", "folder:Documents", "Práce/a.docx", 20),
            new ScannedFile("/src/Práce/x/b.xlsx", "folder:Documents", "Práce/x/b.xlsx", 30),
            new ScannedFile("/src/Archiv/stare.zip", "folder:Documents", "Archiv/stare.zip", 40),
        ],
    };

    [Fact]
    public void TopSegment_RootAndNested()
    {
        Assert.Equal("", TransferGroupFilter.TopSegment("root.txt"));
        Assert.Equal("Práce", TransferGroupFilter.TopSegment("Práce/a.docx"));
        Assert.Equal("Práce", TransferGroupFilter.TopSegment("Práce/x/b.xlsx"));
    }

    [Fact]
    public void KeepTopLevel_FiltersWithoutTouchingOriginal()
    {
        TransferGroup original = MakeGroup();
        TransferGroup filtered = TransferGroupFilter.KeepTopLevel(original, ["Práce", ""]);

        Assert.Equal(4, original.FileCount); // originál zůstává celý
        Assert.Equal(3, filtered.FileCount);
        Assert.DoesNotContain(filtered.Files, f => f.RelativePath.StartsWith("Archiv/"));
        Assert.Contains(filtered.Files, f => f.RelativePath == "root.txt");
        Assert.Equal(original.Key, filtered.Key);
    }

    [Fact]
    public void KeepTopLevel_EmptySelection_MeansNoFiles()
    {
        Assert.Empty(TransferGroupFilter.KeepTopLevel(MakeGroup(), []).Files);
    }
}
