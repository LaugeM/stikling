namespace Stikling.Core.Sync;

/// <summary>
/// What the server and the devices agree on about attached files. A file's record syncs like any
/// other, and its bytes travel on their own, under <c>/collections/{id}/files/{fileId}</c>.
/// </summary>
public static class FileRules
{
    /// <summary>The largest file the server takes, and the app keeps. Images are shrunk to fit well under it.</summary>
    public const int MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>The most files on one plant, propagation or order. Only adding is blocked.</summary>
    public const int MaxFilesPerRecord = 20;

    /// <summary>The most files asked about in one <see cref="StoredFilesRequest"/>.</summary>
    public const int BatchSize = 200;

    /// <summary>
    /// What kind of file the bytes are, from how they start, or null when it isn't one the app keeps:
    /// a PDF, or an image as <see cref="PhotoRules.FormatOf"/> knows them.
    /// </summary>
    public static ImageFormat? FormatOf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8) ? ImageFormat.Pdf : PhotoRules.FormatOf(bytes);
}

/// <summary><c>POST /collections/{id}/files/stored</c>: which of these files have their bytes on the server.</summary>
public sealed record StoredFilesRequest(List<Guid> Ids);

/// <param name="Files">The ids asked about whose bytes are on the server.</param>
public sealed record StoredFilesResponse(List<Guid> Files);
