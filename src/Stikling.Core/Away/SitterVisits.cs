using Stikling.Core.Models;

namespace Stikling.Core.Away;

/// <summary>The days a plant-sitter comes by.</summary>
public static class SitterVisits
{
    /// <summary>
    /// The visits strictly after the day you leave and strictly before the day you are back, oldest
    /// first. Twice a week alternates three and four days, so it falls on the same two weekdays.
    /// </summary>
    public static IReadOnlyList<DateOnly> Dates(VisitPattern pattern, DateOnly from, DateOnly until)
    {
        var steps = pattern switch
        {
            VisitPattern.EveryDay => new[] { 1 },
            VisitPattern.EveryTwoDays => [2],
            VisitPattern.EveryThreeDays => [3],
            VisitPattern.TwiceAWeek => [3, 4],
            VisitPattern.Weekly => [7],
            _ => Array.Empty<int>()
        };

        var dates = new List<DateOnly>();
        if (steps.Length == 0)
            return dates;

        var day = from;
        for (var i = 0; ; i++)
        {
            day = day.AddDays(steps[i % steps.Length]);
            if (day >= until)
                return dates;
            dates.Add(day);
        }
    }
}
