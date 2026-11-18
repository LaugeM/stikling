namespace Stikling.Api.Data;

/// <summary>
/// The bytes of one attached file, kept in Blob Storage next to the photo images. The bytes are in
/// the blob, and this row says they are there and how big they are, so a collection's space can be
/// added up without asking Blob Storage.
/// </summary>
public class StoredFile
{
    public Guid CollectionId { get; set; }
    public Guid FileId { get; set; }
    public long Bytes { get; set; }

    /// <summary>"application/pdf", "image/webp" or "image/jpeg", from the bytes when they were sent.</summary>
    public string ContentType { get; set; } = "";

    public DateTimeOffset StoredAt { get; set; }
}
