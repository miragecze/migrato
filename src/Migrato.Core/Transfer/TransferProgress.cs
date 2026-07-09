namespace Migrato.Core.Transfer;

public sealed record TransferProgress(
    long BytesDone,
    long BytesTotal,
    int FilesDone,
    int FilesTotal,
    string CurrentFile);

public sealed record TransferSummary(
    int FilesOk,
    int FilesFailed,
    long BytesTransferred,
    List<string> Errors,
    List<Protocol.PostActionResult> PostResults);
