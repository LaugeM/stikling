using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>What happened to a plant on one day, strongest first.</summary>
public enum CareDayMark
{
    None,
    Reading,
    Watered,
    Fed
}

/// <summary>One day in the calendar. <see cref="Future"/> is for days after today.</summary>
public sealed record CareCalendarDay(DateOnly Date, CareDayMark Mark, bool Future);

/// <summary>One month, with only the days it has.</summary>
public sealed record CareCalendarMonth(int Year, int Month, IReadOnlyList<CareCalendarDay> Days);

/// <summary>A year of waterings, feeds and meter readings for one plant, shown as a grid of days.</summary>
public sealed class CareCalendar
{
    public const int MaxMonths = 12;

    public IReadOnlyList<CareCalendarMonth> Months { get; }
    public int Waterings { get; }
    public int Feeds { get; }
    public int Readings { get; }

    private CareCalendar(IReadOnlyList<CareCalendarMonth> months, int waterings, int feeds, int readings)
    {
        Months = months;
        Waterings = waterings;
        Feeds = feeds;
        Readings = readings;
    }

    public bool IsEmpty => Months.Count == 0;

    /// <summary>The sentence read out for the grid.</summary>
    public string Summary =>
        $"Last 12 months: watered {Times(Waterings)}, fed {Times(Feeds)}, {Readings} meter {(Readings == 1 ? "reading" : "readings")}";

    private static string Times(int n) => n == 1 ? "1 time" : $"{n} times";

    /// <summary>The mark one entry gives its day, or None for the kinds the calendar doesn't show.</summary>
    public static CareDayMark MarkFor(CareLog log) => log.Kind switch
    {
        CareKind.Fertilised => CareDayMark.Fed,
        CareKind.Watered or CareKind.ToppedUp =>
            log.Products.Count > 0 || log.FeedId is not null ? CareDayMark.Fed : CareDayMark.Watered,
        CareKind.MoistureReading => CareDayMark.Reading,
        _ => CareDayMark.None
    };

    /// <summary>Newest month first, from the plant's first logged day, at most twelve months back.</summary>
    public static CareCalendar Build(IEnumerable<CareLog> logs, Guid plantId, DateOnly today)
    {
        var firstShown = new DateOnly(today.Year, today.Month, 1).AddMonths(-(MaxMonths - 1));
        var marks = new Dictionary<DateOnly, CareDayMark>();
        var all = new List<CareLog>();
        foreach (var log in logs)
        {
            if (log.IsDeleted || log.PlantId != plantId)
                continue;
            var mark = MarkFor(log);
            if (mark == CareDayMark.None)
                continue;
            all.Add(log);
            if (!marks.TryGetValue(log.OccurredOn, out var current) || mark > current)
                marks[log.OccurredOn] = mark;
        }

        var inRange = all.Where(l => l.OccurredOn >= firstShown && l.OccurredOn <= today).ToList();
        if (inRange.Count == 0)
            return new CareCalendar([], 0, 0, 0);

        var first = inRange.Min(l => l.OccurredOn);
        var start = new DateOnly(first.Year, first.Month, 1);
        var months = new List<CareCalendarMonth>();
        for (var m = new DateOnly(today.Year, today.Month, 1); m >= start; m = m.AddMonths(-1))
        {
            var days = Enumerable.Range(1, DateTime.DaysInMonth(m.Year, m.Month))
                .Select(d => new DateOnly(m.Year, m.Month, d))
                .Select(date => new CareCalendarDay(date, marks.GetValueOrDefault(date), date > today))
                .ToList();
            months.Add(new CareCalendarMonth(m.Year, m.Month, days));
        }

        var fed = inRange.Count(l => MarkFor(l) == CareDayMark.Fed);
        var watered = inRange.Count(l => l.Kind is CareKind.Watered or CareKind.ToppedUp);
        var readings = inRange.Count(l => l.Kind == CareKind.MoistureReading);
        return new CareCalendar(months, watered, fed, readings);
    }
}
