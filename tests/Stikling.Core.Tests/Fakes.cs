using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Propagations;
using Stikling.Core.SoilMixes;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

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
        // DateOnly.MaxValue: the fake has no clock, and the future-date rule is tested on the model itself
        if (plant.Validate(DateOnly.MaxValue).Count > 0)
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

internal sealed class FakePropagationRepository : IPropagationRepository
{
    public Dictionary<Guid, Propagation> Propagations { get; } = [];

    public Task<IReadOnlyList<Propagation>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Propagation>>(Propagations.Values.Where(p => !p.IsDeleted).ToList());

    public Task<Propagation?> GetAsync(Guid id) =>
        Task.FromResult(Propagations.TryGetValue(id, out var p) && !p.IsDeleted ? p : null);

    public Task SaveAsync(Propagation propagation)
    {
        if (propagation.Validate().Count > 0)
            throw new InvalidOperationException("Invalid propagation");
        Propagations[propagation.Id] = propagation;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Propagations.TryGetValue(id, out var p))
            p.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCareLogRepository : ICareLogRepository
{
    /// <summary>In the order they were saved, which the multi-plant tests rely on.</summary>
    public List<CareLog> Logs { get; } = [];

    public Task<IReadOnlyList<CareLog>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<CareLog>>(Logs.Where(l => !l.IsDeleted).ToList());

    public Task<CareLog?> GetAsync(Guid id) =>
        Task.FromResult(Logs.FirstOrDefault(l => l.Id == id && !l.IsDeleted));

    public Task SaveAsync(CareLog entry)
    {
        var index = Logs.FindIndex(l => l.Id == entry.Id);
        if (index >= 0)
            Logs[index] = entry;
        else
            Logs.Add(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Logs.FirstOrDefault(l => l.Id == id) is { } entry)
            entry.DeletedAt = DateTimeOffset.UtcNow;
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

    public Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(int count, IReadOnlySet<Guid> subjectIds) =>
        Task.FromResult(TodayBoard.RecentActivity(Entries, subjectIds, count));

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

    // UTC keeps "today" the same on every machine the tests run on
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

internal sealed class FakePotRepository : IPotRepository
{
    public Dictionary<Guid, Pot> Pots { get; } = [];

    public Task<IReadOnlyList<Pot>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Pot>>(Pots.Values.Where(p => !p.IsDeleted).ToList());

    public Task<Pot?> GetAsync(Guid id) =>
        Task.FromResult(Pots.TryGetValue(id, out var pot) && !pot.IsDeleted ? pot : null);

    public Task SaveAsync(Pot pot)
    {
        if (pot.Validate().Count > 0)
            throw new InvalidOperationException("Invalid pot");
        Pots[pot.Id] = pot;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Pots.TryGetValue(id, out var pot))
            pot.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSoilMixRepository : ISoilMixRepository
{
    public Dictionary<Guid, SoilMix> Mixes { get; } = [];

    public Task<IReadOnlyList<SoilMix>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<SoilMix>>(Mixes.Values.Where(m => !m.IsDeleted).ToList());

    public Task<SoilMix?> GetAsync(Guid id) =>
        Task.FromResult(Mixes.TryGetValue(id, out var mix) && !mix.IsDeleted ? mix : null);

    public Task SaveAsync(SoilMix mix)
    {
        if (mix.Validate().Count > 0)
            throw new InvalidOperationException("Invalid mix");
        Mixes[mix.Id] = mix;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Mixes.TryGetValue(id, out var mix))
            mix.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}
