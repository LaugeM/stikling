using Stikling.Core.Models;

namespace Stikling.Core.Flowering;

/// <summary>A plant in flower or with buds now, and since when.</summary>
public sealed record FloweringPlant(Plant Plant, LooseDate Since);

/// <summary>A plant on the calendar, with the years it flowered in that month.</summary>
public sealed record CalendarPlant(Plant Plant, IReadOnlyList<int> Years);

/// <summary>One month of the year (1 to 12) and the plants that have flowered in it.</summary>
public sealed record CalendarMonth(int Month, IReadOnlyList<CalendarPlant> Plants);

/// <summary>
/// Which plants flower when, from the plants' own records. Nothing here says what to do: it is
/// a look at the year.
/// </summary>
public sealed record FloweringCalendar(
    IReadOnlyList<FloweringPlant> InFlowerNow,
    IReadOnlyList<FloweringPlant> BudsNow,
    int NextMonth,
    IReadOnlyList<CalendarPlant> UsuallyNextMonth,
    IReadOnlyList<CalendarMonth> Months)
{
    /// <summary>True when no plant has a flowering that can be placed in a month.</summary>
    public bool IsEmpty => Months.All(m => m.Plants.Count == 0);

    public static FloweringCalendar Build(IEnumerable<Plant> plants, IEnumerable<FloweringMark> marks, DateOnly today)
    {
        var byPlant = marks.Where(m => !m.IsDeleted).ToLookup(m => m.PlantId);
        var next = today.AddMonths(1);

        var inFlower = new List<FloweringPlant>();
        var buds = new List<FloweringPlant>();
        var usually = new List<CalendarPlant>();
        var perMonth = Enumerable.Range(1, 12).ToDictionary(m => m, _ => new List<CalendarPlant>());

        foreach (var plant in plants.Where(p => !p.IsDeleted && p.InCollection)
                     .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var flowerings = Flowerings.Of(byPlant[plant.Id], plant.Id, today);
            if (flowerings.Count == 0)
                continue;

            var now = FloweringNow.Of(flowerings, today);
            var showing = now is { Stale: false };
            if (showing)
            {
                var period = now!.Period;
                if (now.Stage == FloweringStage.InFlower)
                    inFlower.Add(new FloweringPlant(plant, period.InFlower!));
                else
                    buds.Add(new FloweringPlant(plant, period.Buds!));
            }

            var covered = new HashSet<(int Year, int Month)>();
            foreach (var period in flowerings)
                Cover(period, today, covered);

            foreach (var month in covered.GroupBy(c => c.Month))
            {
                var years = month.Select(c => c.Year).Distinct().Order().ToList();
                perMonth[month.Key].Add(new CalendarPlant(plant, years));
                if (month.Key == next.Month && !showing)
                {
                    var earlier = years.Where(y => y < next.Year).ToList();
                    if (earlier.Count > 0)
                        usually.Add(new CalendarPlant(plant, earlier));
                }
            }
        }

        return new FloweringCalendar(
            inFlower, buds, next.Month, usually,
            perMonth.Select(m => new CalendarMonth(m.Key, m.Value)).ToList());
    }

    /// <summary>
    /// The months a flowering covers. It runs from the flowers (or the buds, with no flowers) to
    /// the end, and a month or a day covers its whole month. A year alone can't say a month, so
    /// it is left out.
    /// </summary>
    private static void Cover(FloweringPeriod period, DateOnly today, HashSet<(int Year, int Month)> covered)
    {
        var begin = new[] { period.InFlower, period.Buds }
            .FirstOrDefault(d => d is not null && d.Precision != DatePrecision.Year);
        if (begin is null)
            return;

        DateOnly end;
        if (period.Done is { } done)
            end = done.Precision == DatePrecision.Year ? begin.Start : done.Start;
        else if (period.EndUnknown)
            end = period.LastMarked;
        else if (FloweringNow.Of([period], today) is { Stale: true })
            end = period.LastMarked;
        else
            end = period.LastMarked > today ? period.LastMarked : today;

        if (end < begin.Start)
            end = begin.Start;

        for (var month = new DateOnly(begin.Start.Year, begin.Start.Month, 1);
             month <= end;
             month = month.AddMonths(1))
            covered.Add((month.Year, month.Month));
    }
}
