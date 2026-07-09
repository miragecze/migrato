namespace Migrato.Core;

public static class Format
{
    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} kB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };

    public static string Eta(TimeSpan eta) => eta.TotalHours >= 1
        ? $"{(int)eta.TotalHours} h {eta.Minutes:00} min"
        : eta.TotalMinutes >= 1 ? $"{eta.Minutes} min {eta.Seconds:00} s" : $"{eta.Seconds} s";
}

/// <summary>Klouzavý výpočet rychlosti a odhadu času z průběžných událostí.</summary>
public sealed class SpeedMeter
{
    private long _lastBytes = -1;
    private DateTime _lastTime;
    private double _bytesPerSecond;

    public string Update(long bytesDone, long bytesTotal)
    {
        DateTime now = DateTime.UtcNow;
        if (_lastBytes >= 0)
        {
            double seconds = (now - _lastTime).TotalSeconds;
            if (seconds >= 0.5)
            {
                double instant = (bytesDone - _lastBytes) / seconds;
                _bytesPerSecond = _bytesPerSecond <= 0 ? instant : 0.7 * _bytesPerSecond + 0.3 * instant;
                _lastBytes = bytesDone;
                _lastTime = now;
            }
        }
        else
        {
            _lastBytes = bytesDone;
            _lastTime = now;
        }

        if (_bytesPerSecond <= 0) return "";
        string speed = $"{Format.Bytes((long)_bytesPerSecond)}/s";
        long remaining = bytesTotal - bytesDone;
        if (remaining <= 0) return speed;
        return $"{speed} • {S.T("zbývá", "remaining")} {Format.Eta(TimeSpan.FromSeconds(remaining / _bytesPerSecond))}";
    }
}
