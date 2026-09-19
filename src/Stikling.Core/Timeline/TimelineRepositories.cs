using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

public interface ITimelineRepository
{
    /// <summary>The subject's entries, newest first, excluding deleted ones.</summary>
    Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId);

    /// <summary>The newest entry per subject, for "last activity" in lists.</summary>
    Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync();

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
