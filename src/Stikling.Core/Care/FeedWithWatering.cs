using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>
/// The rule for a plant that gets its feed with the watering: the feed goes in on every
/// watering, or every second or third.
/// </summary>
public static class FeedWithWatering
{
    /// <summary>
    /// True when the feed is due on <paramref name="entry"/>, a watering or topping up that is
    /// about to be saved. It counts the waterings after the last feed, this one included, and
    /// the feed is due when that reaches <paramref name="every"/> (1 when not set). The last feed
    /// is the newest Fertilised entry or ToppedUp entry with products. With none ever, it is due.
    /// </summary>
    /// <param name="plantLogs">The plant's care entries. Deleted ones are ignored.</param>
    public static bool IsDue(IEnumerable<CareLog> plantLogs, CareLog entry, int? every)
    {
        var existing = plantLogs.Where(l => !l.IsDeleted && l.Id != entry.Id).ToList();

        DateOnly? lastFeed = existing
            .Where(l => l.Kind == CareKind.Fertilised || (l.Kind == CareKind.ToppedUp && l.Products.Count > 0))
            .Select(l => (DateOnly?)l.OccurredOn)
            .Max();

        if (lastFeed is not { } feed)
            return true;

        // Only waterings after the last feed count, this one included
        var count = existing.Count(l => l.Kind is CareKind.Watered or CareKind.ToppedUp && l.OccurredOn > feed)
            + (entry.OccurredOn > feed ? 1 : 0);
        return count >= Math.Max(1, every ?? 1);
    }
}
