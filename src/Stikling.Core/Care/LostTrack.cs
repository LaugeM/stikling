using Stikling.Core.Away;
using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>
/// I lost track: when a plant hasn't been seen watered for much longer than usual, the person can say so.
/// Watering then counts from today (<see cref="Plant.WateringSince"/>), so the gap isn't learned as how long
/// the plant goes without water. The earlier entries stay in the history.
/// </summary>
public static class LostTrack
{
    /// <summary>Days since the last watering that count as lost track for a plant with no usual gap.</summary>
    public const int DaysWithoutUsualGap = 30;

    /// <summary>
    /// True when "I lost track of watering" is worth offering on the plant: it has watering history, is not
    /// dormant, and the last watering is more than twice its usual gap ago, or over
    /// <see cref="DaysWithoutUsualGap"/> days ago when it has no usual gap yet. The usual gap is the middle
    /// one of the gaps between waterings, once there are enough of them to trust.
    /// </summary>
    public static bool Offered(Plant plant, IEnumerable<CareLog> logs, IReadOnlyList<AwayStretch> stretches, DateOnly today)
    {
        if (plant.IsDeleted || !plant.InCollection || plant.IsDormant)
            return false;

        var waterings = WateringGuess.Waterings(plant, logs);
        if (waterings.Count == 0)
            return false;

        var daysSince = today.DayNumber - waterings[^1].DayNumber;
        var gaps = WateringGuess.Gaps(plant, logs, stretches).Select(g => g.Days).Order().ToList();
        if (gaps.Count < WateringGuess.GapsNeeded)
            return daysSince > DaysWithoutUsualGap;

        var usual = gaps[gaps.Count / 2];
        return daysSince > 2 * usual;
    }
}
