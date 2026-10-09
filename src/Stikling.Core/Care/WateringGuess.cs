using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>How the app has come to its guess for a plant.</summary>
public enum WateringMode
{
    /// <summary>Not enough in the log yet to say anything.</summary>
    Learning,

    /// <summary>From the days between waterings.</summary>
    Interval,

    /// <summary>From how long the moisture meter took to fall to the watering level after a watering.</summary>
    Meter
}

/// <summary>
/// When a plant is next worth a look for watering, learned only from its own care log. It never
/// uses a care guide or a fixed interval, says nothing until there is enough to go on, and when
/// in doubt it guesses later. A reminder to water too early does more harm than a late one, so
/// the day is the day to check the plant, not the day to water it.
/// </summary>
/// <param name="LastWatered">The last day it got water, or null if the log has none.</param>
/// <param name="DaysSince">Days since then, never below 0.</param>
/// <param name="UsualDays">The usual days between waterings, or the usual days to dry down to the watering level in meter mode.</param>
/// <param name="LearnedFrom">How many waterings the estimate rests on.</param>
/// <param name="WateringsNeeded">While learning, how many more waterings it takes before it says anything.</param>
/// <param name="CheckOn">The day it's due. Null while learning.</param>
/// <param name="LatestReading">The newest meter reading since the last watering.</param>
/// <param name="SeasonAdjusted">True when the time of year changed the estimate.</param>
/// <param name="DaysShortening">True when the days are getting shorter and the plant isn't under a light.</param>
public sealed record WateringGuess(
    WateringForm Form,
    WateringMode Mode,
    DateOnly? LastWatered,
    int? DaysSince,
    int? UsualDays,
    int LearnedFrom,
    int WateringsNeeded,
    DateOnly? CheckOn,
    MoistureReading? LatestReading,
    bool SeasonAdjusted,
    bool DaysShortening)
{
    /// <summary>Gaps between waterings needed before the interval is trusted.</summary>
    public const int GapsNeeded = 3;

    // Every older sample counts this much less than the one after it
    private const double RecencyDecay = 0.8;

    // Samples from an earlier year count extra when it's the same time of year now
    private const double LastYearWeight = 1.5;

    /// <summary>
    /// How many days to wait before asking again when the meter still reads wet, a fraction of
    /// the usual time it takes to dry down. For the "Still wet" answer on Today.
    /// </summary>
    public static int StillWetWait(int usualDays) =>
        Math.Max(1, (int)Math.Round(usualDays / 4.0, MidpointRounding.AwayFromZero));

    // Anything that puts water in counts, the way topping up and flushing a reservoir does
    private static bool PutsWaterIn(CareKind kind) =>
        kind is CareKind.Watered or CareKind.Fertilised or CareKind.Flushed or CareKind.ToppedUp;

    private static IEnumerable<CareLog> OwnLogs(Plant plant, IEnumerable<CareLog> logs) =>
        logs.Where(l => !l.IsDeleted && l.PlantId == plant.Id
            && (plant.WateringSince is not { } since || l.OccurredOn >= since));

    /// <summary>The days the plant got water, oldest first, once each however many entries a day has.</summary>
    public static IReadOnlyList<DateOnly> Waterings(Plant plant, IEnumerable<CareLog> logs) =>
        OwnLogs(plant, logs).Where(l => PutsWaterIn(l.Kind)).Select(l => l.OccurredOn).Distinct().Order().ToList();

    /// <summary>
    /// The days between each watering and the next, with the day each started on, oldest first.
    /// Nothing is left out, so it holds the dormant stretches too.
    /// </summary>
    public static IReadOnlyList<(DateOnly Start, int Days)> Gaps(Plant plant, IEnumerable<CareLog> logs) =>
        GapsBetween(Waterings(plant, logs));

    /// <summary>The gaps that started while the plant was dormant, or the ones that didn't.</summary>
    public static IReadOnlyList<(DateOnly Start, int Days)> Gaps(
        Plant plant, IEnumerable<CareLog> logs, DormantPeriods periods, bool dormant) =>
        Gaps(plant, logs).Where(g => periods.Contains(g.Start) == dormant).ToList();

    private static List<(DateOnly Start, int Days)> GapsBetween(IReadOnlyList<DateOnly> days)
    {
        var gaps = new List<(DateOnly, int)>();
        for (var i = 0; i + 1 < days.Count; i++)
            gaps.Add((days[i], days[i + 1].DayNumber - days[i].DayNumber));
        return gaps;
    }

    /// <summary>Works out the guess for one plant.</summary>
    /// <param name="logs">Care entries for any plants. Only this plant's count.</param>
    /// <param name="timeline">Timeline entries for any subject. Only this plant's changes count, for telling when it was dormant.</param>
    /// <param name="lit">The plant is under a grow light, so the time of year says nothing about it.</param>
    /// <param name="seasonFactor">How much the collection's plants shift their pace this month, from <see cref="WateringSeasonFactor"/>.</param>
    public static WateringGuess Of(
        Plant plant,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        WateringForm form,
        bool lit,
        Hemisphere hemisphere,
        DateOnly today,
        double? seasonFactor,
        TimeProvider time)
    {
        var own = OwnLogs(plant, logs).ToList();
        var waterings = own.Where(l => PutsWaterIn(l.Kind)).Select(l => l.OccurredOn).Distinct().Order().ToList();
        var periods = DormantPeriods.Of(plant, timeline, time);
        var dormant = plant.IsDormant;
        var shortening = !lit && Seasons.DaysShortening(today, hemisphere);

        DateOnly? last = waterings.Count > 0 ? waterings[^1] : null;
        int? daysSince = last is { } l ? Math.Max(0, today.DayNumber - l.DayNumber) : null;

        // One reading a day, the one written last
        var readings = own
            .Where(r => r.Kind == CareKind.MoistureReading && r.Moisture is not null)
            .GroupBy(r => r.OccurredOn)
            .Select(g => (On: g.Key, Value: g.OrderBy(r => r.CreatedAt).Last().Moisture!.Value))
            .OrderBy(r => r.On)
            .ToList();

        MoistureReading? latest = null;
        if (last is { } lastDay && readings.Any(r => r.On > lastDay))
        {
            var newest = readings[^1];
            latest = new MoistureReading(newest.Value, newest.On, Math.Max(0, today.DayNumber - newest.On.DayNumber));
        }

        WateringGuess Make(WateringMode mode, int? usual, int learnedFrom, int needed, DateOnly? checkOn, bool adjusted) =>
            new(form, mode, last, daysSince, usual, learnedFrom, needed, checkOn, latest, adjusted, shortening);

        // Only what happened in the same state counts: a plant resting waters on another pace
        var inBucket = waterings.Where(d => periods.Contains(d) == dormant).ToList();

        // The meter, for a plant that dries out between waterings
        if (form == WateringForm.TopWatered && waterings.Count >= 2)
        {
            var waterAt = plant.WaterAt ?? Plant.DefaultWaterAt;
            var dry = DryDowns(waterings, readings, periods, dormant, waterAt);
            if (dry.Count > 0)
            {
                var (dryDays, adjusted) = Estimate(dry, today, lit, hemisphere, seasonFactor);
                var checkOn = MeterCheckOn(last!.Value, readings.Where(r => r.On > last.Value).ToList(), dryDays, waterAt);
                return Make(WateringMode.Meter, dryDays, dry.Count, 0, checkOn, adjusted);
            }
        }

        var gaps = GapsBetween(waterings).Where(g => periods.Contains(g.Start) == dormant).ToList();
        if (gaps.Count >= GapsNeeded && last is { } lastWatered)
        {
            var (days, adjusted) = Estimate(gaps, today, lit, hemisphere, seasonFactor);
            return Make(WateringMode.Interval, days, gaps.Count + 1, 0, lastWatered.AddDays(days), adjusted);
        }

        return Make(WateringMode.Learning, null, inBucket.Count, Math.Max(1, GapsNeeded + 1 - inBucket.Count), null, false);
    }

    // For each cycle that began in the right state: the days from the watering until the meter
    // first read the watering level or lower. A reading on the day of the next watering belongs to
    // the cycle that ends then, since it was most likely taken to decide on the watering.
    private static List<(DateOnly Start, int Days)> DryDowns(
        IReadOnlyList<DateOnly> waterings,
        IReadOnlyList<(DateOnly On, int Value)> readings,
        DormantPeriods periods,
        bool dormant,
        int waterAt)
    {
        var samples = new List<(DateOnly, int)>();
        for (var i = 0; i + 1 < waterings.Count; i++)
        {
            var (start, next) = (waterings[i], waterings[i + 1]);
            if (periods.Contains(start) != dormant)
                continue;

            var dry = readings.FirstOrDefault(r => r.On > start && r.On <= next && r.Value <= waterAt);
            if (dry != default)
                samples.Add((start, dry.On.DayNumber - start.DayNumber));
        }
        return samples;
    }

    private static DateOnly MeterCheckOn(
        DateOnly last, IReadOnlyList<(DateOnly On, int Value)> cycle, int dryDays, int waterAt)
    {
        if (cycle.Count == 0)
            return last.AddDays(dryDays);

        var (on, value) = cycle[^1];

        // The meter already says dry
        if (value <= waterAt)
            return on;

        // Falling over several readings: when the line through them reaches the watering level
        if (cycle.Count >= 2 && Slope(cycle) is { } slope and < 0)
        {
            var daysToGo = (int)Math.Ceiling((value - waterAt) / -slope);
            return on.AddDays(Math.Max(1, daysToGo));
        }

        // Still wet and no trend to go by: the usual time to dry, and not before a while from now
        var usual = last.AddDays(dryDays);
        var later = on.AddDays(StillWetWait(dryDays));
        return usual > later ? usual : later;
    }

    // Reading points per day, by least squares. Null with only one day to go by.
    private static double? Slope(IReadOnlyList<(DateOnly On, int Value)> points)
    {
        var meanX = points.Average(p => (double)p.On.DayNumber);
        var meanY = points.Average(p => (double)p.Value);
        var spread = points.Sum(p => Math.Pow(p.On.DayNumber - meanX, 2));
        if (spread == 0)
            return null;
        return points.Sum(p => (p.On.DayNumber - meanX) * (p.Value - meanY)) / spread;
    }

    /// <summary>
    /// The usual number of days from samples of (the day it started, how many days it took).
    /// Recent samples count most. When it isn't under a light, the time of year matters too:
    /// the same weeks of earlier years lead when there are enough, otherwise other plants' shift
    /// this month moves the plant's own median, and while the days shorten the guess leans
    /// to the slower end. The result is at least a day.
    /// </summary>
    internal static (int Days, bool Adjusted) Estimate(
        IReadOnlyList<(DateOnly Start, int Days)> samples,
        DateOnly today,
        bool lit,
        Hemisphere hemisphere,
        double? seasonFactor)
    {
        var ordered = samples.OrderBy(s => s.Start).ToList();
        var recent = ordered.Where(s => today.DayNumber - s.Start.DayNumber <= 365).ToList();
        if (recent.Count == 0)
            recent = ordered;

        var median = Whole(Percentile(Weigh(recent), 0.5));
        if (lit)
            return (median, false);

        var shortening = Seasons.DaysShortening(today, hemisphere);
        var lastYears = ordered
            .Where(s => today.DayNumber - s.Start.DayNumber > 300 && Seasons.SameTimeOfYear(s.Start, today))
            .ToList();

        double value;
        if (lastYears.Count >= 2)
            value = Percentile(Weigh(lastYears, LastYearWeight), 0.5);
        else if (seasonFactor is { } factor)
        {
            // Other plants can only make it later than this plant's own recent pace, never earlier.
            // In spring its own gaps get shorter by themselves as it is watered more often.
            var own = ordered.Select(s => s.Days).Order().ToList()[ordered.Count / 2] * factor;
            value = Math.Max(own, median);
        }
        else if (shortening)
            value = Percentile(Weigh(recent), 0.75);
        else
            value = median;

        var days = Whole(value);
        return (days, days != median);
    }

    private static int Whole(double days) => Math.Max(1, (int)Math.Round(days, MidpointRounding.AwayFromZero));

    // Newest first gets 1, and each one before it a bit less
    private static List<(int Days, double Weight)> Weigh(IReadOnlyList<(DateOnly Start, int Days)> byStart, double extra = 1)
    {
        var weighted = new List<(int, double)>();
        for (var i = 0; i < byStart.Count; i++)
            weighted.Add((byStart[i].Days, extra * Math.Pow(RecencyDecay, byStart.Count - 1 - i)));
        return weighted;
    }

    // Where the weights add up to the share. On a tie it takes the later one, which leans late.
    private static double Percentile(List<(int Days, double Weight)> weighted, double share)
    {
        var sorted = weighted.OrderBy(w => w.Days).ToList();
        var goal = sorted.Sum(w => w.Weight) * share + 1e-9;
        var sum = 0.0;
        foreach (var (days, weight) in sorted)
        {
            sum += weight;
            if (sum > goal)
                return days;
        }
        return sorted[^1].Days;
    }
}
