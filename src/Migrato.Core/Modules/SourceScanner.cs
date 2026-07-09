using Migrato.Core.Protocol;
using Migrato.Core.Transfer;

namespace Migrato.Core.Modules;

/// <summary>
/// Na zdrojovém počítači (starém PC) sestaví nabídku přenositelných skupin:
/// známé složky, profily aplikací z katalogu, seznam programů a Wi-Fi sítě.
/// </summary>
public sealed class SourceScanner(string stagingDir)
{
    public event Action<string>? StatusChanged;

    public async Task<List<TransferGroup>> ScanAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(stagingDir);
        var groups = new List<TransferGroup>();

        groups.AddRange(ScanKnownFolders(ct));
        groups.AddRange(ScanAppProfiles(ct));

        TransferGroup? winget = await CreateWingetGroupAsync(ct).ConfigureAwait(false);
        if (winget is not null) groups.Add(winget);

        TransferGroup? wifi = await CreateWifiGroupAsync(ct).ConfigureAwait(false);
        if (wifi is not null) groups.Add(wifi);

        TransferGroup? look = LookModule.Scan();
        if (look is not null) groups.Add(look);

        return groups;
    }

    private List<TransferGroup> ScanKnownFolders(CancellationToken ct)
    {
        var groups = new List<TransferGroup>();
        foreach (string name in Categories.KnownFolderNames)
        {
            ct.ThrowIfCancellationRequested();
            string? root = KnownFolders.Get(name);
            if (root is null || !Directory.Exists(root)) continue;

            StatusChanged?.Invoke(S.Scanning(S.FolderTitle(name)));
            var files = new List<ScannedFile>();
            CollectFiles(root, root, Categories.FolderPrefix + name, files, ct);
            if (files.Count == 0) continue;

            groups.Add(new TransferGroup
            {
                Key = "folder-" + name.ToLowerInvariant(),
                Kind = "folder",
                Title = S.FolderTitle(name),
                Description = root,
                Files = files,
            });
        }
        return groups;
    }

    private List<TransferGroup> ScanAppProfiles(CancellationToken ct)
    {
        var groups = new List<TransferGroup>();
        foreach (AppProfile profile in AppProfileCatalog.Load())
        {
            ct.ThrowIfCancellationRequested();
            var files = new List<ScannedFile>();

            foreach (AppProfilePath path in profile.Paths)
            {
                string? root = ProfileRoots.Resolve(path.Root);
                if (root is null) continue;

                string category = $"{Categories.ProfilePrefix}{profile.Key}:{path.Root}";
                string full = Path.Combine(root, path.Rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    var info = new FileInfo(full);
                    files.Add(new ScannedFile(full, category, path.Rel, info.Length));
                }
                else if (Directory.Exists(full))
                {
                    StatusChanged?.Invoke(S.Scanning(profile.Name));
                    CollectFiles(full, root, category, files, ct);
                }
            }
            if (files.Count == 0) continue;

            bool running = profile.ProcessName is not null
                           && ExternalTools.IsProcessRunning(profile.ProcessName);
            groups.Add(new TransferGroup
            {
                Key = "profile-" + profile.Key,
                Kind = "profile",
                Title = profile.Name,
                Description = profile.LocalizedDescription,
                Files = files,
                WarnIfProcessRunning = running ? profile.ProcessName : null,
                PostActionType = ActionType.EnsureApp,
                WingetId = profile.WingetId,
            });
        }
        return groups;
    }

    private async Task<TransferGroup?> CreateWingetGroupAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return null;
        StatusChanged?.Invoke(S.ScanningApps);

        var files = new List<ScannedFile>();

        string exportPath = Path.Combine(stagingDir, "winget-export.json");
        (int code, _) = await ExternalTools.RunAsync(
            "winget", $"export -o \"{exportPath}\" --accept-source-agreements --disable-interactivity",
            timeoutSeconds: 600, ct).ConfigureAwait(false);
        // winget export vrací nenulový kód, i když jen část programů nemá balíček —
        // rozhoduje, jestli soubor vznikl.
        if (File.Exists(exportPath))
            files.Add(new ScannedFile(
                exportPath, Categories.Winget, "winget-export.json", new FileInfo(exportPath).Length));

        List<InstalledApp> apps = InstalledApps.List();
        if (apps.Count > 0)
        {
            string reportPath = Path.Combine(stagingDir, S.ProgramsReportFile);
            await File.WriteAllTextAsync(
                reportPath, InstalledApps.RenderReport(apps, Environment.MachineName), ct).ConfigureAwait(false);
            files.Add(new ScannedFile(
                reportPath, Categories.FolderPrefix + "Desktop", S.ProgramsReportFile,
                new FileInfo(reportPath).Length));
        }

        if (files.Count == 0) return null;
        return new TransferGroup
        {
            Key = "winget",
            Kind = "winget",
            Title = S.InstalledProgramsTitle,
            Description = S.InstalledProgramsDesc(apps.Count),
            Files = files,
            PostActionType = ActionType.WingetImport,
            WingetPackages = File.Exists(exportPath) ? WingetExport.ReadPackages(exportPath) : [],
            WingetExportPath = File.Exists(exportPath) ? exportPath : null,
        };
    }

    private async Task<TransferGroup?> CreateWifiGroupAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return null;
        StatusChanged?.Invoke(S.ExportingWifi);

        string wifiDir = Path.Combine(stagingDir, "wifi");
        Directory.CreateDirectory(wifiDir);
        (int code, _) = await ExternalTools.RunAsync(
            "netsh", $"wlan export profile key=clear folder=\"{wifiDir}\"", 120, ct).ConfigureAwait(false);
        if (code != 0) return null;

        var files = Directory.EnumerateFiles(wifiDir, "*.xml")
            .Select(p => new ScannedFile(p, Categories.Wifi, Path.GetFileName(p), new FileInfo(p).Length))
            .ToList();
        if (files.Count == 0) return null;

        return new TransferGroup
        {
            Key = "wifi",
            Kind = "wifi",
            Title = S.WifiTitle,
            Description = S.WifiDesc(files.Count),
            Files = files,
            PostActionType = ActionType.WifiImport,
        };
    }

    /// <summary>
    /// Prohledá uživatelem vybranou složku. Relativní cesty začínají názvem
    /// složky (kořen je rodič), takže na cíli vznikne stejnojmenná složka.
    /// </summary>
    public static TransferGroup? ScanCustomFolder(string path, CancellationToken ct = default)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) return null;

        string root = dir.Parent?.FullName ?? dir.FullName;
        var files = new List<ScannedFile>();
        CollectFiles(dir.FullName, root, Categories.Custom, files, ct);
        if (files.Count == 0) return null;

        return new TransferGroup
        {
            Key = "custom:" + dir.FullName,
            Kind = "custom",
            Title = dir.Name,
            Description = S.CustomFolderDesc(dir.FullName),
            Files = files,
        };
    }

    /// <summary>
    /// Rekurzivně posbírá soubory; přeskakuje reparse pointy (symlinky, junctiony)
    /// a nečitelné položky — jeden zamčený soubor nesmí zastavit celý sken.
    /// </summary>
    private static void CollectFiles(
        string dir, string root, string category, List<ScannedFile> files, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(dir).EnumerateFileSystemInfos();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return;
        }

        foreach (FileSystemInfo entry in entries)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            if (SelfLocation.Contains(entry.FullName)) continue; // nepřenášet sebe sama

            if (entry is FileInfo file)
            {
                if (file.Name.EndsWith(".migrato-part", StringComparison.OrdinalIgnoreCase)) continue;
                if (file.Name is "parent.lock" or "lock") continue; // zámky Mozilla profilů
                string rel = Path.GetRelativePath(root, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
                files.Add(new ScannedFile(file.FullName, category, rel, file.Length));
            }
            else if (entry is DirectoryInfo subDir)
            {
                CollectFiles(subDir.FullName, root, category, files, ct);
            }
        }
    }
}
