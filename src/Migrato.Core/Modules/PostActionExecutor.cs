using Migrato.Core.Protocol;

namespace Migrato.Core.Modules;

/// <summary>
/// Na straně příjemce provádí akce po dokončení přenosu souborů:
/// import Wi-Fi profilů, winget import, doinstalování aplikací k přeneseným profilům.
/// </summary>
public sealed class PostActionExecutor(Func<int, string?> resolveItemPath)
{
    public async Task<List<PostActionResult>> ExecuteAsync(
        List<PostAction> actions, Action<string>? status = null, CancellationToken ct = default)
    {
        var results = new List<PostActionResult>();
        foreach (PostAction action in actions)
        {
            ct.ThrowIfCancellationRequested();
            PostActionResult result = action.Type switch
            {
                ActionType.WingetImport => await WingetImportAsync(action, status, ct).ConfigureAwait(false),
                ActionType.WifiImport => await WifiImportAsync(action, status, ct).ConfigureAwait(false),
                ActionType.EnsureApp => await EnsureAppAsync(action, status, ct).ConfigureAwait(false),
                _ => new PostActionResult(action.Type, action.AppKey, false, S.UnknownAction),
            };
            results.Add(result);
        }
        return results;
    }

    private async Task<PostActionResult> WingetImportAsync(
        PostAction action, Action<string>? status, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return new PostActionResult(action.Type, null, false, S.OnlyOnWindows);

        string? file = action.ItemIds?
            .Select(resolveItemPath)
            .FirstOrDefault(p => p is not null && p.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (file is null || !File.Exists(file))
            return new PostActionResult(action.Type, null, false, S.WingetExportMissing);

        status?.Invoke(S.InstallingPrograms);
        (int code, string output) = await ExternalTools.RunAsync(
            "winget",
            $"import -i \"{file}\" --accept-package-agreements --accept-source-agreements " +
            "--ignore-unavailable --ignore-versions --disable-interactivity",
            timeoutSeconds: 3 * 3600, ct,
            onLine: line =>
            {
                // Průběžný výstup wingetu do stavového řádku — bez něj instalace
                // desítek programů vypadá jako zamrzlá aplikace.
                string clean = Sanitize(line);
                if (clean.Length > 3) status?.Invoke($"winget: {clean}");
            }).ConfigureAwait(false);

        // winget import vrací nenulový kód i při dílčích neúspěších — soubor
        // s výstupem proto ukládáme vedle exportu pro pozdější kontrolu.
        string log = Path.ChangeExtension(file, ".log");
        try { await File.WriteAllTextAsync(log, output, ct).ConfigureAwait(false); } catch { /* jen log */ }

        return code == 0
            ? new PostActionResult(action.Type, null, true, S.ProgramsInstalled)
            : new PostActionResult(action.Type, null, false, S.ProgramsPartlyFailed);
    }

    /// <summary>Odstraní pseudo-grafiku průběhu (spinnery, \r, procenta) z řádku wingetu.</summary>
    private static string Sanitize(string line)
    {
        string last = line.Split('\r')[^1];
        var sb = new System.Text.StringBuilder(last.Length);
        foreach (char c in last)
            if (!char.IsControl(c) && c is not ('█' or '▒' or '─'))
                sb.Append(c);
        string clean = sb.ToString().Trim();
        return clean.Length > 90 ? clean[..90] + "…" : clean;
    }

    private async Task<PostActionResult> WifiImportAsync(
        PostAction action, Action<string>? status, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return new PostActionResult(action.Type, null, false, S.OnlyOnWindows);

        status?.Invoke(S.ImportingWifi);
        int ok = 0, failed = 0;
        foreach (int id in action.ItemIds ?? [])
        {
            string? file = resolveItemPath(id);
            if (file is null || !File.Exists(file)) { failed++; continue; }
            (int code, _) = await ExternalTools.RunAsync(
                "netsh", $"wlan add profile filename=\"{file}\" user=all", 60, ct).ConfigureAwait(false);
            if (code == 0) ok++; else failed++;
        }
        return failed == 0
            ? new PostActionResult(action.Type, null, true, S.WifiAdded(ok))
            : new PostActionResult(action.Type, null, ok > 0, S.WifiPartlyFailed(ok, failed));
    }

    private async Task<PostActionResult> EnsureAppAsync(
        PostAction action, Action<string>? status, CancellationToken ct)
    {
        if (action.WingetId is null)
            return new PostActionResult(action.Type, action.AppKey, true, S.NoInstallNeeded);
        if (!OperatingSystem.IsWindows())
            return new PostActionResult(action.Type, action.AppKey, false, S.OnlyOnWindows);

        (int listCode, _) = await ExternalTools.RunAsync(
            "winget", $"list --id {action.WingetId} -e --disable-interactivity", 120, ct).ConfigureAwait(false);
        if (listCode == 0)
            return new PostActionResult(action.Type, action.AppKey, true, S.AppAlreadyInstalled);

        status?.Invoke(S.InstallingApp(action.WingetId));
        (int code, _) = await ExternalTools.RunAsync(
            "winget",
            $"install --id {action.WingetId} -e --silent " +
            "--accept-package-agreements --accept-source-agreements --disable-interactivity",
            timeoutSeconds: 3600, ct).ConfigureAwait(false);

        return code == 0
            ? new PostActionResult(action.Type, action.AppKey, true, S.AppInstalled)
            : new PostActionResult(action.Type, action.AppKey, false, S.AppInstallFailed(action.WingetId));
    }
}
