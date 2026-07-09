using Migrato.Core.Net;
using Migrato.Core.Protocol;
using Migrato.Core.Transfer;

namespace Migrato.Core.Tests;

public class MessageIOTests
{
    [Fact]
    public async Task RoundTrip_PreservesFields()
    {
        using var stream = new MemoryStream();
        var msg = new Msg
        {
            T = MsgType.Manifest,
            Items = [new TransferItem(1, "folder:Desktop", "a/b.txt", 42)],
            TotalBytes = 42,
            Parts = new Dictionary<int, long> { [1] = 10 },
        };

        await MessageIO.WriteAsync(stream, msg);
        stream.Position = 0;
        Msg read = await MessageIO.ReadAsync(stream);

        Assert.Equal(MsgType.Manifest, read.T);
        Assert.NotNull(read.Items);
        var item = Assert.Single(read.Items);
        Assert.Equal(new TransferItem(1, "folder:Desktop", "a/b.txt", 42), item);
        Assert.Equal(42, read.TotalBytes);
        Assert.Equal(10, read.Parts![1]);
    }

    [Fact]
    public async Task Read_RejectsOversizedMessage()
    {
        using var stream = new MemoryStream([0xFF, 0xFF, 0xFF, 0x7F, 0x00]);
        await Assert.ThrowsAsync<InvalidDataException>(() => MessageIO.ReadAsync(stream));
    }

    [Fact]
    public async Task Expect_ThrowsOnWrongType()
    {
        using var stream = new MemoryStream();
        await MessageIO.WriteAsync(stream, new Msg { T = MsgType.Done });
        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => MessageIO.ExpectAsync(stream, MsgType.Hello));
    }
}

public class PairingTests
{
    [Fact]
    public void Pin_IsSixDigits()
    {
        for (int i = 0; i < 100; i++)
        {
            string pin = Pairing.GeneratePin();
            Assert.Equal(6, pin.Length);
            Assert.All(pin, c => Assert.InRange(c, '0', '9'));
        }
    }

    [Fact]
    public void Verify_AcceptsCorrectPin()
    {
        string hmac = Pairing.ComputeHmac("123456", "ABCDEF");
        Assert.True(Pairing.Verify("123456", "ABCDEF", hmac));
    }

    [Fact]
    public void Verify_RejectsWrongPin()
    {
        string hmac = Pairing.ComputeHmac("123456", "ABCDEF");
        Assert.False(Pairing.Verify("654321", "ABCDEF", hmac));
    }

    [Fact]
    public void Verify_RejectsHmacBoundToDifferentCertificate()
    {
        // Útočník uprostřed má jiný certifikát → jiný otisk → HMAC neprojde.
        string hmac = Pairing.ComputeHmac("123456", "OTISK-UTOCNIKA");
        Assert.False(Pairing.Verify("123456", "OTISK-PRAVEHO-SERVERU", hmac));
    }

    [Fact]
    public void Verify_RejectsMalformedHex()
    {
        Assert.False(Pairing.Verify("123456", "ABCDEF", "toto-neni-hex"));
    }
}

public class DestinationResolverTests
{
    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("a/../../b")]
    [InlineData("/absolute")]
    [InlineData("a//b")]
    [InlineData("")]
    [InlineData("a\\b")]
    public void Sanitize_RejectsEscapeAttempts(string path)
    {
        Assert.Throws<InvalidDataException>(() => DestinationResolver.SanitizeRelative(path));
    }

    [Fact]
    public void Sanitize_AcceptsNormalPath()
    {
        string result = DestinationResolver.SanitizeRelative("Thunderbird/Profiles/abc.default/prefs.js");
        Assert.Equal(Path.Combine("Thunderbird", "Profiles", "abc.default", "prefs.js"), result);
    }

    [Fact]
    public void Resolve_WingetGoesToStaging()
    {
        var resolver = new DestinationResolver("/tmp/staging");
        string path = resolver.Resolve(new TransferItem(1, "winget", "winget-export.json", 1));
        Assert.Equal(Path.Combine("/tmp/staging", "winget", "winget-export.json"), path);
    }

    [Fact]
    public void Resolve_RejectsUnknownCategory()
    {
        var resolver = new DestinationResolver("/tmp/staging");
        Assert.Throws<InvalidDataException>(
            () => resolver.Resolve(new TransferItem(1, "hacky:category", "x", 1)));
    }
}

public class AppProfileCatalogTests
{
    [Fact]
    public void Catalog_LoadsAndContainsThunderbird()
    {
        var profiles = Migrato.Core.Modules.AppProfileCatalog.Load();
        Assert.NotEmpty(profiles);
        var thunderbird = profiles.Single(p => p.Key == "thunderbird");
        Assert.Equal("Mozilla.Thunderbird", thunderbird.WingetId);
        Assert.All(profiles, p => Assert.NotEmpty(p.Paths));
        Assert.All(profiles.SelectMany(p => p.Paths),
            p => Assert.Contains(p.Root, new[] { "Roaming", "Local", "Documents", "Home" }));
    }
}
