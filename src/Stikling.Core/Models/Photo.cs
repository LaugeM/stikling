namespace Stikling.Core.Models;

/// <summary>
/// Photo metadata. The image itself (and a thumbnail) is stored separately as binary data,
/// keyed by the photo's id, so lists of photos stay small and fast to load.
/// </summary>
public sealed class Photo : Entity
{
    public SubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>When the photo was taken, as far as the device knows.</summary>
    public DateTimeOffset TakenAt { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
}
