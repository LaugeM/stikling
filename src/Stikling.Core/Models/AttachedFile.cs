using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// What a file is attached to. It has its own list rather than reusing <see cref="SubjectType"/>,
/// since an older version of the app fails on a name it doesn't know, and that one is read by photos
/// and the timeline.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FileSubject>))]
public enum FileSubject
{
    Plant,
    Propagation,
    Order
}

/// <summary>
/// A receipt, a plant tag or a care card kept with a plant, a propagation or an order, as an image or
/// a PDF. This is only the record. The bytes are kept on the device by the file's id and travel
/// on their own, like a photo's images, see <c>FileSyncService</c>. Files never appear on share links
/// or the public collection page.
/// </summary>
public sealed class AttachedFile : Entity
{
    /// <summary>The longest a name can be.</summary>
    public const int MaxNameLength = 200;

    public const string Pdf = "application/pdf";

    public FileSubject SubjectType { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>The name shown. It starts as the file's own name and can be changed.</summary>
    public string Name { get; set; } = "";

    /// <summary>"application/pdf", "image/webp" or "image/jpeg", as the bytes are stored.</summary>
    public string ContentType { get; set; } = "";

    /// <summary>The size in bytes, as stored.</summary>
    public long Size { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    /// <summary>What is wrong with the record, empty when it can be saved. The service has checked the input first, so these are not shown to people.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
            errors.Add("A file needs a name of 1 to 200 characters.");
        if (SubjectId == Guid.Empty)
            errors.Add("A file needs something to belong to.");
        if (Size < 0)
            errors.Add("A file can't have a negative size.");
        return errors;
    }
}
