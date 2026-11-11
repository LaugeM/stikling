using Stikling.Core.Models;
using Stikling.Core.Sync;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Web.Services;

public sealed class IndexedDbTimelineRepository(IndexedDb db, TimeProvider time) : ITimelineRepository
{
    public async Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId) =>
        TimelineOrder.NewestFirst((await db.GetAllAsync(RecordKinds.Timeline)).Where(e => e.SubjectId == subjectId));

    public async Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync() =>
        TimelineOrder.NewestFirst(await db.GetAllAsync(RecordKinds.Timeline))
            .GroupBy(e => e.SubjectId)
            .ToDictionary(g => g.Key, g => g.First());

    public async Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(
        int count, IReadOnlySet<Guid> subjectIds, DateTimeOffset since, IReadOnlySet<Guid>? referencePhotoIds = null) =>
        TodayBoard.RecentActivity(await db.GetAllAsync(RecordKinds.Timeline), subjectIds, since, count, referencePhotoIds);

    public async Task<IReadOnlyList<TimelineEntry>> GetPlantChangesAsync() =>
        (await db.GetAllAsync(RecordKinds.Timeline))
            .Where(e => !e.IsDeleted && e.Kind == TimelineKind.Change && e.SubjectType == SubjectType.Plant
                && TimelineEvents.Of(e) != TimelineEvent.LookedOver)
            .ToList();

    public async Task<IReadOnlyList<TimelineEntry>> GetComingBackAsync() =>
        (await db.GetAllAsync(RecordKinds.Timeline)).Where(NotesComingBack.IsWaiting).ToList();

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLastActivityBeforeAsync(DateTimeOffset before) =>
        TimelineActivity.LastBefore(await db.GetAllAsync(RecordKinds.Timeline), before);

    public async Task<IReadOnlyList<TimelineEntry>> GetSinceAsync(DateTimeOffset since) =>
        (await db.GetAllAsync(RecordKinds.Timeline))
            .Where(e => !e.IsDeleted && e.OccurredAt >= since)
            .ToList();

    public async Task AddAsync(TimelineEntry entry)
    {
        entry.CreatedAt = entry.UpdatedAt = time.GetUtcNow();
        if (entry.OccurredAt == default)
            entry.OccurredAt = entry.CreatedAt;
        await db.PutAsync(RecordKinds.Timeline, entry);
    }

    public async Task UpdateAsync(TimelineEntry entry)
    {
        entry.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(RecordKinds.Timeline, entry);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await db.GetAsync(RecordKinds.Timeline, id);
        if (entry is null || entry.IsDeleted)
            return;
        entry.DeletedAt = entry.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(RecordKinds.Timeline, entry);
    }
}

public sealed class IndexedDbPhotoRepository(IndexedDb db, TimeProvider time) : IPhotoRepository
{
    public async Task<IReadOnlyList<Photo>> GetForAsync(Guid subjectId) =>
        (await db.GetAllAsync(RecordKinds.Photos))
            .Where(p => p.SubjectId == subjectId && !p.IsDeleted)
            .OrderByDescending(p => p.TakenAt)
            .ToList();

    public async Task<Photo?> GetAsync(Guid id)
    {
        var photo = await db.GetAsync(RecordKinds.Photos, id);
        return photo is { IsDeleted: false } ? photo : null;
    }

    public async Task<IReadOnlyList<Photo>> GetOfKindAsync(SubjectType subjectType) =>
        (await db.GetAllAsync(RecordKinds.Photos))
            .Where(p => p.SubjectType == subjectType && !p.IsDeleted)
            .OrderByDescending(p => p.TakenAt)
            .ToList();

    public async Task<IReadOnlyDictionary<Guid, Photo>> GetNewestPerSubjectAsync() =>
        (await db.GetAllAsync(RecordKinds.Photos))
            .Where(p => !p.IsDeleted && !p.IsReference)
            .GroupBy(p => p.SubjectId)
            .ToDictionary(g => g.Key, g => g.MaxBy(p => p.TakenAt)!);

    public async Task AddAsync(Photo photo)
    {
        photo.CreatedAt = photo.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(RecordKinds.Photos, photo);
    }

    public async Task UpdateAsync(Photo photo)
    {
        photo.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(RecordKinds.Photos, photo);
    }

    public async Task DeleteAsync(Guid id)
    {
        var photo = await db.GetAsync(RecordKinds.Photos, id);
        if (photo is null || photo.IsDeleted)
            return;
        photo.DeletedAt = photo.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(RecordKinds.Photos, photo);
    }
}
