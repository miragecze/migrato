using System.Diagnostics;
using System.Text;

namespace Migrato.Core.Modules;

public static class ExternalTools
{
    /// <summary>
    /// Spustí externí nástroj (winget, netsh) a vrátí exit kód + spojený výstup.
    /// Volitelný onLine dostává řádky výstupu průběžně — pro živý stav v UI.
    /// </summary>
    public static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName, string arguments, int timeoutSeconds = 1800, CancellationToken ct = default,
        Action<string>? onLine = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        void Collect(string? line)
        {
            if (line is null) return;
            lock (output) output.AppendLine(line);
            onLine?.Invoke(line);
        }
        process.OutputDataReceived += (_, e) => Collect(e.Data);
        process.ErrorDataReceived += (_, e) => Collect(e.Data);

        try
        {
            if (!process.Start())
                return (-1, S.ToolStartFailed(fileName));
        }
        catch (Exception ex)
        {
            return (-1, S.ToolUnavailable(fileName, ex.Message));
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* proces už mohl skončit */ }
            ct.ThrowIfCancellationRequested();
            return (-1, S.ToolTimedOut(fileName, timeoutSeconds));
        }

        lock (output) return (process.ExitCode, output.ToString());
    }

    public static bool IsProcessRunning(string processName)
    {
        try
        {
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process p in processes) p.Dispose();
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
