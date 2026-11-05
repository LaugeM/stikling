using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>A plant that has been with someone another year, shown on Today through <paramref name="ShowUntil"/>.</summary>
/// <param name="Years">How many years since the plant was acquired, at least 1.</param>
/// <param name="ShowUntil">The last day it shows.</param>
public sealed record Anniversary(Plant Plant, int Years, DateOnly ShowUntil)
{
    /// <summary>The year the window started, which is what <see cref="PutOffs.Anniversary"/> takes.</summary>
    public int WindowYear => ShowUntil.AddDays(1 - Anniversaries.WindowDays).Year;
}

/// <summary>
/// The plants on Today that have been with someone another year, worked out from the day acquired.
/// A plant with a whole day shows from that day through the six after it, one with a month from the
/// 1st through the 7th of the month, and one with only a year never shows.
/// </summary>
public static class Anniversaries
{
    /// <summary>How many days an anniversary shows, the day itself included.</summary>
    public const int WindowDays = 7;

    public static IReadOnlyList<Anniversary> For(IEnumerable<Plant> plants, DateOnly day, PutOffs putOffs)
    {
        var found = new List<Anniversary>();
        foreach (var plant in plants)
        {
            if (plant.IsDeleted || !plant.InCollection || plant.AcquiredOn is not { } acquired)
                continue;

            // The window may have started last December and run into January
            foreach (var year in new[] { day.Year, day.Year - 1 })
            {
                var years = year - acquired.Year;
                var start = WindowStart(acquired, year);
                if (years < 1 || start is not { } first || day < first || day > first.AddDays(WindowDays - 1))
                    continue;

                if (!putOffs.IsPutOff(PutOffs.Anniversary(plant.Id, year), day))
                    found.Add(new Anniversary(plant, years, first.AddDays(WindowDays - 1)));
                break;
            }
        }

        return found
            .OrderByDescending(a => a.Years)
            .ThenBy(a => a.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Plant.Id)
            .ToList();
    }

    // The first day of the window in the year, or null for a date that only has a year
    private static DateOnly? WindowStart(LooseDate acquired, int year) => acquired.Precision switch
    {
        DatePrecision.Day => new DateOnly(year, acquired.Month!.Value,
            Math.Min(acquired.Day!.Value, DateTime.DaysInMonth(year, acquired.Month.Value))),
        DatePrecision.Month => new DateOnly(year, acquired.Month!.Value, 1),
        _ => null
    };
}
