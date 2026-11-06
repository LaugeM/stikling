using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>One way a failed batch stood apart from the rooted batches of its type. A fact from the records, never a cause.</summary>
public abstract record FailedDifference;

/// <param name="Same">How many of the rooted batches were in the same medium.</param>
/// <param name="MostUsed">The medium most of the rooted batches were in.</param>
public sealed record MediumDifference(GrowingMedium Medium, int Same, GrowingMedium MostUsed, int MostUsedCount) : FailedDifference;

/// <summary>It had an aid few of the rooted batches had.</summary>
public sealed record AidUsedDifference(RootingAid Aid, int RootedWith) : FailedDifference;

/// <summary>It didn't have an aid most of the rooted batches had.</summary>
public sealed record AidNotUsedDifference(RootingAid Aid, int RootedWith) : FailedDifference;

/// <param name="Month">The month it was started, 1 to 12.</param>
/// <param name="Around">How many rooted batches were started within a month of that.</param>
/// <param name="MostUsedMonth">The month most rooted batches were started in, 1 to 12.</param>
public sealed record StartMonthDifference(int Month, int Around, int MostUsedMonth, int MostUsedCount) : FailedDifference;

/// <param name="DaysHere">Days between water changes on this batch. Null when it has none logged.</param>
/// <param name="DaysUsual">The middle value of the days between changes on the rooted batches compared.</param>
/// <param name="Compared">How many rooted batches have water changes logged.</param>
public sealed record WaterChangeDifference(int? DaysHere, int DaysUsual, int Compared) : FailedDifference;

/// <summary>What a failed batch is compared with, and what stood out.</summary>
/// <param name="Rooted">How many of your batches of the same type have rooted.</param>
public sealed record FailedComparison(int Rooted, IReadOnlyList<FailedDifference> Differences)
{
    public bool HasEnough => Rooted >= FailedComparisons.MinimumRooted;

    public bool NothingStandsOut => HasEnough && Differences.Count == 0;
}

/// <summary>
/// How a failed propagation differed from the batches of the same type that rooted: the medium,
/// the rooting aids, the month it was started and how often the water was changed. Worked out from
/// what is recorded, so nothing is stored.
/// </summary>
public static class FailedComparisons
{
    /// <summary>With fewer rooted batches than this, any difference would be chance.</summary>
    public const int MinimumRooted = 3;

    /// <summary>Few: at most this share of the rooted batches had the same thing.</summary>
    private const double FewShare = 0.25;

    /// <summary>Most: at least this share of the rooted batches had it.</summary>
    private const double MostShare = 0.75;

    /// <summary>How many times longer or shorter the gap between water changes has to be to count.</summary>
    private const double WaterRatio = 1.5;

    /// <summary>Gaps closer than this many days are about the same, however small they are.</summary>
    private const int WaterMinimumDays = 2;

    /// <summary>The comparison for a failed propagation, or null for one that isn't failed or is deleted.</summary>
    public static FailedComparison? For(
        Propagation failed,
        IEnumerable<Propagation> propagations,
        IEnumerable<PropagationCareLog> careLogs)
    {
        if (failed.IsDeleted || failed.Stage != PropagationStage.Failed)
            return null;

        var rooted = propagations
            .Where(p => !p.IsDeleted && p.Id != failed.Id && p.Type == failed.Type && HasRooted(p))
            .ToList();
        if (rooted.Count < MinimumRooted)
            return new FailedComparison(rooted.Count, []);

        var differences = new List<FailedDifference>();
        if (Medium(failed, rooted) is { } medium)
            differences.Add(medium);
        differences.AddRange(Aids(failed, rooted));
        if (StartMonth(failed, rooted) is { } month)
            differences.Add(month);
        if (WaterChanges(failed, rooted, careLogs.Where(l => !l.IsDeleted && l.Kind == PropagationCareKind.ChangedWater).ToList()) is { } water)
            differences.Add(water);
        return new FailedComparison(rooted.Count, differences);
    }

    /// <summary>It got roots: a rooted date, or units potted up from one that went straight there. A batch that failed doesn't count.</summary>
    private static bool HasRooted(Propagation p) =>
        p.Stage != PropagationStage.Failed && (p.RootedOn is not null || p.PottedUpCount > 0 || p.Stage == PropagationStage.Rooted);

