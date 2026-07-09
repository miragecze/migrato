using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Migrato.Core;

namespace Migrato.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject _currentPage;

    public MainViewModel() => _currentPage = new HomeViewModel(this);

    public void NavigateHome()
    {
        (CurrentPage as IDisposable)?.Dispose();
        CurrentPage = new HomeViewModel(this);
    }

    public void NavigateSend() => CurrentPage = new SendViewModel(this);

    public void NavigateReceive()
    {
        var vm = new ReceiveViewModel(this);
        CurrentPage = vm;
        vm.Start();
    }
}

public partial class HomeViewModel : ObservableObject
{
    private const string RepoUrl = "https://github.com/miragecze/migrato";
    public static Uri RepoUri { get; } = new(RepoUrl);
    public static Uri LatestReleaseUri { get; } = new(RepoUrl + "/releases/latest");

    private readonly MainViewModel _main;

    /// <summary>Texty pohledu — nová instance VM po přepnutí jazyka je přenačte.</summary>
    public UI L { get; } = new();

    [ObservableProperty] private string _updateText = "";

    public string VersionText { get; } =
        S.T($"verze {AppVersion.Current}", $"version {AppVersion.Current}");

    public HomeViewModel(MainViewModel main)
    {
        _main = main;
        _ = CheckForUpdateAsync();
    }

    /// <summary>
    /// Tichá kontrola nové verze na GitHubu — při neúspěchu (offline, limit API)
    /// se prostě nic nezobrazí; aplikace na ní nijak nezávisí.
    /// </summary>
    private async Task CheckForUpdateAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Migrato/" + AppVersion.Current);
            string json = await http.GetStringAsync(
                "https://api.github.com/repos/miragecze/migrato/releases/latest");
            using JsonDocument doc = JsonDocument.Parse(json);
            string? tag = doc.RootElement.GetProperty("tag_name").GetString();
            if (tag is null) return;

            var latest = Version.Parse(tag.TrimStart('v', 'V'));
            var current = Version.Parse(AppVersion.Current);
            if (latest > current)
                Dispatcher.UIThread.Post(() => UpdateText = S.T(
                    $"⬆ Je k dispozici nová verze {latest} — klikněte pro stažení",
                    $"⬆ New version {latest} available — click to download"));
        }
        catch
        {
            // Kontrola aktualizací je jen bonus — nesmí ovlivnit start aplikace.
        }
    }

    [RelayCommand]
    private void Send() => _main.NavigateSend();

    [RelayCommand]
    private void Receive() => _main.NavigateReceive();

    public bool IsCz => Lang.IsCz;
    public bool IsEn => !Lang.IsCz;

    /// <summary>Přepne jazyk a znovu postaví úvodní obrazovku s novými texty.</summary>
    [RelayCommand]
    private void SetLanguage(string lang)
    {
        if (lang == Lang.Current) return;
        Lang.Save(lang);
        _main.NavigateHome();
    }
}
