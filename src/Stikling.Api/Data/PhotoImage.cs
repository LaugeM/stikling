using Stikling.Core.Sync;

namespace Stikling.Api.Data;

/// <summary>
/// One image kept in Blob Storage for a photo. The image is in the blob, and this row says it is
/// there and how big it is, so a collection's space can be added up without asking Blob Storage.
/// </summary>
public class PhotoImage
{
    public Guid CollectionId { get; set; }
    public Guid PhotoId { get; set; }
    public PhotoSize Size { get; set; }
    public long Bytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
