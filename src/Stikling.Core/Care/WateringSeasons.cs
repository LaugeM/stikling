using Stikling.Core.Away;
using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>A range of days. Low and High are the same when the gaps hardly differ.</summary>
public sealed record DayRange(int Low, int High);

/// <summary>
/// How long a plant has gone between waterings in the brighter and in the darker half of the year,
/// from its own log. It only states what happened, and the watering reminder doesn't use it.
/// </summary>
public sealed record WateringSeasons(DayRange Brighter, DayRange Darker)
{
    /// <summary>
    /// The ranges, or null unless each half has at least <see cref="WateringGuess.GapsNeeded"/> gaps.
    /// Also null under a grow light, where the time of year says nothing about the plant. A gap counts
    /// for the half it started in. One that touches a rest is left out, since a plant resting waters
    /// on another pace, and so is one across a stretch away. Each range is the middle half of the
    /// gaps, so one forgotten watering doesn't stretch it.
    /// </summary>
    /// <param name="logs">Care entries for any plants. Only this plant's count.</param>
    /// <param name="timeline">Timeline entries for any subject. Only this plant's changes count, for telling when it was dormant.</param>
    public static WateringSeasons? Of(
        Plant plant,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        bool lit,
        Hemisphere hemisphere,
        IReadOnlyList<AwayStretch> stretches,
        TimeProvider time)
    {
        if (lit)
            return null;

        var periods = DormantPeriods.Of(plant, timeline, time);
        var gaps = WateringGuess.Gaps(plant, logs, stretches)
            .Where(g => !TouchesRest(g.Start, g.Days, periods))
            .ToLookup(g => Seasons.InBrighterHalf(g.Start, hemisphere), g => g.Days);

        return RangeOf(gaps[true]) is { } brighter && RangeOf(gaps[false]) is { } darker
            ? new WateringSeasons(brighter, darker)
            : null;
    }

    // Started during a rest, or a rest began before the next watering
    private static bool TouchesRest(DateOnly start, int days, DormantPeriods periods) =>
        periods.Contains(start) || periods.Starts.Any(s => s > start && s < start.AddDays(days));

    private static DayRange? RangeOf(IEnumerable<int> gaps)
    {
        var weighted = gaps.Select(days => (days, 1.0)).ToList();
        return weighted.Count < WateringGuess.GapsNeeded
            ? null
            : new DayRange((int)WateringGuess.Percentile(weighted, 0.25), (int)WateringGuess.Percentile(weighted, 0.75));
    }
}
