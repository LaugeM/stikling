using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>The newest entry of one kind: the day it was done and how many days ago that is.</summary>
public sealed record PropagationCareLatest(DateOnly Day, int DaysAgo);

/// <summary>Writing to a propagation's care log, and reading it back.</summary>
public sealed class PropagationCareService(IPropagationCareLogRepository logs, TimeProvider time)
{
    private DateOnly Today => time.Today();

    public Task<IReadOnlyList<PropagationCareLog>> GetAllAsync() => logs.GetAllAsync();

    /// <summary>Saves one care entry. Throws for a finished propagation, a day outside its life or a bad reading.</summary>
    public async Task<PropagationCareLog> LogAsync(
        Propagation propagation, PropagationCareKind kind, DateOnly on, int? moisture = null)
    {
        if (!propagation.IsActive)
            throw new InvalidOperationException("This propagation is finished.");
        if (on > Today)
            throw new InvalidOperationException("That date is in the future.");
        if (on < propagation.StartedOn)
            throw new InvalidOperationException("The date can't be before the propagation started.");

        var entry = new PropagationCareLog
        {
            PropagationId = propagation.Id,
            Kind = kind,
            OccurredOn = on,
            Moisture = moisture
        };
        if (entry.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        await logs.SaveAsync(entry);
        return entry;
    }

    /// <summary>
    /// Changes the number on a moisture reading, so a second tap the same day corrects the entry instead
    /// of adding one. Returns null when the entry is gone, is not a reading or is not this propagation's.
    /// </summary>
    public async Task<PropagationCareLog?> CorrectReadingAsync(Guid propagationId, Guid id, int moisture)
    {
        if (await logs.GetAsync(id) is not { IsDeleted: false, Kind: PropagationCareKind.MoistureReading } entry
            || entry.PropagationId != propagationId)
            return null;
        var previous = entry.Moisture;
        entry.Moisture = moisture;
        if (entry.Validate().FirstOrDefault() is { } error)
        {
            entry.Moisture = previous;
            throw new InvalidOperationException(error);
        }
        await logs.SaveAsync(entry);
        return entry;
    }

    public Task DeleteAsync(Guid id) => logs.DeleteAsync(id);

    /// <summary>The entries of one propagation, newest first.</summary>
    public static IReadOnlyList<PropagationCareLog> For(IEnumerable<PropagationCareLog> logs, Guid propagationId) =>
        [.. logs
            .Where(l => !l.IsDeleted && l.PropagationId == propagationId)
            .OrderByDescending(l => l.OccurredOn)
            .ThenByDescending(l => l.CreatedAt)];

    /// <summary>The newest entry of a kind, or null when there is none.</summary>
    public static PropagationCareLatest? Latest(
        IEnumerable<PropagationCareLog> logs, Guid propagationId, PropagationCareKind kind, DateOnly today) =>
        For(logs, propagationId).FirstOrDefault(l => l.Kind == kind) is { } newest
            ? new PropagationCareLatest(newest.OccurredOn, today.DayNumber - newest.OccurredOn.DayNumber)
            : null;

    /// <summary>The day of the newest entry of any kind for each propagation that has one, for Today's check.</summary>
    public static IReadOnlyDictionary<Guid, DateOnly> NewestDays(IEnumerable<PropagationCareLog> logs) =>
        logs.Where(l => !l.IsDeleted)
            .GroupBy(l => l.PropagationId)
            .ToDictionary(g => g.Key, g => g.Max(l => l.OccurredOn));

    /// <summary>
    /// What can be logged in this medium. Water only has its water to change, and the water under a
    /// corm riser is the same. Everything else is watered, with a meter reading if there is a meter.
    /// </summary>
    public static IReadOnlyList<PropagationCareKind> KindsFor(GrowingMedium medium) => medium switch
    {
        GrowingMedium.Water or GrowingMedium.CormRiser => [PropagationCareKind.ChangedWater],
        _ => [PropagationCareKind.Watered, PropagationCareKind.MoistureReading]
    };

    /// <summary>"Watered", "Changed the water" or "Moisture 4/10".</summary>
    public static string Describe(PropagationCareLog entry) => entry.Kind switch
    {
        PropagationCareKind.Watered => "Watered",
        PropagationCareKind.ChangedWater => "Changed the water",
        PropagationCareKind.MoistureReading => $"Moisture {entry.Moisture}/10",
        _ => entry.Kind.ToString()
    };
}
