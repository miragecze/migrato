// Vyrenderuje obrazovky aplikace bez displeje (Avalonia.Headless) — pro README.
// Použití: dotnet run --project tools/Migrato.Screenshots -- <výstupní složka> [cs|en]

using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Migrato.App;
using Migrato.App.ViewModels;
using Migrato.App.Views;

string outDir = args.Length > 0 ? args[0] : "docs/screenshots";
Migrato.Core.Lang.Current = args.Length > 1 ? args[1] : "en";

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

Directory.CreateDirectory(outDir);

var main = new MainViewModel();
var window = new MainWindow { DataContext = main };
window.Show();
Pump();
Capture(window, Path.Combine(outDir, "home.png"));

// Obrazovka příjmu se skutečnou relací (PIN i adresa jsou opravdové).
main.NavigateReceive();
Pump();
Thread.Sleep(800); // čekání na otevření portu a doplnění adresy
Pump();
Capture(window, Path.Combine(outDir, "receive.png"));

window.Close();
Pump();
Console.WriteLine("OK");
return;

static void Pump() => Dispatcher.UIThread.RunJobs();

static void Capture(MainWindow window, string path)
{
    WriteableBitmap frame = window.CaptureRenderedFrame()
        ?? throw new InvalidOperationException("Okno se nevyrenderovalo.");
    frame.Save(path);
    Console.WriteLine($"saved {path}");
}
