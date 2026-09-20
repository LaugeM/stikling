using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Web.Services;

public sealed class IndexedDbTimelineRepository(IndexedDb db, TimeProvider time) : ITimelineRepository
{
    public async Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId) =>
        TimelineOrder.NewestFirst((await db.GetAllAsync<TimelineEntry>(Stores.Timeline)).Where(e => e.SubjectId == subjectId));

    public async Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync() =>
        TimelineOrder.NewestFirst(await db.GetAllAsync<TimelineEntry>(Stores.Timeline))
            .GroupBy(e => e.SubjectId)
            .ToDictionary(g => g.Key, g => g.First());

    public async Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(int count, IReadOnlySet<Guid> subjectIds) =>
        TodayBoard.RecentActivity(await db.GetAllAsync<TimelineEntry>(Stores.Timeline), subjectIds, count);

    public async Task AddAsync(TimelineEntry entry)
    {
        entry.CreatedAt = entry.UpdatedAt = time.GetUtcNow();
        if (entry.OccurredAt == default)
            entry.OccurredAt = entry.CreatedAt;
        await db.PutAsync(Stores.Timeline, entry);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await db.GetAsync<TimelineEntry>(Stores.Timeline, id);
        if (entry is null || entry.IsDeleted)
            return;
        entry.DeletedAt = entry.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(Stores.Timeline, entry);
    }
}

public sealed class IndexedDbPhotoRepository(IndexedDb db, TimeProvider time) : IPhotoRepository
{
    public async Task<IReadOnlyList<Photo>> GetForAsync(Guid subjectId) =>
        (await db.GetAllAsync<Photo>(Stores.Photos))
            .Where(p => p.SubjectId == subjectId && !p.IsDeleted)
            .OrderByDescending(p => p.TakenAt)
            .ToList();

    public async Task<Photo?> GetAsync(Guid id)
    {
        var photo = await db.GetAsync<Photo>(Stores.Photos, id);
        return photo is { IsDeleted: false } ? photo : null;
    }

    public async Task AddAsync(Photo photo)
    {
        photo.CreatedAt = photo.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(Stores.Photos, photo);
    }

    public async Task DeleteAsync(Guid id)
    {
        var photo = await db.GetAsync<Photo>(Stores.Photos, id);
        if (photo is null || photo.IsDeleted)
            return;
        photo.DeletedAt = photo.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(Stores.Photos, photo);
    }
}
