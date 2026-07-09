using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Migrato.Core;
using Migrato.Core.Net;
using Migrato.Core.Transfer;

namespace Migrato.App.ViewModels;

public partial class ReceiveViewModel(MainViewModel main) : ObservableObject, IDisposable
{
    private ReceiveSession? _session;
    private CancellationTokenSource? _cts;
    private readonly SpeedMeter _speed = new();

    [ObservableProperty] private string _pin = "";
    [ObservableProperty] private string _machineName = Environment.MachineName;
    [ObservableProperty] private string _addressInfo = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private bool _isWaiting = true;
    [ObservableProperty] private bool _isTransferring;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _currentFile = "";
    [ObservableProperty] private string _summaryText = "";

    public ObservableCollection<string> PostResultLines { get; } = [];

    public string MachineInfo =>
        S.T($"Název počítače: {MachineName} • verze {AppVersion.Current}",
            $"Computer name: {MachineName} • version {AppVersion.Current}");

    public string CancelLabel => IsDone
        ? S.T("Zavřít", "Close")
        : S.T("Zrušit", "Cancel");

    partial void OnIsDoneChanged(bool value) => OnPropertyChanged(nameof(CancelLabel));

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _session = new ReceiveSession();
        Pin = _session.Pin;
        Status = S.T("Čekám, až se starý počítač připojí…", "Waiting for the old computer to connect…");

        _session.StatusChanged += s => Dispatcher.UIThread.Post(() => Status = s);
        _session.PeerPaired += _ => Dispatcher.UIThread.Post(() =>
        {
            IsWaiting = false;
            IsTransferring = true;
        });
        _session.ProgressChanged += p => Dispatcher.UIThread.Post(() =>
        {
            IsWaiting = false;
            IsTransferring = true;
            ProgressPercent = p.BytesTotal > 0 ? 100.0 * p.BytesDone / p.BytesTotal : 0;
            CurrentFile = p.CurrentFile;
            string speed = _speed.Update(p.BytesDone, p.BytesTotal);
            ProgressText = $"{Format.Bytes(p.BytesDone)} / {Format.Bytes(p.BytesTotal)}"
                           + (speed.Length > 0 ? $" • {speed}" : "");
        });

        _ = RunAsync();
        _ = FillAddressAsync();
    }

    /// <summary>Adresa pro ruční připojení — port je známý až po otevření listeneru.</summary>
    private async Task FillAddressAsync()
    {
        for (int i = 0; i < 100 && _session is { Port: 0 }; i++)
            await Task.Delay(50);
        if (_session is not { Port: > 0 } session) return;

        string? ip = LocalIp.GetPrimaryIPv4();
        if (ip is null) return;
        AddressInfo = S.T(
            $"Adresa pro ruční připojení: {ip}:{session.Port}",
            $"Address for manual connection: {ip}:{session.Port}");
    }

    private async Task RunAsync()
    {
        try
        {
            TransferSummary summary = await Task.Run(() => _session!.RunAsync(_cts!.Token));
            IsWaiting = false;
            IsTransferring = false;
            IsDone = true;
            SummaryText = S.T(
                $"Přijato {summary.FilesOk} souborů ({Format.Bytes(summary.BytesTransferred)}).",
                $"Received {summary.FilesOk} files ({Format.Bytes(summary.BytesTransferred)}).")
                + (summary.FilesFailed > 0
                    ? " " + S.T(
                        $"{summary.FilesFailed} se nepodařilo — spusťte přenos znovu, naváže se.",
                        $"{summary.FilesFailed} failed — run the transfer again, it will resume.")
                    : "");
            foreach (var r in summary.PostResults)
                PostResultLines.Add($"{(r.Ok ? "✔" : "✖")} {r.Message}");
            foreach (string e in summary.Errors.Take(10))
                PostResultLines.Add($"✖ {e}");
        }
        catch (OperationCanceledException)
        {
            // Zrušeno uživatelem — návrat řeší Cancel.
        }
        catch (Exception ex)
        {
            IsWaiting = false;
            IsTransferring = false;
            ErrorText = S.T($"Příjem selhal: {ex.Message}", $"Receiving failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel() => main.NavigateHome();

    public void Dispose()
    {
        _cts?.Cancel();
        _session?.Dispose();
        _cts?.Dispose();
    }
}
