using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using Migrato.Core.Modules;
using Migrato.Core.Net;
using Migrato.Core.Protocol;

namespace Migrato.Core.Transfer;

/// <summary>
/// Odesílací strana (starý PC): připojí se k příjemci, spáruje se PINem
/// a odešle vybrané skupiny souborů + následné akce.
/// </summary>
public sealed class SendSession(string host, int port, string pin, string? machineName = null)
{
    private const int ChunkSize = 128 * 1024;

    private readonly string _machineName = machineName ?? Environment.MachineName;

    public event Action<string>? StatusChanged;
    public event Action<TransferProgress>? ProgressChanged;

    public async Task<TransferSummary> RunAsync(
        IReadOnlyList<TransferGroup> groups, CancellationToken ct = default)
    {
        using var awake = new SleepBlocker();
        using var client = new TcpClient { NoDelay = true };
        StatusChanged?.Invoke(S.Connecting);
        await client.ConnectAsync(host, port, ct).ConfigureAwait(false);

        string serverFingerprint = "";
        await using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
            (_, certificate, _, _) =>
            {
                // Certifikát je jednorázový self-signed — pravost protistrany
                // se ověřuje PINem navázaným na tento otisk, ne řetězem důvěry.
                if (certificate is null) return false;
                serverFingerprint = TlsHelper.Fingerprint(certificate);
                return true;
            });

        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "migrato",
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        }, ct).ConfigureAwait(false);

        await MessageIO.WriteAsync(tls, new Msg
        {
            T = MsgType.Hello,
            ProtocolVersion = Discovery.Discovery.ProtocolVersion,
            AppVersion = Core.AppVersion.Current,
            Machine = _machineName,
        }, ct).ConfigureAwait(false);
        Msg hello = await MessageIO.ExpectAsync(tls, MsgType.Hello, ct).ConfigureAwait(false);
        if (hello.ProtocolVersion != Discovery.Discovery.ProtocolVersion)
            throw new PairFailedException(
                S.DecodeWireError(S.WireIncompatiblePrefix + (hello.AppVersion ?? "?")));
        if (hello.AppVersion is not null && hello.AppVersion != Core.AppVersion.Current)
            StatusChanged?.Invoke(S.VersionMismatchNote(Core.AppVersion.Current, hello.AppVersion));

        StatusChanged?.Invoke(S.PairingWith(hello.Machine));
        await MessageIO.WriteAsync(tls, new Msg
        {
            T = MsgType.Pair, Hmac = Pairing.ComputeHmac(pin, serverFingerprint),
        }, ct).ConfigureAwait(false);
        Msg pairResult = await MessageIO.ExpectAsync(tls, MsgType.PairResult, ct).ConfigureAwait(false);
        if (pairResult.Ok != true)
            throw new PairFailedException(S.DecodeWireError(pairResult.Error ?? S.PairingFailed));

        // Manifest: očíslovat soubory a posbírat následné akce.
        var items = new List<TransferItem>();
        var sourceById = new Dictionary<int, string>();
        var actions = new List<PostAction>();
        int nextId = 1;
        foreach (TransferGroup group in groups)
        {
            var groupItemIds = new List<int>();
            foreach (ScannedFile file in group.Files)
            {
                int id = nextId++;
                items.Add(new TransferItem(id, file.Category, file.RelativePath, file.Length));
                sourceById[id] = file.SourcePath;
                groupItemIds.Add(id);
            }
            if (group.PostActionType is not null)
                actions.Add(new PostAction(group.PostActionType, groupItemIds, group.Key, group.WingetId));
        }
        long totalBytes = items.Sum(i => i.Length);

        StatusChanged?.Invoke(S.SendingManifest(items.Count));
        await MessageIO.WriteAsync(tls, new Msg
        {
            T = MsgType.Manifest, Items = items, TotalBytes = totalBytes,
        }, ct).ConfigureAwait(false);

        Msg resume = await MessageIO.ExpectAsync(tls, MsgType.Resume, ct).ConfigureAwait(false);
        Dictionary<int, long> parts = resume.Parts ?? [];

        // Kontrola místa na cíli dřív, než poteče první bajt — s rezervou na
        // dočasné part soubory a práci systému.
        const long spaceMargin = 200L * 1024 * 1024;
        long toSend = totalBytes - parts.Values.Sum();
        if (resume.FreeBytes is long free && toSend + spaceMargin > free)
            throw new IOException(S.NotEnoughSpace(toSend, free));

        long bytesDone = parts.Values.Sum();
        int filesDone = 0, filesOk = 0, filesFailed = 0;
        var errors = new List<string>();

        foreach (TransferItem item in items)
        {
            ct.ThrowIfCancellationRequested();
            long offset = parts.GetValueOrDefault(item.Id, 0);

            await MessageIO.WriteAsync(tls, new Msg
            {
                T = MsgType.File, Id = item.Id, Offset = offset,
            }, ct).ConfigureAwait(false);

            long baseDone = bytesDone;
            string sha = await SendFileBytesAsync(
                tls, sourceById[item.Id], offset, item.Length,
                sent => ProgressChanged?.Invoke(new TransferProgress(
                    baseDone + sent, totalBytes, filesDone, items.Count, item.RelativePath)),
                ct).ConfigureAwait(false);

            await MessageIO.WriteAsync(tls, new Msg
            {
                T = MsgType.FileEnd, Id = item.Id, Sha256 = sha,
            }, ct).ConfigureAwait(false);

            Msg ack = await MessageIO.ExpectAsync(tls, MsgType.FileAck, ct).ConfigureAwait(false);
            if (ack.Ok == true) filesOk++;
            else
            {
                filesFailed++;
                errors.Add($"{item.RelativePath}: {ack.Error ?? S.UnknownError}");
            }
            filesDone++;
            bytesDone += item.Length - offset;
        }

        List<PostActionResult> postResults = [];
        if (actions.Count > 0)
        {
            StatusChanged?.Invoke(S.PeerRunningPostActions);
            await MessageIO.WriteAsync(tls, new Msg { T = MsgType.PostActions, Actions = actions }, ct)
                .ConfigureAwait(false);
            Msg results = await MessageIO.ExpectAsync(tls, MsgType.PostResults, ct).ConfigureAwait(false);
            postResults = results.Results ?? [];
        }

        await MessageIO.WriteAsync(tls, new Msg { T = MsgType.Done }, ct).ConfigureAwait(false);
        await MessageIO.ExpectAsync(tls, MsgType.DoneAck, ct).ConfigureAwait(false);

        StatusChanged?.Invoke(S.TransferFinished);
        return new TransferSummary(filesOk, filesFailed, bytesDone, errors, postResults);
    }

    /// <summary>
    /// Pošle přesně manifestovaný počet bajtů od daného offsetu; hash se počítá
    /// od začátku souboru, aby odpovídal tomu, co příjemce složí dohromady.
    /// </summary>
    private static async Task<string> SendFileBytesAsync(
        Stream target, string sourcePath, long offset, long manifestLength,
        Action<long> onProgress, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        byte[] buffer = new byte[ChunkSize];

        await using var file = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (file.Length < manifestLength)
            throw new IOException(S.FileShrunk(sourcePath));

        long position = 0;
        long sent = 0;
        while (position < manifestLength)
        {
            int toRead = (int)Math.Min(ChunkSize, manifestLength - position);
            int read = await file.ReadAsync(buffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
            if (read == 0) throw new IOException(S.FileEndedEarly(sourcePath));
            sha.TransformBlock(buffer, 0, read, null, 0);

            if (position + read > offset)
            {
                // Část bloku (nebo celý) už za offsetem — poslat jen novou část.
                int skip = (int)Math.Max(0, offset - position);
                await target.WriteAsync(buffer.AsMemory(skip, read - skip), ct).ConfigureAwait(false);
                sent += read - skip;
                onProgress(sent);
            }
            position += read;
        }
        await target.FlushAsync(ct).ConfigureAwait(false);

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }
}
