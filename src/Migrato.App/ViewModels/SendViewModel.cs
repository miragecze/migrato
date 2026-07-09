using System.Collections.ObjectModel;
using System.Net;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Migrato.Core;
using Migrato.Core.Discovery;
using Migrato.Core.Modules;
using Migrato.Core.Net;
using Migrato.Core.Transfer;

namespace Migrato.App.ViewModels;

public sealed partial class DeviceVm(DiscoveredDevice device) : ObservableObject
{
    public DiscoveredDevice Device { get; } = device;
    public string Machine => Device.Machine;
    public string Detail => S.T($"{Device.Address} • verze {Device.AppVersion}",
                                $"{Device.Address} • version {Device.AppVersion}");
}

public sealed partial class PackageVm(string id) : ObservableObject
{
    public string Id { get; } = id;
    [ObservableProperty] private bool _isSelected = true;
}

public sealed partial class GroupVm : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;

    public required TransferGroup Model { get; init; }

    /// <summary>U skupiny programů: jednotlivé balíčky k individuálnímu výběru.</summary>
    public ObservableCollection<PackageVm> Packages { get; } = [];
    public bool HasPackages => Packages.Count > 0;
    public string PackagesHeader => S.T(
        $"Vybrat programy ({Packages.Count(p => p.IsSelected)} z {Packages.Count})",
        $"Choose programs ({Packages.Count(p => p.IsSelected)} of {Packages.Count})");

    public void NotifyPackagesChanged() => OnPropertyChanged(nameof(PackagesHeader));
    public string Icon => Model.Kind switch
    {
        "folder" => "📁",
        "custom" => "📂",
        "profile" => "🧩",
        "winget" => "📦",
        "wifi" => "📶",
        _ => "🗂",
    };
    public string Title => Model.Title;
    public string Description => Model.Description;
    public string SizeText => Model.Kind is "folder" or "profile" or "custom"
        ? S.T($"{Model.FileCount:N0} souborů • {Format.Bytes(Model.TotalBytes)}",
              $"{Model.FileCount:N0} files • {Format.Bytes(Model.TotalBytes)}")
        : Format.Bytes(Model.TotalBytes);
    public string WarningText => Model.WarnIfProcessRunning is null
        ? ""
        : S.T($"⚠ Aplikace ({Model.WarnIfProcessRunning}) právě běží — před přenosem ji zavřete.",
              $"⚠ The app ({Model.WarnIfProcessRunning}) is running — close it before transferring.");
    public bool HasWarning => Model.WarnIfProcessRunning is not null;
}

