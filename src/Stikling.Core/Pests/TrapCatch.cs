using Stikling.Core.Models;

namespace Stikling.Core.Pests;

/// <summary>
/// What one trap count caught since the last one.
/// </summary>
/// <param name="Count">The count it was worked out from.</param>
/// <param name="Caught">How many are new since last time.</param>
/// <param name="Days">Days since the last count or new trap, or since the case started.</param>
public sealed record TrapCatch(PestTreatment Count, int Caught, int Days)
{
    public DateOnly On => Count.OccurredOn;

    /// <summary>
    /// The catch as a weekly rate, so counts a few days apart and counts a fortnight apart can
    /// be compared. Null when there are no days to spread it over.
    /// </summary>
    public double? PerWeek => Days > 0 ? Caught * 7.0 / Days : null;
}
