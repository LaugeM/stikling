using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

/// <summary>In-memory repositories for testing services without a browser database.</summary>
internal sealed class FakePlantRepository : IPlantRepository
{
    public Dictionary<Guid, Plant> Plants { get; } = [];

    public Task<IReadOnlyList<Plant>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Plant>>(Plants.Values.Where(p => !p.IsDeleted).ToList());

    public Task<Plant?> GetAsync(Guid id) =>
        Task.FromResult(Plants.TryGetValue(id, out var p) && !p.IsDeleted ? p : null);

    public Task SaveAsync(Plant plant)
    {
        if (plant.Validate().Count > 0)
            throw new InvalidOperationException("Invalid plant");
        Plants[plant.Id] = plant;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Plants.TryGetValue(id, out var p))
            p.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeTimelineRepository : ITimelineRepository
{
    public List<TimelineEntry> Entries { get; } = [];

    public Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId) =>
        Task.FromResult(TimelineOrder.NewestFirst(Entries.Where(e => e.SubjectId == subjectId)));

    public Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, TimelineEntry>>(
            TimelineOrder.NewestFirst(Entries).GroupBy(e => e.SubjectId).ToDictionary(g => g.Key, g => g.First()));

    public Task AddAsync(TimelineEntry entry)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        Entries.First(e => e.Id == id).DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

/// <summary>A clock that always returns the same moment.</summary>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
