using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Migrato.Core.Discovery;

public sealed record DiscoveredDevice(
    string Machine, IPAddress Address, int Port, string Fingerprint, string AppVersion)
{
    public DateTime LastSeen { get; init; } = DateTime.UtcNow;
}

internal sealed record AnnouncePayload(string App, int V, string Machine, int Port, string Fp, string? Ver);

/// <summary>
/// Příjemce (nový PC) pravidelně ohlašuje svou přítomnost UDP broadcastem,
/// odesílatel (starý PC) poslouchá a skládá seznam nalezených zařízení.
/// </summary>
public static class Discovery
{
    public const int Port = 42424;
    public const string AppId = "migrato";
    public const int ProtocolVersion = 2;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Broadcast adresy všech aktivních IPv4 rozhraní + globální broadcast.</summary>
    internal static List<IPAddress> BroadcastAddresses()
    {
        var result = new List<IPAddress> { IPAddress.Broadcast };
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] ip = ua.Address.GetAddressBytes();
                    byte[] mask = ua.IPv4Mask.GetAddressBytes();
                    var bcast = new byte[4];
                    for (int i = 0; i < 4; i++) bcast[i] = (byte)(ip[i] | ~mask[i]);
                    result.Add(new IPAddress(bcast));
                }
            }
        }
        catch
        {
            // Výčet rozhraní může selhat (oprávnění, exotická konfigurace) — globální broadcast zůstává.
        }
        return result.Distinct().ToList();
    }
}

public sealed class DiscoveryAnnouncer : IDisposable
{
    private readonly UdpClient _udp = new() { EnableBroadcast = true };
    private readonly byte[] _payload;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public DiscoveryAnnouncer(string machine, int tcpPort, string fingerprint)
    {
        var payload = new AnnouncePayload(
            Discovery.AppId, Discovery.ProtocolVersion, machine, tcpPort, fingerprint, AppVersion.Current);
        _payload = JsonSerializer.SerializeToUtf8Bytes(payload, Discovery.Json);
    }

    public void Start()
        => _loop = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                foreach (IPAddress addr in Discovery.BroadcastAddresses())
                {
                    try
                    {
                        await _udp.SendAsync(_payload, new IPEndPoint(addr, Discovery.Port), _cts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // Jednotlivé rozhraní může odeslání odmítnout — ostatní adresy to nezastaví.
                    }
                }
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        });

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ukončení */ }
        _udp.Dispose();
        _cts.Dispose();
    }
}

public sealed class DiscoveryListener : IDisposable
{
    private static readonly TimeSpan Expiry = TimeSpan.FromSeconds(5);

    private readonly UdpClient _udp;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, DiscoveredDevice> _devices = new();
    private Task? _loop;

    public DiscoveryListener()
    {
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, Discovery.Port));
    }

    /// <summary>Aktuální seznam zařízení viděných v posledních několika sekundách.</summary>
    public IReadOnlyList<DiscoveredDevice> Devices
    {
        get
        {
            DateTime cutoff = DateTime.UtcNow - Expiry;
            return _devices.Values.Where(d => d.LastSeen >= cutoff).OrderBy(d => d.Machine).ToList();
        }
    }

    public void Start()
        => _loop = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _udp.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    continue;
                }

                try
                {
                    var payload = JsonSerializer.Deserialize<AnnouncePayload>(
                        Encoding.UTF8.GetString(result.Buffer), Discovery.Json);
                    if (payload is not { App: Discovery.AppId, V: Discovery.ProtocolVersion }) continue;

                    var device = new DiscoveredDevice(
                        payload.Machine, result.RemoteEndPoint.Address, payload.Port, payload.Fp,
                        payload.Ver ?? "?");
                    _devices[$"{device.Address}:{device.Port}"] = device;
                }
                catch
                {
                    // Cizí/poškozený paket na našem portu — ignorovat.
                }
            }
        });

    public void Dispose()
    {
        _cts.Cancel();
        _udp.Dispose();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ukončení */ }
        _cts.Dispose();
    }
}
