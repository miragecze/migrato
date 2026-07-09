using Migrato.Core.Modules;

namespace Migrato.Core.Transfer;

/// <summary>Výběr podsložek 1. úrovně u známých složek (Dokumenty bez „Archiv“ apod.).</summary>
public static class TransferGroupFilter
{
    /// <summary>První segment relativní cesty; "" = soubor přímo v kořeni složky.</summary>
    public static string TopSegment(string relativePath)
    {
        int i = relativePath.IndexOf('/');
        return i < 0 ? "" : relativePath[..i];
    }

    /// <summary>Kopie skupiny obsahující jen soubory z vybraných segmentů (originál se nemění).</summary>
    public static TransferGroup KeepTopLevel(TransferGroup group, IReadOnlyCollection<string> keepSegments)
    {
        var keep = new HashSet<string>(keepSegments, StringComparer.OrdinalIgnoreCase);
        return new TransferGroup
        {
            Key = group.Key,
            Kind = group.Kind,
            Title = group.Title,
            Description = group.Description,
            Files = group.Files.Where(f => keep.Contains(TopSegment(f.RelativePath))).ToList(),
            WarnIfProcessRunning = group.WarnIfProcessRunning,
            PostActionType = group.PostActionType,
            WingetId = group.WingetId,
        };
    }
}
