using System.Net.Sockets;

namespace Migrato.Core.Net;

public static class SocketTuning
{
    /// <summary>
    /// TCP keepalive: uspaný nebo odpojený protějšek se pozná do ~25 sekund
    /// a blokované čtení/zápis skončí chybou — bez toho by přenos visel donekonečna.
    /// </summary>
    public static void EnableKeepAlive(Socket socket)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
        }
        catch
        {
            // Exotický systém bez podpory ladění keepalive — základní zapnutí stačí,
            // v nejhorším platí systémové výchozí intervaly.
        }
    }
}
