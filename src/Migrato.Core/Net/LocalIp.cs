using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Migrato.Core.Net;

public static class LocalIp
{
    /// <summary>
    /// Nejpravděpodobnější místní IPv4 adresa — přes UDP „connect“ (nic neodesílá,
    /// jen nechá systém vybrat výchozí rozhraní), s fallbackem na výčet rozhraní.
    /// </summary>
    public static string? GetPrimaryIPv4()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint { Address: var address }
                && !IPAddress.Any.Equals(address))
                return address.ToString();
        }
        catch
        {
            // Bez výchozí trasy (izolovaná síť) — zkusí se výčet níže.
        }

        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString();
        }
        catch
        {
            return null;
        }
    }
}
