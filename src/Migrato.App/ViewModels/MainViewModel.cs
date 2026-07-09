using CommunityToolkit.Mvvm.ComponentModel;
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

public partial class HomeViewModel(MainViewModel main) : ObservableObject
{
    public string VersionText { get; } =
        S.T($"verze {AppVersion.Current}", $"version {AppVersion.Current}");

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Send() => main.NavigateSend();

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Receive() => main.NavigateReceive();
}
