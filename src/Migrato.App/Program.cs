using Avalonia;

namespace Migrato.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Úklid po automatické aktualizaci (běžící exe se při ní přejmenovává na .old).
        try
        {
            if (Environment.ProcessPath is { } exe && File.Exists(exe + ".old"))
                File.Delete(exe + ".old");
        }
        catch
        {
            // Zamčený .old smaže některý z příštích startů.
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