public partial class SendViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _main;
    private readonly DiscoveryListener _listener = new();
    private readonly DispatcherTimer _timer;
    private readonly SpeedMeter _speed = new();
    private List<TransferGroup>? _scannedGroups;
    private CancellationTokenSource? _transferCts;

    /// <summary>Texty pohledu — nová instance VM po přepnutí jazyka je přenačte.</summary>
    public UI L { get; } = new();

    [ObservableProperty] private bool _stepDevices = true;
    [ObservableProperty] private bool _stepPin;
    [ObservableProperty] private bool _stepScan;
    [ObservableProperty] private bool _stepSelect;
    [ObservableProperty] private bool _stepTransfer;
    [ObservableProperty] private bool _stepDone;

    [ObservableProperty] private DeviceVm? _selectedDevice;
    [ObservableProperty] private string _pinText = "";
    [ObservableProperty] private string _manualAddress = "";
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private string _scanStatus = "";
    [ObservableProperty] private string _transferStatus = "";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _currentFile = "";
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _selectedTotalText = "";
    [ObservableProperty] private string _reportInfo = "";

    private TransferSummary? _lastSummary;

    public ObservableCollection<DeviceVm> Devices { get; } = [];
    public ObservableCollection<GroupVm> Groups { get; } = [];
    public ObservableCollection<string> PostResultLines { get; } = [];

    public SendViewModel(MainViewModel main)
    {
        _main = main;
        _listener.Start();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => RefreshDevices());
        _timer.Start();
    }

    private void RefreshDevices()
    {
        if (!StepDevices) return;
        IReadOnlyList<DiscoveredDevice> found = _listener.Devices;

        for (int i = Devices.Count - 1; i >= 0; i--)
        {
            if (!found.Any(d => Same(d, Devices[i].Device)))
            {
                if (SelectedDevice == Devices[i]) SelectedDevice = null;
                Devices.RemoveAt(i);
            }
        }
        foreach (DiscoveredDevice device in found)
        {
            if (!Devices.Any(vm => Same(device, vm.Device)))
                Devices.Add(new DeviceVm(device));
        }

        static bool Same(DiscoveredDevice a, DiscoveredDevice b)
            => a.Address.Equals(b.Address) && a.Port == b.Port;
    }

    [RelayCommand]
    private void Back()
    {
        ErrorText = "";
        if (StepPin) { StepPin = false; StepDevices = true; }
        else if (StepSelect) { StepSelect = false; StepPin = true; }
        else _main.NavigateHome();
    }

    [RelayCommand]
    private void ChooseDevice()
    {
        if (SelectedDevice is null) return;
        ErrorText = "";
        StepDevices = false;
        StepPin = true;
    }

    /// <summary>Připojení bez discovery — podle „IP:port“ z obrazovky nového počítače.</summary>
    [RelayCommand]
    private void ManualConnect()
    {
        string input = ManualAddress.Trim();
        int colon = input.LastIndexOf(':');
        IPAddress? address = null;
        int port = 0;
        if (colon > 0)
        {
            IPAddress.TryParse(input[..colon].Trim('[', ']'), out address);
            int.TryParse(input[(colon + 1)..], out port);
        }
        if (address is null || port is < 1 or > 65535)
        {
            ErrorText = S.T(
                "Zadejte adresu ve tvaru IP:port — najdete ji na obrazovce nového počítače.",
                "Enter the address as IP:port — you'll find it on the new computer's screen.");
            return;
        }

        SelectedDevice = new DeviceVm(new DiscoveredDevice(input, address, port, "", "?"));
        ErrorText = "";
        StepDevices = false;
        StepPin = true;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        string pin = PinText.Trim();
        if (pin.Length != 6 || !pin.All(char.IsAsciiDigit))
        {
            ErrorText = S.T(
                "PIN má 6 číslic — najdete ho na obrazovce nového počítače.",
                "The PIN has 6 digits — you'll find it on the new computer's screen.");
            return;
        }
        ErrorText = "";
        StepPin = false;

        if (_scannedGroups is null)
        {
            StepScan = true;
            ScanStatus = S.T("Připravuji…", "Preparing…");
            string staging = Path.Combine(Path.GetTempPath(), "migrato-scan-" + Guid.NewGuid().ToString("N")[..8]);
            var scanner = new SourceScanner(staging);
            scanner.StatusChanged += s => Dispatcher.UIThread.Post(() => ScanStatus = s);
            try
            {
                _scannedGroups = await Task.Run(() => scanner.ScanAsync());
            }
            catch (Exception ex)
            {
                StepScan = false;
                StepPin = true;
                ErrorText = S.T($"Prohledání počítače selhalo: {ex.Message}",
                                $"Scanning this computer failed: {ex.Message}");
                return;
            }
            StepScan = false;

            Groups.Clear();
            foreach (TransferGroup group in _scannedGroups)
            {
                var vm = new GroupVm { Model = group };
                vm.PropertyChanged += (_, _) => UpdateSelectedTotal();
                foreach (string packageId in group.WingetPackages)
                {
                    var package = new PackageVm(packageId);
                    package.PropertyChanged += (_, _) => vm.NotifyPackagesChanged();
                    vm.Packages.Add(package);
                }
                Groups.Add(vm);
            }
        }

        UpdateSelectedTotal();
        StepSelect = true;
    }

    /// <summary>Uživatelem vybrané složky nad rámec známých — projdou stejným přenosem.</summary>
    [RelayCommand]
    private async Task AddCustomFolderAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } window })
            return;

        var picked = await window.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = L.AddCustomFolderTitle,
                AllowMultiple = true,
            });

        foreach (var folder in picked)
        {
            string? path = folder.TryGetLocalPath();
            if (path is null) continue;
            if (Groups.Any(g => g.Model.Key == "custom:" + path)) continue;

            TransferGroup? group = await Task.Run(() => SourceScanner.ScanCustomFolder(path));
            if (group is null) continue;

            var vm = new GroupVm { Model = group };
            vm.PropertyChanged += (_, _) => UpdateSelectedTotal();
            Groups.Add(vm);
        }
        UpdateSelectedTotal();
    }

    private void UpdateSelectedTotal()
    {
        List<GroupVm> selected = Groups.Where(g => g.IsSelected).ToList();
        SelectedTotalText = selected.Count == 0
            ? S.T("Nic není vybráno.", "Nothing selected.")
            : S.T($"Vybráno {selected.Count} položek • {Format.Bytes(selected.Sum(g => g.Model.TotalBytes))}",
                  $"Selected {selected.Count} items • {Format.Bytes(selected.Sum(g => g.Model.TotalBytes))}");
    }

    [RelayCommand]
    private async Task TransferAsync()
    {
        // Výběr jednotlivých programů se propíše do winget exportu (vždy z originálu,
        // takže funguje i opakovaný přenos se změněným výběrem).
        foreach (GroupVm g in Groups.Where(g => g.IsSelected && g.HasPackages))
            WingetExport.ApplySelection(
                g.Model, g.Packages.Where(p => p.IsSelected).Select(p => p.Id).ToList());

        List<TransferGroup> selected = Groups.Where(g => g.IsSelected).Select(g => g.Model).ToList();
        if (selected.Count == 0)
        {
            ErrorText = S.T("Vyberte alespoň jednu položku.", "Select at least one item.");
            return;
        }
        if (SelectedDevice is null) { Back(); return; }

        ErrorText = "";
        StepSelect = false;
        StepTransfer = true;
        TransferStatus = S.Connecting;
        _transferCts = new CancellationTokenSource();

        var session = new SendSession(
            SelectedDevice.Device.Address.ToString(), SelectedDevice.Device.Port, PinText.Trim());
        session.StatusChanged += s => Dispatcher.UIThread.Post(() => TransferStatus = s);
        session.ProgressChanged += p => Dispatcher.UIThread.Post(() =>
        {
            ProgressPercent = p.BytesTotal > 0 ? 100.0 * p.BytesDone / p.BytesTotal : 0;
            CurrentFile = p.CurrentFile;
            string speed = _speed.Update(p.BytesDone, p.BytesTotal);
            ProgressText = $"{Format.Bytes(p.BytesDone)} / {Format.Bytes(p.BytesTotal)}"
                           + (speed.Length > 0 ? $" • {speed}" : "");
        });

        try
        {
            TransferSummary summary = await Task.Run(() => session.RunAsync(selected, _transferCts.Token));
            _lastSummary = summary;
            StepTransfer = false;
            StepDone = true;
            SummaryText = S.T(
                $"Odesláno {summary.FilesOk} souborů ({Format.Bytes(summary.BytesTransferred)}).",
                $"Sent {summary.FilesOk} files ({Format.Bytes(summary.BytesTransferred)}).")
                + (summary.FilesFailed > 0
                    ? " " + S.T($"{summary.FilesFailed} se nepodařilo.", $"{summary.FilesFailed} failed.")
                    : "");
            PostResultLines.Clear();
            foreach (var r in summary.PostResults)
                PostResultLines.Add($"{(r.Ok ? "✔" : "✖")} {r.Message}");
            foreach (string e in summary.Errors.Take(10))
                PostResultLines.Add($"✖ {e}");
        }
        catch (PairFailedException ex)
        {
            StepTransfer = false;
            StepPin = true;
            ErrorText = ex.Message;
        }
        catch (OperationCanceledException)
        {
            StepTransfer = false;
            StepSelect = true;
            ErrorText = S.T("Přenos zrušen. Při dalším spuštění se naváže tam, kde skončil.",
                            "Transfer cancelled. The next run will resume where it left off.");
        }
        catch (Exception ex)
        {
            StepTransfer = false;
            StepSelect = true;
            ErrorText = S.T(
                $"Přenos selhal: {ex.Message} Zkuste to znovu — naváže se tam, kde skončil.",
                $"Transfer failed: {ex.Message} Try again — it will resume where it left off.");
        }
    }

    [RelayCommand]
    private void SaveReport()
    {
        if (_lastSummary is null) return;
        try
        {
            string path = TransferReport.SaveToDesktop(_lastSummary, sending: true);
            ReportInfo = S.T($"Protokol uložen: {path}", $"Report saved: {path}");
        }
        catch (Exception ex)
        {
            ReportInfo = S.T($"Protokol se nepodařilo uložit: {ex.Message}",
                             $"Could not save the report: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelTransfer() => _transferCts?.Cancel();

    [RelayCommand]
    private void Finish() => _main.NavigateHome();

    public void Dispose()
    {
        _timer.Stop();
        _listener.Dispose();
        _transferCts?.Cancel();
        _transferCts?.Dispose();
    }
}
