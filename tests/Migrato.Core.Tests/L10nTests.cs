using Migrato.Core;
using Migrato.Core.Modules;

namespace Migrato.Core.Tests;

/// <summary>Testy mění globální jazyk, proto běží sériově s ostatními (kolekce loopback).</summary>
[Collection("loopback")]
public sealed class L10nTests : IDisposable
{
    private readonly string _original = Lang.Current;

    public void Dispose() => Lang.Current = _original;

    [Fact]
    public void FolderTitles_FollowLanguage()
    {
        Lang.Current = "cs";
        Assert.Equal("Plocha", S.FolderTitle("Desktop"));
        Lang.Current = "en";
        Assert.Equal("Desktop", S.FolderTitle("Desktop"));
    }

    [Fact]
    public void WireErrors_DecodeInDisplayLanguage()
    {
        Lang.Current = "en";
        Assert.Equal("Invalid PIN.", S.DecodeWireError(S.WireInvalidPin));
        Assert.Contains("Incompatible", S.DecodeWireError(S.WireIncompatiblePrefix + "9.9.9"));
        Assert.Contains("9.9.9", S.DecodeWireError(S.WireIncompatiblePrefix + "9.9.9"));

        Lang.Current = "cs";
        Assert.Equal("Neplatný PIN.", S.DecodeWireError(S.WireInvalidPin));
        Assert.Contains("Nekompatibilní", S.DecodeWireError(S.WireIncompatiblePrefix + "9.9.9"));

        // Neznámý text projde beze změny (chyby FileAck se posílají jako prostý text).
        Assert.Equal("whatever", S.DecodeWireError("whatever"));
    }

    [Fact]
    public void AppProfiles_HaveBothDescriptions()
    {
        var profiles = AppProfileCatalog.Load();
        Assert.All(profiles, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.False(string.IsNullOrWhiteSpace(p.DescriptionEn));
        });

        Lang.Current = "en";
        Assert.Equal(profiles[0].DescriptionEn, profiles[0].LocalizedDescription);
        Lang.Current = "cs";
        Assert.Equal(profiles[0].Description, profiles[0].LocalizedDescription);
    }
}
