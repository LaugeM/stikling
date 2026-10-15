using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>
/// When Today asks whether a plant has gone dormant. Only plants with a growing season of Summer
/// or Winter are asked, as most houseplants don't rest. The asking runs over a window of days
/// that starts a little before the rest usually does.
/// </summary>
public static class RestSeason
{
    /// <summary>How many days the asking window lasts.</summary>
    public const int WindowDays = 90;

    /// <summary>How many days before the day it went dormant last time the window starts.</summary>
    public const int LeadDays = 14;

    /// <summary>How long before the usual start a past rest still counts as one of its season.</summary>
    public const int LearnBeforeDays = 60;

    /// <summary>
    /// The first day of the window that today falls in, or null when it's outside the window or
    /// the plant has no season to ask about. The window starts on a default day for the season:
    /// 1 November for a plant that rests in winter and 1 June for one that rests in summer, and
    /// the other way round in the south (1 May and 1 December). A plant that went dormant earlier
    /// than that moves it earlier, to <see cref="LeadDays"/> before the latest day it went dormant.
    /// A rest that came later never moves it later, and one far from its season is left out.
    /// </summary>
    public static DateOnly? WindowStart(Plant plant, DormantPeriods periods, Hemisphere hemisphere, DateOnly today)
    {
        if (plant.GrowingSeason is not (GrowingSeason.Summer or GrowingSeason.Winter))
            return null;

        var restsInWinter = plant.GrowingSeason == GrowingSeason.Summer;
        var north = hemisphere == Hemisphere.Northern;
        var (month, day) = (restsInWinter, north) switch
        {
            (true, true) => (11, 1),
            (true, false) => (5, 1),
            (false, true) => (6, 1),
            (false, false) => (12, 1)
        };

        // Answering late one year mustn't push next year's question past the rest, so a past rest
        // only moves the window earlier. One far from its season, like a plant put down for
        // another reason, is left out.
        if (periods.Starts.Count > 0)
        {
            var last = periods.Starts[^1];
            var learned = last.AddDays(-LeadDays);
            foreach (var year in new[] { last.Year - 1, last.Year })
            {
                var usual = new DateOnly(year, month, day);
                if (last >= usual.AddDays(-LearnBeforeDays) && last < usual.AddDays(WindowDays) && learned < usual)
                {
                    (month, day) = (learned.Month, learned.Day);
                    break;
                }
            }
        }

        foreach (var year in new[] { today.Year, today.Year - 1 })
        {
            var start = new DateOnly(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
            if (start <= today && today < start.AddDays(WindowDays))
                return start;
        }
        return null;
    }

    /// <summary>
    /// The plants to ask about today with the start of their window: in the collection, active,
    /// not dormant now, with a window today, and not gone dormant since the window started.
    /// Ordered by name.
    /// </summary>
    public static IReadOnlyList<(Plant Plant, DateOnly WindowStart)> AskNow(
        IEnumerable<Plant> plants, Func<Plant, DormantPeriods> periodsOf, Hemisphere hemisphere, DateOnly today)
    {
        var result = new List<(Plant Plant, DateOnly WindowStart)>();
        foreach (var plant in plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active && !p.IsDormant))
        {
            var periods = periodsOf(plant);
            if (WindowStart(plant, periods, hemisphere, today) is { } start && !periods.Starts.Any(s => s >= start))
                result.Add((plant, start));
        }
        return result.OrderBy(r => r.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