    private static bool IsFew(int count, int total) => count <= total * FewShare;

    private static MediumDifference? Medium(Propagation failed, List<Propagation> rooted)
    {
        var same = rooted.Count(p => p.Medium == failed.Medium);
        if (!IsFew(same, rooted.Count))
            return null;
        var most = rooted.GroupBy(p => p.Medium).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
        return new MediumDifference(failed.Medium, same, most.Key, most.Count());
    }

    private static IEnumerable<FailedDifference> Aids(Propagation failed, List<Propagation> rooted)
    {
        foreach (var aid in Enum.GetValues<RootingAid>())
        {
            var with = rooted.Count(p => p.RootingAids.Contains(aid));
            if (failed.RootingAids.Contains(aid))
            {
                if (IsFew(with, rooted.Count))
                    yield return new AidUsedDifference(aid, with);
            }
            else if (with >= rooted.Count * MostShare)
            {
                yield return new AidNotUsedDifference(aid, with);
            }
        }
    }

    private static StartMonthDifference? StartMonth(Propagation failed, List<Propagation> rooted)
    {
        var month = failed.StartedOn.Month;
        var around = rooted.Count(p => FairComparison.MonthsApart(p.StartedOn.Month, month) <= 1);
        if (!IsFew(around, rooted.Count))
            return null;
        var most = rooted.GroupBy(p => p.StartedOn.Month).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
        return new StartMonthDifference(month, around, most.Key, most.Count());
    }

    /// <summary>
    /// Only for a batch in water. A batch with no changes logged can have had its water changed without
    /// anyone writing it down, so a rooted batch without any isn't counted, and one here without any is
    /// only mentioned when it ran long enough for a change to be expected.
    /// </summary>
    private static WaterChangeDifference? WaterChanges(Propagation failed, List<Propagation> rooted, List<PropagationCareLog> changes)
    {
        if (!PropagationCareService.KindsFor(failed.Medium).Contains(PropagationCareKind.ChangedWater))
            return null;

        var byBatch = changes.GroupBy(l => l.PropagationId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.OccurredOn).Distinct().ToList());

        var gaps = rooted
            .Where(p => PropagationCareService.KindsFor(p.Medium).Contains(PropagationCareKind.ChangedWater))
            .Select(p => DaysBetweenChanges(p, p.RootedOn ?? p.FinishedOn, byBatch.GetValueOrDefault(p.Id)))
            .OfType<double>()
            .Order()
            .ToList();
        if (gaps.Count < MinimumRooted)
            return null;

        var usual = gaps.Count % 2 == 1 ? gaps[gaps.Count / 2] : (gaps[gaps.Count / 2 - 1] + gaps[gaps.Count / 2]) / 2;
        var usualDays = Rounded(usual);

        if (!byBatch.TryGetValue(failed.Id, out var own))
        {
            var ran = failed.FinishedOn is { } finished ? finished.DayNumber - failed.StartedOn.DayNumber : 0;
            return ran >= usual * 2 && usualDays >= WaterMinimumDays ? new WaterChangeDifference(null, usualDays, gaps.Count) : null;
        }

        if (DaysBetweenChanges(failed, failed.FinishedOn, own) is not { } here)
            return null;
        var far = here >= usual * WaterRatio || here * WaterRatio <= usual;
        var hereDays = Rounded(here);
        return far && Math.Abs(hereDays - usualDays) >= WaterMinimumDays
            ? new WaterChangeDifference(hereDays, usualDays, gaps.Count)
            : null;
    }

    /// <summary>The days it ran over the days the water was changed. Null without any changes or a stretch of time to go on.</summary>
    private static double? DaysBetweenChanges(Propagation p, DateOnly? end, List<DateOnly>? days)
    {
        if (days is null || days.Count == 0)
            return null;
        var until = end ?? days.Max();
        var span = until.DayNumber - p.StartedOn.DayNumber;
        return span <= 0 ? null : span / (double)days.Count;
    }

    private static int Rounded(double days) => (int)Math.Round(days, MidpointRounding.AwayFromZero);
}
