using Migrato.Core.Modules;

namespace Migrato.Core.Tests;

public sealed class RegistryModuleTests
{
    [Theory]
    [InlineData("HKCU\\Software\\SimonTatham\\PuTTY", true)]
    [InlineData("HKEY_CURRENT_USER\\Software\\7-Zip", true)]
    [InlineData("HKLM\\Software\\Microsoft", false)]
    [InlineData("HKEY_LOCAL_MACHINE\\SYSTEM", false)]
    [InlineData("HKCR\\.txt", false)]
    public void IsAllowedKey_OnlyCurrentUser(string key, bool allowed)
    {
        Assert.Equal(allowed, RegistryModule.IsAllowedKey(key));
    }

    [Fact]
    public void RegFileName_IsSafeAndDeterministic()
    {
        string name = RegistryModule.RegFileName("HKCU\\Software\\SimonTatham\\PuTTY");
        Assert.EndsWith(".reg", name);
        Assert.DoesNotContain('\\', name);
        Assert.DoesNotContain(' ', name);
        Assert.All(name[..^4], c => Assert.True(char.IsLetterOrDigit(c) || c == '_'));
        // Stejný klíč → stejný název (kvůli navazování a resume).
        Assert.Equal(name, RegistryModule.RegFileName("HKCU\\Software\\SimonTatham\\PuTTY"));
    }

    [Fact]
    public async Task ExportAsync_RejectsDisallowedRoot()
    {
        // HKLM se nikdy neexportuje, i kdyby to volající zkusil.
        bool ok = await RegistryModule.ExportAsync(
            "HKLM\\Software\\Microsoft", Path.Combine(Path.GetTempPath(), "x.reg"));
        Assert.False(ok);
    }
}

public sealed class AppProfileRegistryTests
{
    [Fact]
    public void Catalog_RegistryKeysAreValidHkcu()
    {
        var profiles = AppProfileCatalog.Load();
        var putty = profiles.Single(p => p.Key == "putty");
        Assert.NotNull(putty.RegistryKeys);
        Assert.Contains("HKCU\\Software\\SimonTatham\\PuTTY", putty.RegistryKeys!);

        // Žádný katalogový klíč nesmí sahat mimo HKCU.
        foreach (var profile in profiles)
            foreach (string key in profile.RegistryKeys ?? [])
                Assert.True(RegistryModule.IsAllowedKey(key), $"{profile.Key}: {key}");
    }

    [Fact]
    public void Catalog_RegistryOnlyAppsHaveNoPaths()
    {
        var profiles = AppProfileCatalog.Load();
        // PuTTY/7-Zip/WinRAR nemají souborový profil — jen registr.
        foreach (string key in new[] { "putty", "sevenzip", "winrar" })
        {
            var p = profiles.Single(x => x.Key == key);
            Assert.Empty(p.Paths);
            Assert.NotEmpty(p.RegistryKeys!);
        }
    }
}
