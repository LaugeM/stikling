using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

public interface ITimelineRepository
{
    /// <summary>The subject's entries, newest first, excluding deleted ones.</summary>
    Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId);

    /// <summary>The newest entry per subject, for "last activity" in lists.</summary>
    Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync();

    /// <summary>
    /// The newest entries for these subjects from <paramref name="since"/> on, for the activity
    /// list on Today. Deleted plants and propagations are left out by passing only the ones that
    /// are still there.
    /// </summary>
    Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(int count, IReadOnlySet<Guid> subjectIds, DateTimeOffset since);

    Task AddAsync(TimelineEntry entry);

    /// <summary>Saves a corrected entry.</summary>
    Task UpdateAsync(TimelineEntry entry);

    /// <summary>Soft-deletes an entry, e.g. a note added by mistake.</summary>
    Task DeleteAsync(Guid id);
}

public interface IPhotoRepository
{
    /// <summary>The subject's photos, newest first, excluding deleted ones.</summary>
    Task<IReadOnlyList<Photo>> GetForAsync(Guid subjectId);

    Task<Photo?> GetAsync(Guid id);

    /// <summary>Every photo of one kind of subject, excluding deleted ones, e.g. all pot photos for the pot library.</summary>
    Task<IReadOnlyList<Photo>> GetOfKindAsync(SubjectType subjectType);

    /// <summary>The newest photo of each subject, by when it was taken, for the photo reminder.</summary>
    Task<IReadOnlyDictionary<Guid, Photo>> GetNewestPerSubjectAsync();

    /// <summary>Saves photo metadata. The image data is stored separately by the photo service.</summary>
    Task AddAsync(Photo photo);

    /// <summary>Saves changed metadata, e.g. a new date when its timeline entry was corrected.</summary>
    Task UpdateAsync(Photo photo);

    Task DeleteAsync(Guid id);
}
