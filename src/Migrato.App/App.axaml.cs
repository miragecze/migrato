using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Migrato.App.ViewModels;
using Migrato.App.Views;

namespace Migrato.App;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainViewModel();
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

            // Instance restartovaná jako správce pokračuje rovnou na příjem.
            if (desktop.Args?.Contains("--receive") == true)
                mainViewModel.NavigateReceive();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
