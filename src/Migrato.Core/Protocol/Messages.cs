namespace Migrato.Core.Protocol;

/// <summary>Jedna přenášená položka (soubor) v manifestu.</summary>
public sealed record TransferItem(int Id, string Category, string RelativePath, long Length);

/// <summary>Akce, kterou má cílový počítač provést po dokončení přenosu souborů.</summary>
public sealed record PostAction(string Type, List<int>? ItemIds = null, string? AppKey = null, string? WingetId = null);

public sealed record PostActionResult(string Type, string? AppKey, bool Ok, string? Message);

/// <summary>
/// Jediný tvar zprávy protokolu; typ určuje pole T. Nepoužitá pole se neserializují.
/// </summary>
public sealed class Msg
{
    public string T { get; set; } = "";

    // hello
    public int? ProtocolVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? Machine { get; set; }

    // pair / pairResult / fileAck / doneAck
    public string? Hmac { get; set; }
    public bool? Ok { get; set; }
    public string? Error { get; set; }

    // manifest / resume
    public List<TransferItem>? Items { get; set; }
    public long? TotalBytes { get; set; }
    public Dictionary<int, long>? Parts { get; set; }

    /// <summary>Volné místo na cílovém disku (resume) — nepovinné, starší verze neposílají.</summary>
    public long? FreeBytes { get; set; }

    // file / fileEnd / fileAck
    public int? Id { get; set; }
    public long? Offset { get; set; }
    public string? Sha256 { get; set; }

    // postActions / postResults
    public List<PostAction>? Actions { get; set; }
    public List<PostActionResult>? Results { get; set; }
}

public static class MsgType
{
    public const string Hello = "hello";
    public const string Pair = "pair";
    public const string PairResult = "pairResult";
    public const string Manifest = "manifest";
    public const string Resume = "resume";
    public const string File = "file";
    public const string FileEnd = "fileEnd";
    public const string FileAck = "fileAck";
    public const string PostActions = "postActions";
    public const string PostResults = "postResults";
    public const string Done = "done";
    public const string DoneAck = "doneAck";
}

public static class ActionType
{
    /// <summary>Na cíli spustit winget import z přeneseného exportu.</summary>
    public const string WingetImport = "wingetImport";

    /// <summary>Na cíli naimportovat přenesené Wi-Fi profily přes netsh.</summary>
    public const string WifiImport = "wifiImport";

    /// <summary>Ověřit, že je aplikace nainstalovaná; případně ji doinstalovat přes winget.</summary>
    public const string EnsureApp = "ensureApp";

    /// <summary>Nastavit přenesenou tapetu a zaregistrovat přenesená písma.</summary>
    public const string ApplyLook = "applyLook";

    /// <summary>Naimportovat přenesené .reg soubory (reg import) do registru uživatele.</summary>
    public const string RegistryImport = "registryImport";
}
