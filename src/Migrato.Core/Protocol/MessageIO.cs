using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Migrato.Core.Protocol;

/// <summary>
/// Rámcování zpráv: 4 bajty délky (little-endian) + UTF-8 JSON.
/// Datové bloky souborů se posílají mimo rámce jako surový proud známé délky.
/// </summary>
public static class MessageIO
{
    public const int MaxMessageBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(Stream stream, Msg msg, CancellationToken ct = default)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(msg, Options);
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<Msg> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxMessageBytes)
            throw new InvalidDataException($"Neplatná délka zprávy: {length}");

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Msg>(payload, Options)
               ?? throw new InvalidDataException("Zprávu se nepodařilo dekódovat.");
    }

    /// <summary>Přečte zprávu a ověří očekávaný typ.</summary>
    public static async Task<Msg> ExpectAsync(Stream stream, string expectedType, CancellationToken ct = default)
    {
        Msg msg = await ReadAsync(stream, ct).ConfigureAwait(false);
        if (msg.T != expectedType)
            throw new InvalidDataException($"Očekávána zpráva '{expectedType}', přišla '{msg.T}'.");
        return msg;
    }
}
