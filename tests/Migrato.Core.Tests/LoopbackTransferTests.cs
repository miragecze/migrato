using System.Security.Cryptography;
using Migrato.Core.Modules;
using Migrato.Core.Net;
using Migrato.Core.Transfer;

namespace Migrato.Core.Tests;

/// <summary>
/// Integrační test celého přenosu: skutečný TCP + TLS na localhostu,
/// PIN párování, manifest, resume rozpracovaného souboru i ověření obsahu.
/// Testy sdílí přesměrování kořenů, proto neběží paralelně s ničím dalším.
/// </summary>
[Collection("loopback")]
public sealed class LoopbackTransferTests : IDisposable
{
    private readonly string _srcDir;
    private readonly string _dstRoot;

    public LoopbackTransferTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "migrato-test-" + Guid.NewGuid().ToString("N")[..8]);
        _srcDir = Path.Combine(baseDir, "src");
        _dstRoot = Path.Combine(baseDir, "dst");
        Directory.CreateDirectory(_srcDir);
        Directory.CreateDirectory(_dstRoot);
        KnownFolders.Override = name => Path.Combine(_dstRoot, name);
        ProfileRoots.Override = root => Path.Combine(_dstRoot, "roots", root);
    }

    public void Dispose()
    {
        KnownFolders.Override = null;
        ProfileRoots.Override = null;
        try { Directory.Delete(Path.GetDirectoryName(_srcDir)!, recursive: true); } catch { /* úklid */ }
    }

    private string CreateSourceFile(string name, int length)
    {
        string path = Path.Combine(_srcDir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] data = new byte[length];
        RandomNumberGenerator.Fill(data);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static async Task<int> WaitForPortAsync(ReceiveSession session)
    {
        for (int i = 0; i < 100 && session.Port == 0; i++)
            await Task.Delay(50);
        Assert.True(session.Port > 0, "Příjemce neotevřel port.");
        return session.Port;
    }

    [Fact]
    public async Task FullTransfer_WithResumeAndVerification()
    {
        // Zdrojová data: velký soubor (víc bloků), vnořený soubor, prázdný soubor,
        // a soubor do "profilového" kořene.
        string big = CreateSourceFile("big.bin", 700_000);
        string nested = CreateSourceFile(Path.Combine("sub", "nested.bin"), 300_000);
        string empty = CreateSourceFile("empty.txt", 0);
        string profile = CreateSourceFile("prefs.js", 5_000);

        var group = new TransferGroup
        {
            Key = "test", Kind = "folder", Title = "Test",
            Files =
            [
                new ScannedFile(big, "folder:Desktop", "big.bin", 700_000),
                new ScannedFile(nested, "folder:Desktop", "sub/nested.bin", 300_000),
                new ScannedFile(empty, "folder:Documents", "empty.txt", 0),
                new ScannedFile(profile, "profile:thunderbird:Roaming", "Thunderbird/prefs.js", 5_000),
            ],
        };

        // Rozpracovaný stažený kus velkého souboru → musí se navázat, ne posílat znovu.
        string desktopDir = Path.Combine(_dstRoot, "Desktop");
        Directory.CreateDirectory(desktopDir);
        byte[] firstPart = (await File.ReadAllBytesAsync(big))[..250_000];
        await File.WriteAllBytesAsync(Path.Combine(desktopDir, "big.bin.migrato-part"), firstPart);

        using var receiver = new ReceiveSession("test-prijemce");
        Task<TransferSummary> receiveTask = receiver.RunAsync(CancellationToken.None);
        int port = await WaitForPortAsync(receiver);

        long resumedFrom = -1;
        var sender = new SendSession("127.0.0.1", port, receiver.Pin, "test-odesilatel");
        sender.ProgressChanged += p => { if (resumedFrom < 0) resumedFrom = p.BytesDone; };
        TransferSummary sent = await sender.RunAsync([group], CancellationToken.None);
        TransferSummary received = await receiveTask;

        Assert.Equal(4, sent.FilesOk);
        Assert.Equal(0, sent.FilesFailed);
        Assert.Equal(4, received.FilesOk);
        Assert.Empty(received.Errors);

        // Resume: první ohlášený postup musí už zahrnovat existující část.
        Assert.True(resumedFrom >= 250_000, $"Přenos nenavázal na část ({resumedFrom}).");

        // Obsah dorazil bit po bitu a part soubory zmizely.
        Assert.Equal(File.ReadAllBytes(big), File.ReadAllBytes(Path.Combine(desktopDir, "big.bin")));
        Assert.Equal(File.ReadAllBytes(nested),
            File.ReadAllBytes(Path.Combine(desktopDir, "sub", "nested.bin")));
        Assert.Equal(0, new FileInfo(Path.Combine(_dstRoot, "Documents", "empty.txt")).Length);
        Assert.Equal(File.ReadAllBytes(profile),
            File.ReadAllBytes(Path.Combine(_dstRoot, "roots", "Roaming", "Thunderbird", "prefs.js")));
        Assert.False(File.Exists(Path.Combine(desktopDir, "big.bin.migrato-part")));
    }

    [Fact]
    public async Task CustomFolder_ScansAndLandsInTransferredFolders()
    {
        // Vlastní složka s podsložkou — jako by ji uživatel vybral přes „Přidat vlastní složku“.
        string customDir = Path.Combine(_srcDir, "Projekty");
        CreateSourceFile(Path.Combine("Projekty", "plan.txt"), 5_000);
        CreateSourceFile(Path.Combine("Projekty", "2026", "data.bin"), 50_000);

        TransferGroup? group = SourceScanner.ScanCustomFolder(customDir);
        Assert.NotNull(group);
        Assert.Equal("Projekty", group.Title);
        Assert.Equal(2, group.FileCount);
        Assert.All(group.Files, f => Assert.StartsWith("Projekty/", f.RelativePath));

        using var receiver = new ReceiveSession("test-prijemce");
        Task<TransferSummary> receiveTask = receiver.RunAsync(CancellationToken.None);
        int port = await WaitForPortAsync(receiver);
        TransferSummary sent = await new SendSession("127.0.0.1", port, receiver.Pin, "t")
            .RunAsync([group], CancellationToken.None);
        TransferSummary received = await receiveTask;

        Assert.Equal(2, sent.FilesOk);
        Assert.Empty(received.Errors);
        string destRoot = Path.Combine(_dstRoot, "Desktop", Migrato.Core.S.TransferredFoldersDir, "Projekty");
        Assert.Equal(File.ReadAllBytes(Path.Combine(customDir, "plan.txt")),
            File.ReadAllBytes(Path.Combine(destRoot, "plan.txt")));
        Assert.Equal(File.ReadAllBytes(Path.Combine(customDir, "2026", "data.bin")),
            File.ReadAllBytes(Path.Combine(destRoot, "2026", "data.bin")));
    }

    [Fact]
    public async Task SecondRun_SkipsCompletedFiles_AndVerifiesThem()
    {
        string big = CreateSourceFile("big.bin", 500_000);
        string small = CreateSourceFile("small.txt", 2_000);
        var group = new TransferGroup
        {
            Key = "test", Kind = "folder", Title = "Test",
            Files =
            [
                new ScannedFile(big, "folder:Desktop", "big.bin", 500_000),
                new ScannedFile(small, "folder:Desktop", "small.txt", 2_000),
            ],
        };

        // První, kompletní přenos.
        using (var receiver1 = new ReceiveSession("test-prijemce"))
        {
            Task<TransferSummary> task1 = receiver1.RunAsync(CancellationToken.None);
            int port1 = await WaitForPortAsync(receiver1);
            await new SendSession("127.0.0.1", port1, receiver1.Pin, "t").RunAsync([group], CancellationToken.None);
            await task1;
        }

        // Druhý běh (uživatel po pádu spustil přenos znovu): hotové soubory
        // se nesmí posílat znovu — odesílatel nepošle žádná data souborů.
        using var receiver2 = new ReceiveSession("test-prijemce");
        Task<TransferSummary> task2 = receiver2.RunAsync(CancellationToken.None);
        int port2 = await WaitForPortAsync(receiver2);

        long dataEventsBytes = 0;
        var sender2 = new SendSession("127.0.0.1", port2, receiver2.Pin, "t");
        sender2.ProgressChanged += p => dataEventsBytes = p.BytesDone;
        TransferSummary sent2 = await sender2.RunAsync([group], CancellationToken.None);
        TransferSummary received2 = await task2;

        Assert.Equal(2, sent2.FilesOk);
        Assert.Equal(2, received2.FilesOk);
        Assert.Empty(received2.Errors);
        // Obsah zůstal správný a na místě.
        Assert.Equal(File.ReadAllBytes(big),
            File.ReadAllBytes(Path.Combine(_dstRoot, "Desktop", "big.bin")));
        Assert.Equal(File.ReadAllBytes(small),
            File.ReadAllBytes(Path.Combine(_dstRoot, "Desktop", "small.txt")));
    }

    [Fact]
    public async Task UnwritableFile_FailsAlone_TransferContinues()
    {
        string first = CreateSourceFile("a.txt", 20_000);
        string blocked = CreateSourceFile("bad.bin", 150_000);
        string last = CreateSourceFile("z.txt", 20_000);

        // Na cíli existuje SOUBOR jménem "blocker", takže stejnojmennou složku
        // nejde vytvořit — zápis prostředního souboru musí selhat, přenos ne.
        Directory.CreateDirectory(Path.Combine(_dstRoot, "Desktop"));
        await File.WriteAllTextAsync(Path.Combine(_dstRoot, "Desktop", "blocker"), "x");

        var group = new TransferGroup
        {
            Key = "test", Kind = "folder", Title = "Test",
            Files =
            [
                new ScannedFile(first, "folder:Desktop", "a.txt", 20_000),
                new ScannedFile(blocked, "folder:Desktop", "blocker/bad.bin", 150_000),
                new ScannedFile(last, "folder:Desktop", "z.txt", 20_000),
            ],
        };

        using var receiver = new ReceiveSession("test-prijemce");
        Task<TransferSummary> receiveTask = receiver.RunAsync(CancellationToken.None);
        int port = await WaitForPortAsync(receiver);

        var sender = new SendSession("127.0.0.1", port, receiver.Pin, "t");
        TransferSummary sent = await sender.RunAsync([group], CancellationToken.None);
        TransferSummary received = await receiveTask;

        Assert.Equal(2, sent.FilesOk);
        Assert.Equal(1, sent.FilesFailed);
        Assert.Equal(2, received.FilesOk);
        Assert.Equal(1, received.FilesFailed);
        string error = Assert.Single(received.Errors);
        Assert.Contains("blocker/bad.bin", error);

        // Soubory před chybou i po ní dorazily v pořádku.
        Assert.Equal(File.ReadAllBytes(first),
            File.ReadAllBytes(Path.Combine(_dstRoot, "Desktop", "a.txt")));
        Assert.Equal(File.ReadAllBytes(last),
            File.ReadAllBytes(Path.Combine(_dstRoot, "Desktop", "z.txt")));
    }

    [Fact]
    public async Task WrongPin_IsRejected_AndReceiverKeepsWaiting()
    {
        string file = CreateSourceFile("data.txt", 1_000);
        var group = new TransferGroup
        {
            Key = "test", Kind = "folder", Title = "Test",
            Files = [new ScannedFile(file, "folder:Desktop", "data.txt", 1_000)],
        };

        using var receiver = new ReceiveSession("test-prijemce");
        Task<TransferSummary> receiveTask = receiver.RunAsync(CancellationToken.None);
        int port = await WaitForPortAsync(receiver);

        string wrongPin = receiver.Pin == "000000" ? "000001" : "000000";
        var badSender = new SendSession("127.0.0.1", port, wrongPin, "utocnik");
        await Assert.ThrowsAsync<PairFailedException>(
            () => badSender.RunAsync([group], CancellationToken.None));

        // Příjemce po špatném PINu čeká dál — správný PIN musí projít.
        var goodSender = new SendSession("127.0.0.1", port, receiver.Pin, "test-odesilatel");
        TransferSummary sent = await goodSender.RunAsync([group], CancellationToken.None);
        TransferSummary received = await receiveTask;

        Assert.Equal(1, sent.FilesOk);
        Assert.Equal(1, received.FilesOk);
        Assert.True(File.Exists(Path.Combine(_dstRoot, "Desktop", "data.txt")));
    }
}
