using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

public interface ITimelineRepository
{
    /// <summary>The subject's entries, newest first, excluding deleted ones.</summary>
    Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId);

    /// <summary>The newest entry per subject, for "last activity" in lists.</summary>
    Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync();

    /// <summary>
    /// The newest entries for these subjects, for the activity list on Today. Deleted plants and
    /// propagations are left out by passing only the ones that are still there.
    /// </summary>
    Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(int count, IReadOnlySet<Guid> subjectIds);

    Task AddAsync(TimelineEntry entry);

    /// <summary>Soft-deletes an entry, e.g. a note added by mistake.</summary>
    Task DeleteAsync(Guid id);
}

public interface IPhotoRepository
{
    /// <summary>The subject's photos, newest first, excluding deleted ones.</summary>
    Task<IReadOnlyList<Photo>> GetForAsync(Guid subjectId);

    Task<Photo?> GetAsync(Guid id);

    /// <summary>Saves photo metadata. The image data is stored separately by the photo service.</summary>
    Task AddAsync(Photo photo);

    Task DeleteAsync(Guid id);
}
