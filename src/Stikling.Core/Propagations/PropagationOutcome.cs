using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Propagations;

/// <summary>A plant a finished propagation became, with the room it is in now.</summary>
/// <param name="Room">The room or spot it stands in, or null when it has none.</param>
public sealed record OutcomePlant(Plant Plant, string? Room)
{
    /// <summary>Still in the collection, so not died, given away or sold.</summary>
    public bool StillHere => Plant.Status == PlantStatus.Active;
}

/// <summary>
/// How a finished propagation turned out, worked out from what it already keeps: how long it
/// ran, how soon it rooted, how many units were potted up or failed, and the plants it became.
/// </summary>
/// <param name="DaysRan">Days from the start to the day it finished. Null for one finished before the finish date was kept.</param>
/// <param name="DaysToRoot">
/// Days to the first root, or to the first seedling for seeds. Falls back to the day it reached
/// Rooted when no first root was noted. Null when it never got that far.
/// </param>
/// <param name="Plants">The plants potted up from it that aren't deleted, the ones still here first.</param>
public sealed record PropagationOutcome(
    int? DaysRan,
    int? DaysToRoot,
    int Started,
    int PottedUp,
    int Failed,
    IReadOnlyList<OutcomePlant> Plants)
{
    /// <summary>
    /// The outcome of a finished propagation, or null for one still going. A propagation finished
    /// before the finish date was kept has no <see cref="Propagation.FinishedOn"/>, and then
    /// <paramref name="fallbackEnd"/> stands in for it.
    /// </summary>
    public static PropagationOutcome? For(Propagation propagation, IEnumerable<Plant> plants, Places places, DateOnly? fallbackEnd = null)
    {
        if (propagation.IsActive)
            return null;

        var end = propagation.FinishedOn ?? fallbackEnd;
        int? daysRan = end is { } e ? Math.Max(0, e.DayNumber - propagation.StartedOn.DayNumber) : null;

        var firstSign = propagation.Type == PropagationType.Seed ? propagation.FirstGerminatedOn : propagation.FirstRootOn;
        var daysToRoot = firstSign is { } sign
            ? Math.Max(0, sign.DayNumber - propagation.StartedOn.DayNumber)
            : propagation.DaysToRoot;

        var became = plants
            .Where(p => p.FromPropagationId == propagation.Id && !p.IsDeleted)
            .Select(p => new OutcomePlant(p, places.NameOf(p.PlaceId)))
            .OrderByDescending(p => p.StillHere)
            .ThenBy(p => p.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new PropagationOutcome(
            daysRan,
            daysToRoot,
            propagation.InitialCount,
            propagation.PottedUpCount,
            propagation.FailedCount,
            became);
    }
}
