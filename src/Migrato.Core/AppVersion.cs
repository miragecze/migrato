using System.Reflection;

namespace Migrato.Core;

public static class AppVersion
{
    /// <summary>Verze aplikace z buildu (Directory.Build.props), bez metadat za „+“.</summary>
    public static string Current { get; } = Compute();

    private static string Compute()
    {
        string? info = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info)) return "0.0.0";
        int plus = info.IndexOf('+');
        return plus > 0 ? info[..plus] : info;
    }
}
