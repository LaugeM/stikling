using Stikling.Core.Models;

namespace Stikling.Core.Flowering;

/// <summary>
/// One flowering of a plant, worked out from its marks. One that was never marked done because a
/// later one began is closed with <see cref="EndUnknown"/>, and a Done with nothing before it is
/// kept as a flowering of its own so no mark is lost. (Not called Flowering, since that is also
/// the name of the namespace.)
/// </summary>
public sealed record FloweringPeriod(
    LooseDate? Buds,
    LooseDate? InFlower,
    LooseDate? Done,
    bool EndUnknown,
    IReadOnlyList<FloweringMark> Marks)
{
    /// <summary>When it began: the buds, else the flowers, else (for a lone Done) the end.</summary>
    public LooseDate Start => Buds ?? InFlower ?? Done!;

    /// <summary>True when the flowering has a start of its own, i.e. it isn't only a Done.</summary>
    public bool HasStart => Buds is not null || InFlower is not null;

    /// <summary>The last stage reached.</summary>
    public FloweringStage CurrentStage =>
        Done is not null ? FloweringStage.Done
        : InFlower is not null ? FloweringStage.InFlower
        : FloweringStage.Buds;

    /// <summary>Still going: not marked done and not closed by a later flowering.</summary>
    public bool IsOpen => Done is null && !EndUnknown;

    /// <summary>The latest day any of its marks was made for, which is when it was last confirmed.</summary>
    public DateOnly LastMarked => Marks.Max(m => m.On.Start);
}

/// <summary>Works out a plant's flowerings from its marks.</summary>
public static class Flowerings
{
    /// <summary>
    /// After this many days without a mark, an open flowering is taken as one whose end wasn't
    /// written down, like a past flowering added without an end. It closes with
    /// <see cref="FloweringPeriod.EndUnknown"/>, and the next mark starts a new one. It is longer
    /// than <see cref="FloweringNow.StaleAfterDays"/>, so "Still in flower?" is asked first.
    /// </summary>
    public const int ForgottenAfterDays = 365;

    /// <summary>
    /// The plant's flowerings, oldest first. Deleted marks and ones with an unknown stage are ignored.
    /// With <paramref name="today"/>, an open flowering forgotten by then is closed too.
    /// </summary>
    public static IReadOnlyList<FloweringPeriod> Of(IEnumerable<FloweringMark> marks, Guid plantId, DateOnly? today = null)
    {
        var sorted = marks
            .Where(m => !m.IsDeleted && m.PlantId == plantId && m.Stage is not null)
            .OrderBy(m => m.On.Start)
            .ThenBy(m => m.CreatedAt)
            .ToList();

        var all = new List<Builder>();
        Builder? open = null;

        foreach (var mark in sorted)
        {
            if (open is not null && Forgotten(open, mark.On.Start))
            {
                open.EndUnknown = true;
                open = null;
            }

            switch (mark.Stage)
            {
                case FloweringStage.Buds:
                    if (open is { InFlower: null })
                    {
                        // Buds again only confirms them
                        open.Marks.Add(mark);
                        break;
                    }
                    if (open is not null)
                        open.EndUnknown = true;
                    open = new Builder { Buds = mark.On };
                    open.Marks.Add(mark);
                    all.Add(open);
                    break;

                case FloweringStage.InFlower:
                    if (open is null)
                    {
                        open = new Builder { InFlower = mark.On };
                        all.Add(open);
                    }
                    else
                    {
                        open.InFlower ??= mark.On;
                    }
                    open.Marks.Add(mark);
                    break;

                case FloweringStage.Done:
                    if (open is not null)
                    {
                        open.Done = mark.On;
                        open.Marks.Add(mark);
                        open = null;
                    }
                    else if (all.Count > 0 && all[^1] is { Done: { } last } && last.Start == mark.On.Start)
                    {
                        // The same Done from two devices
                        all[^1].Marks.Add(mark);
                    }
                    else
                    {
                        var builder = new Builder { Done = mark.On };
                        builder.Marks.Add(mark);
                        all.Add(builder);
                    }
                    break;
            }
        }

        if (open is not null && today is { } day && Forgotten(open, day))
            open.EndUnknown = true;

        return all.Select(b => new FloweringPeriod(b.Buds, b.InFlower, b.Done, b.EndUnknown, b.Marks)).ToList();
    }

    private static bool Forgotten(Builder open, DateOnly day) =>
        day.DayNumber - open.Marks.Max(m => m.On.Start).DayNumber > ForgottenAfterDays;

    private sealed class Builder
    {
        public LooseDate? Buds { get; set; }
        public LooseDate? InFlower { get; set; }
        public LooseDate? Done { get; set; }
        public bool EndUnknown { get; set; }
        public List<FloweringMark> Marks { get; } = [];
    }
}

/// <summary>
/// Where a plant is in flower now. There is none unless its newest flowering is still open. One
/// not confirmed for a long time is <see cref="Stale"/>: it gets no badge, and the plant page asks
/// whether it is still in flower.
/// </summary>
public sealed record FloweringNow(FloweringStage Stage, bool Stale, FloweringPeriod Period)
{
    public const int StaleAfterDays = 120;

    public static FloweringNow? Of(IReadOnlyList<FloweringPeriod> flowerings, DateOnly today)
    {
        if (flowerings.Count == 0 || flowerings[^1] is not { IsOpen: true } newest)
            return null;

        var stale = today.DayNumber - newest.LastMarked.DayNumber > StaleAfterDays;
        return new FloweringNow(newest.CurrentStage, stale, newest);
    }
}

/// <summary>When a plant usually starts flowering, learned from its own records.</summary>
/// <param name="Month">The latest month any year started in. "Usually starts by April."</param>
/// <param name="LateThisYear">No flowering has started this year and that month has passed.</param>
/// <param name="Years">How many different years it is learned from.</param>
public sealed record UsualStart(int Month, bool LateThisYear, int Years)
{
    public static UsualStart? Of(IReadOnlyList<FloweringPeriod> flowerings, DateOnly today)
    {
        // A year alone can't say which month, and a lone Done has no start
        var starts = flowerings
            .Where(f => f.HasStart && f.Start.Precision != DatePrecision.Year)
            .Select(f => f.Start.Start)
            .ToList();

        var years = starts.Select(s => s.Year).Distinct().Count();
        if (years < 2)
            return null;

        var month = starts.Max(s => s.Month);
        var late = flowerings.All(f => f.Start.Year != today.Year)
            && today > new DateOnly(today.Year, month, DateTime.DaysInMonth(today.Year, month));
        return new UsualStart(month, late, years);
    }
}
