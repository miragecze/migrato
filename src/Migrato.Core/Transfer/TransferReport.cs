using System.Text;

namespace Migrato.Core.Transfer;

/// <summary>Textový protokol o dokončeném přenosu — k uložení na plochu.</summary>
public static class TransferReport
{
    public static string Build(TransferSummary summary, bool sending)
    {
        var sb = new StringBuilder();
        sb.AppendLine(S.ReportTitle);
        sb.AppendLine($"Migrato {AppVersion.Current} • {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine(sending ? S.ReportDirectionSent : S.ReportDirectionReceived);
        sb.AppendLine(new string('=', 60));
        sb.AppendLine(S.ReportSummary(summary.FilesOk, summary.FilesFailed, summary.BytesTransferred));

        if (summary.PostResults.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(S.ReportPostActions);
            foreach (var result in summary.PostResults)
                sb.AppendLine($"  {(result.Ok ? "OK " : "!! ")}{result.Message}");
        }

        sb.AppendLine();
        if (summary.Errors.Count == 0)
        {
            sb.AppendLine(S.ReportNoErrors);
        }
        else
        {
            sb.AppendLine(S.ReportErrors);
            foreach (string error in summary.Errors)
                sb.AppendLine($"  !! {error}");
        }
        return sb.ToString();
    }

    /// <summary>Uloží protokol na plochu a vrátí cestu k souboru.</summary>
    public static string SaveToDesktop(TransferSummary summary, bool sending)
    {
        string name = S.T("Migrato-protokol", "Migrato-report")
                      + $"-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name);
        File.WriteAllText(path, Build(summary, sending));
        return path;
    }
}
