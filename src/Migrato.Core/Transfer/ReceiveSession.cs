using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Migrato.Core.Discovery;
using Migrato.Core.Modules;
using Migrato.Core.Net;
using Migrato.Core.Protocol;

namespace Migrato.Core.Transfer;

/// <summary>
/// Přijímací strana (nový PC): naslouchá, ohlašuje se v síti, ověří PIN,
/// přijme soubory a provede následné akce (winget import, Wi-Fi profily, …).
/// </summary>
public sealed class ReceiveSession : IDisposable
{
    private const int ChunkSize = 128 * 1024;
    private const string PartSuffix = ".migrato-part";

    private readonly string _machineName;
    private readonly X509Certificate2 _certificate;
    private readonly TcpListener _listener;
    private int _failedPairAttempts;

    public string Pin { get; }
    public string Fingerprint { get; }
    public int Port { get; private set; }

    public event Action<string>? StatusChanged;
    public event Action<TransferProgress>? ProgressChanged;
    public event Action<string>? PeerPaired;

    public ReceiveSession(string? machineName = null)
    {
        _machineName = machineName ?? Environment.MachineName;
        _certificate = TlsHelper.CreateEphemeralCertificate(_machineName);
        Fingerprint = TlsHelper.Fingerprint(_certificate);
        Pin = Pairing.GeneratePin();
        _listener = new TcpListener(IPAddress.Any, 0);
    }

