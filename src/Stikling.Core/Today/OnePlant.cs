using Stikling.Core.Away;
using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>
/// Just one plant: the one plant to look at on a day with little energy. A plant flagged Needs attention
/// comes first, the one flagged longest ago before the rest, and then the plant that has gone longest since
/// it was looked over. Nothing about it is stored.
/// </summary>
public static class OnePlant
{
    /// <summary>
    /// The plant to look at, or null when there is none to pick: during away or quiet time, when nothing is
    /// left, or when every plant has been seen already. Dormant plants and plants that have left are left out.
    /// </summary>
    /// <param name="lastLooked">
    /// The newest moment per plant of a note, photos or a look over
    /// (<see cref="Timeline.ITimelineRepository.GetLastActivityBeforeAsync"/>). A plant with none counts as the
    /// longest ago, and the display name decides between two equals.
    /// </param>
    /// <param name="seen">The plants already shown in this visit, so One more never gives the same one twice.</param>
    public static Plant? Pick(
        IEnumerable<Plant> plants,
        IReadOnlyDictionary<Guid, DateTimeOffset> lastLooked,
        IReadOnlyList<AwayStretch> stretches,
        DateOnly today,
        IReadOnlySet<Guid>? seen = null)
    {
        if (AwayTime.Current(stretches, today) is not null)
            return null;

        var candidates = plants
            .Where(p => !p.IsDeleted && p.InCollection && !p.IsDormant && seen?.Contains(p.Id) != true)
            .ToList();

        var flagged = candidates
            .Where(p => p.Attention is not null && !AttentionLater.IsWaiting(p.Attention, p.AttentionLater, today))
            .OrderBy(p => p.Attention!.Since)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
        if (flagged is not null)
            return flagged;

        return candidates
            .OrderBy(p => lastLooked.TryGetValue(p.Id, out var moment) ? moment : DateTimeOffset.MinValue)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }
}
