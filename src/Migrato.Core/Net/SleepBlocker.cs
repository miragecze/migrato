using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Migrato.Core.Net;

/// <summary>
/// Po dobu přenosu brání Windows v uspání systému — usnutí kterékoli strany
/// by přenos přerušilo. Mimo Windows je no-op.
/// SetThreadExecutionState platí pro konkrétní vlákno, a async kód mezi vlákny
/// přeskakuje, proto stav drží vyhrazené vlákno až do Dispose.
/// </summary>
public sealed class SleepBlocker : IDisposable
{
    private readonly ManualResetEventSlim _release = new();
    private readonly Thread? _thread;

    public SleepBlocker()
    {
        if (!OperatingSystem.IsWindows()) return;
        _thread = new Thread(HoldWindows) { IsBackground = true, Name = "Migrato-SleepBlocker" };
        _thread.Start();
    }

    [SupportedOSPlatform("windows")]
    private void HoldWindows()
    {
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
        _release.Wait();
        SetThreadExecutionState(ES_CONTINUOUS);
    }

    public void Dispose()
    {
        _release.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _release.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);
}