    /// <summary>Čeká na odesílatele, obslouží celý přenos a vrátí souhrn.</summary>
    public async Task<TransferSummary> RunAsync(CancellationToken ct = default)
    {
        using var awake = new SleepBlocker();
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        using var announcer = new DiscoveryAnnouncer(_machineName, Port, Fingerprint);
        announcer.Start();
        StatusChanged?.Invoke(S.WaitingForConnection(Pin));

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            using TcpClient client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            client.NoDelay = true;
            SocketTuning.EnableKeepAlive(client.Client);

            TransferSummary? summary = await HandleClientAsync(client, ct).ConfigureAwait(false);
            if (summary is not null)
                return summary;

            if (_failedPairAttempts >= Pairing.MaxAttempts)
                throw new PairFailedException(S.TooManyPinAttempts);
        }
    }

    /// <summary>Vrátí souhrn při dokončeném přenosu, null při neúspěšném párování.</summary>
    private async Task<TransferSummary?> HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        await using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        try
        {
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            }, ct).ConfigureAwait(false);

            Msg hello = await MessageIO.ExpectAsync(tls, MsgType.Hello, ct).ConfigureAwait(false);
            await MessageIO.WriteAsync(tls, new Msg
            {
                T = MsgType.Hello,
                ProtocolVersion = Discovery.Discovery.ProtocolVersion,
                AppVersion = Core.AppVersion.Current,
                Machine = _machineName,
            }, ct).ConfigureAwait(false);

            Msg pair = await MessageIO.ExpectAsync(tls, MsgType.Pair, ct).ConfigureAwait(false);

            if (hello.ProtocolVersion != Discovery.Discovery.ProtocolVersion)
            {
                // Odpověď jde přes pairResult, aby si ji přečetly i starší verze;
                // kód si odesílatel přeloží do svého jazyka.
                await MessageIO.WriteAsync(tls, new Msg
                {
                    T = MsgType.PairResult, Ok = false,
                    Error = S.WireIncompatiblePrefix + Core.AppVersion.Current,
                }, ct).ConfigureAwait(false);
                StatusChanged?.Invoke(S.IncompatibleKeepWaiting);
                return null;
            }
            bool paired = pair.Hmac is not null && Pairing.Verify(Pin, Fingerprint, pair.Hmac);
            if (!paired)
            {
                _failedPairAttempts++;
                await MessageIO.WriteAsync(tls, new Msg
                {
                    T = MsgType.PairResult, Ok = false, Error = S.WireInvalidPin,
                }, ct).ConfigureAwait(false);
                StatusChanged?.Invoke(S.InvalidPinKeepWaiting);
                return null;
            }

            await MessageIO.WriteAsync(tls, new Msg { T = MsgType.PairResult, Ok = true }, ct)
                .ConfigureAwait(false);
            PeerPaired?.Invoke(hello.Machine ?? "?");
            StatusChanged?.Invoke(S.PairedWith(hello.Machine ?? "?"));

            return await ReceiveTransferAsync(tls, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or AuthenticationException or InvalidDataException)
        {
            StatusChanged?.Invoke(S.ConnectionFailedKeepWaiting(ex.Message));
            return null;
        }
    }

    private async Task<TransferSummary> ReceiveTransferAsync(SslStream tls, CancellationToken ct)
    {
        string stagingDir = Path.Combine(Path.GetTempPath(), "migrato-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(stagingDir);
        var resolver = new DestinationResolver(stagingDir);

        Msg manifest = await MessageIO.ExpectAsync(tls, MsgType.Manifest, ct).ConfigureAwait(false);
        List<TransferItem> items = manifest.Items ?? [];
        long totalBytes = manifest.TotalBytes ?? items.Sum(i => i.Length);
        var itemById = items.ToDictionary(i => i.Id);
        var finalPaths = new Dictionary<int, string>();

        // Rozmyslet cílové cesty předem a nahlásit rozpracované části pro resume.
        var parts = new Dictionary<int, long>();
        foreach (TransferItem item in items)
        {
            string finalPath = resolver.Resolve(item);
            finalPaths[item.Id] = finalPath;
            var part = new FileInfo(finalPath + PartSuffix);
            if (part.Exists && part.Length > 0 && part.Length <= item.Length)
            {
                parts[item.Id] = part.Length;
            }
            else if (item.Length > 0 && File.Exists(finalPath)
                     && new FileInfo(finalPath).Length == item.Length)
            {
                // Soubor už při minulém (přerušeném) běhu dorazil celý — ohlásí se jako
                // rozpracovaný v plné délce, takže se nepřenáší znovu, jen se ověří hashem.
                File.Move(finalPath, part.FullName, overwrite: true);
                parts[item.Id] = item.Length;
            }
        }
        long? freeBytes = null;
        try
        {
            string root = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!;
            freeBytes = new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            // Bez údaje o volném místě se kontrola na odesílateli prostě přeskočí.
        }
        await MessageIO.WriteAsync(tls, new Msg
        {
            T = MsgType.Resume, Parts = parts, FreeBytes = freeBytes,
        }, ct).ConfigureAwait(false);

        if (parts.Count > 0)
            StatusChanged?.Invoke(S.VerifyingResumed);

        long bytesDone = parts.Values.Sum();
        int filesDone = 0, filesOk = 0, filesFailed = 0;
        var errors = new List<string>();
        var postResults = new List<PostActionResult>();
        var failedIds = new HashSet<int>(); // odesílatel může neúspěšné soubory poslat znovu

        while (true)
        {
            Msg msg = await MessageIO.ReadAsync(tls, ct).ConfigureAwait(false);

            if (msg.T == MsgType.File)
            {
                TransferItem item = itemById[msg.Id!.Value];
                long offset = msg.Offset ?? 0;
                string finalPath = finalPaths[item.Id];
                string partPath = finalPath + PartSuffix;

                string? error = null;
                if (SelfLocation.Contains(finalPath))
                {
                    error = S.SelfFileSkipped;
                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        error = S.CannotCreateFolder(ex.Message);
                    }
                }

                // I při chybě zápisu se tělo souboru z drátu dočte (a zahodí),
                // aby selhal jen tento soubor, ne celé spojení.
                (string? actualSha, string? writeError) = await ReceiveFileBytesAsync(
                    tls, error is null ? partPath : null, offset, item.Length,
                    progress =>
                    {
                        ProgressChanged?.Invoke(new TransferProgress(
                            bytesDone + progress, totalBytes, filesDone, items.Count, item.RelativePath));
                    }, ct).ConfigureAwait(false);
                error ??= writeError;

                Msg end = await MessageIO.ExpectAsync(tls, MsgType.FileEnd, ct).ConfigureAwait(false);
                bool ok = error is null
                          && string.Equals(end.Sha256, actualSha, StringComparison.OrdinalIgnoreCase);
                if (ok)
                {
                    try
                    {
                        File.Move(partPath, finalPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        error = S.MoveToTargetFailed(ex.Message);
                    }
                }

                bool wasFailed = failedIds.Contains(item.Id);
                if (ok)
                {
                    filesOk++;
                    if (failedIds.Remove(item.Id))
                    {
                        // Opakovaný pokus uspěl — chyba prvního pokusu se ruší.
                        filesFailed--;
                        errors.RemoveAll(e => e.StartsWith(item.RelativePath + ": ", StringComparison.Ordinal));
                    }
                }
                else
                {
                    error ??= S.ChecksumMismatch;
                    TryDelete(partPath);
                    if (failedIds.Add(item.Id))
                    {
                        filesFailed++;
                        errors.Add($"{item.RelativePath}: {error}");
                    }
                }
                if (!wasFailed) filesDone++;
                bytesDone = Math.Min(bytesDone + item.Length - offset, totalBytes);
                await MessageIO.WriteAsync(tls, new Msg
                {
                    T = MsgType.FileAck, Id = item.Id, Ok = ok, Error = error,
                }, ct).ConfigureAwait(false);
            }
            else if (msg.T == MsgType.PostActions)
            {
                StatusChanged?.Invoke(S.RunningPostActions);
                var executor = new PostActionExecutor(id => finalPaths.GetValueOrDefault(id));
                postResults = await executor.ExecuteAsync(msg.Actions ?? [], StatusChanged, ct)
                    .ConfigureAwait(false);
                await MessageIO.WriteAsync(tls, new Msg { T = MsgType.PostResults, Results = postResults }, ct)
                    .ConfigureAwait(false);
            }
            else if (msg.T == MsgType.Done)
            {
                await MessageIO.WriteAsync(tls, new Msg { T = MsgType.DoneAck, Ok = true }, ct)
                    .ConfigureAwait(false);
                break;
            }
            else
            {
                throw new InvalidDataException(
                    S.T($"Neočekávaná zpráva: {msg.T}", $"Unexpected message: {msg.T}"));
            }
        }

        TryDeleteDir(stagingDir);
        StatusChanged?.Invoke(S.TransferFinished);
        return new TransferSummary(filesOk, filesFailed, bytesDone, errors, postResults);
    }

    /// <summary>
    /// Přijme tělo souboru do part souboru a vrátí SHA-256 celého obsahu
    /// (včetně již dříve přijaté části při navazování). S partPath = null jen
    /// dočte a zahodí data z drátu. Chyby zápisu nevyhazuje — vrací je, protože
    /// zbytek těla se musí vždy dočíst, jinak by se rozpadl celý protokol.
    /// </summary>
    private static async Task<(string? Sha, string? Error)> ReceiveFileBytesAsync(
        Stream source, string? partPath, long offset, long totalLength,
        Action<long> onProgress, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        byte[] buffer = new byte[ChunkSize];
        FileStream? file = null;
        string? error = null;

        if (partPath is not null)
        {
            try
            {
                file = new FileStream(
                    partPath, offset > 0 ? FileMode.Open : FileMode.Create,
                    FileAccess.ReadWrite, FileShare.None);
                if (offset > 0)
                {
                    // Hash existující části, aby výsledný součet pokryl celý soubor.
                    file.Position = 0;
                    long hashed = 0;
                    int chunksSinceProgress = 0;
                    while (hashed < offset)
                    {
                        int read = await file.ReadAsync(
                            buffer.AsMemory(0, (int)Math.Min(ChunkSize, offset - hashed)), ct)
                            .ConfigureAwait(false);
                        if (read == 0)
                            throw new IOException(S.PartFileShrunk);
                        sha.TransformBlock(buffer, 0, read, null, 0);
                        hashed += read;
                        if (++chunksSinceProgress >= 256)
                        {
                            // Ověřování velkých navázaných souborů trvá — občas
                            // obnovit UI, ať je vidět, že aplikace žije (~32 MB).
                            onProgress(0);
                            chunksSinceProgress = 0;
                        }
                    }
                    file.SetLength(offset);
                    file.Position = offset;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = S.FileWriteFailed(ex.Message);
                file?.Dispose();
                file = null;
            }
        }

        long remaining = totalLength - offset;
        long received = 0;
        try
        {
            while (remaining > 0)
            {
                int read = await source.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(ChunkSize, remaining)), ct).ConfigureAwait(false);
                if (read == 0) throw new IOException(S.ConnectionLostMidFile);
                if (file is not null)
                {
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    try
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        error = S.FileWriteFailed(ex.Message);
                        file.Dispose();
                        file = null;
                    }
                }
                remaining -= read;
                received += read;
                onProgress(received);
            }
        }
        finally
        {
            file?.Dispose();
        }

        if (error is not null || partPath is null) return (null, error);
        sha.TransformFinalBlock([], 0, 0);
        return (Convert.ToHexString(sha.Hash!), null);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* úklid — nesmí shodit přenos */ }
    }

    private static void TryDeleteDir(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* úklid — nesmí shodit přenos */ }
    }

    public void Dispose()
    {
        _listener.Dispose();
        _certificate.Dispose();
    }
}
