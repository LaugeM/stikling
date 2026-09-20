using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>The last time something was done, e.g. "Watered, 3 days ago".</summary>
public sealed record LastCare(CareKind Kind, DateOnly On, int DaysAgo, string? Notes);

/// <summary>The newest moisture meter reading and when it was taken.</summary>
public sealed record MoistureReading(int Value, DateOnly On, int DaysAgo);

/// <summary>
/// Reading the care log back. This is the part that makes logging worth the effort: the
/// numbers here are what the plant page and the cards show.
/// </summary>
public static class CareSummary
{
    /// <summary>The kinds shown on the plant page summary, in the order they're shown.</summary>
    public static readonly IReadOnlyList<CareKind> Headline =
        [CareKind.Watered, CareKind.Fertilised, CareKind.Flushed];

    /// <summary>One plant's entries, newest first.</summary>
    public static IReadOnlyList<CareLog> For(IEnumerable<CareLog> logs, Guid plantId) =>
        logs
            .Where(l => !l.IsDeleted && l.PlantId == plantId)
            .OrderByDescending(l => l.OccurredOn)
            .ThenByDescending(l => l.CreatedAt)
            .ToList();

    /// <summary>The last time each kind was done for a plant, newest first.</summary>
    public static IReadOnlyList<LastCare> LastPerKind(IEnumerable<CareLog> logs, Guid plantId, DateOnly today) =>
        For(logs, plantId)
            .GroupBy(l => l.Kind)
            .Select(g => Describe(g.First(), today))
            .OrderByDescending(l => l.On)
            .ToList();

    /// <summary>The last time one thing was done, or null if it never has been.</summary>
    public static LastCare? Latest(IEnumerable<CareLog> logs, Guid plantId, CareKind kind, DateOnly today) =>
        For(logs, plantId).FirstOrDefault(l => l.Kind == kind) is { } entry ? Describe(entry, today) : null;

    /// <summary>The newest moisture reading for a plant, or null if there isn't one.</summary>
    public static MoistureReading? LatestMoisture(IEnumerable<CareLog> logs, Guid plantId, DateOnly today) =>
        For(logs, plantId).FirstOrDefault(l => l.Kind == CareKind.MoistureReading && l.Moisture is not null)
            is { Moisture: { } value } entry
            ? new MoistureReading(value, entry.OccurredOn, DaysBetween(entry.OccurredOn, today))
            : null;

    private static LastCare Describe(CareLog entry, DateOnly today) =>
        new(entry.Kind, entry.OccurredOn, DaysBetween(entry.OccurredOn, today), entry.Notes);

    // Floored at 0 so an entry dated today never reads as negative
    private static int DaysBetween(DateOnly then, DateOnly today) => Math.Max(0, today.DayNumber - then.DayNumber);
}
