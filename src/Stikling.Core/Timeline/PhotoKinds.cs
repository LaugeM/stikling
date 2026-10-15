using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>
/// Tells a subject's own photos from its reference photos, so every place that lists a plant's or
/// propagation's photos leaves the references out the same way.
/// </summary>
public static class PhotoKinds
{
    /// <summary>The photos of the plant or propagation itself: not deleted and not reference photos.</summary>
    public static IEnumerable<Photo> Own(IEnumerable<Photo> photos) =>
        photos.Where(p => !p.IsDeleted && !p.IsReference);

    /// <summary>The reference photos that aren't deleted, newest first.</summary>
    public static IReadOnlyList<Photo> References(IEnumerable<Photo> photos) =>
        photos.Where(p => !p.IsDeleted && p.IsReference).OrderByDescending(p => p.TakenAt).ToList();

    /// <summary>
    /// The chosen cover, unless it's gone, deleted or a reference photo, in which case null so the
    /// caller falls back to the usual default as if none were chosen.
    /// </summary>
    public static Guid? ValidCover(Guid? chosen, IEnumerable<Photo> photos) =>
        chosen is { } id && photos.Any(p => p.Id == id && !p.IsDeleted && !p.IsReference) ? id : null;
}
