using Stikling.Core.Models;

namespace Stikling.Core.Away;

/// <summary>Stretches that overlap or touch, taken as one.</summary>
/// <param name="Until">The day Today asks again.</param>
/// <param name="Kind">Away if any stretch in it is Away, else Quiet.</param>
public sealed record AwaySpan(DateOnly From, DateOnly Until, AwayKind Kind, IReadOnlyList<AwayStretch> Stretches)
{
    /// <summary>True on the days from <see cref="From"/> up to, but not including, <see cref="Until"/>.</summary>
    public bool IsOn(DateOnly day) => From <= day && day < Until;

    /// <summary>
    /// The days actually away: from the first Away stretch's start to the last one's end, without
    /// quiet time that touches the trip. The span itself when there is no Away stretch in it.
    /// </summary>
    public AwaySpan Trip
    {
        get
        {
            var away = Stretches.Where(s => s.Kind == AwayKind.Away).ToList();
            return away.Count == 0 ? this : new(away.Min(s => s.From), away.Max(s => s.Until), AwayKind.Away, away);
        }
    }
}

/// <summary>Working out where today is among the stretches that have been set aside.</summary>
public static class AwayTime
{
    /// <summary>How many days the list on the day back shows: the day itself and the two after.</summary>
    public const int BackDays = 3;

    /// <summary>How many days ahead an Away stretch is mentioned on Today.</summary>
    public const int UpcomingDays = 7;

    /// <summary>
    /// The stretches as spans, oldest first. Ones that overlap or touch are one span, deleted ones
    /// are ignored.
    /// </summary>
    public static IReadOnlyList<AwaySpan> Spans(IEnumerable<AwayStretch> stretches)
    {
        var spans = new List<AwaySpan>();
        var group = new List<AwayStretch>();
        var until = default(DateOnly);

        foreach (var stretch in stretches.Where(s => !s.IsDeleted && s.Until > s.From).OrderBy(s => s.From).ThenBy(s => s.Until))
        {
            if (group.Count > 0 && stretch.From > until)
            {
                spans.Add(Close(group, until));
                group = [];
            }

            until = group.Count == 0 || stretch.Until > until ? stretch.Until : until;
            group.Add(stretch);
        }

        if (group.Count > 0)
            spans.Add(Close(group, until));
        return spans;

        static AwaySpan Close(List<AwayStretch> group, DateOnly until) =>
            new(group[0].From, until, group.Any(s => s.Kind == AwayKind.Away) ? AwayKind.Away : AwayKind.Quiet, group);
    }

    /// <summary>The span that is on today, or null.</summary>
    public static AwaySpan? Current(IEnumerable<AwayStretch> stretches, DateOnly today) =>
        Spans(stretches).FirstOrDefault(s => s.IsOn(today));

    /// <summary>True when a stretch is on that day.</summary>
    public static bool IsOn(IEnumerable<AwayStretch> stretches, DateOnly day) =>
        Current(stretches, day) is not null;

    /// <summary>The next Away span that starts within the coming week. A quiet one never counts.</summary>
    public static AwaySpan? Upcoming(IEnumerable<AwayStretch> stretches, DateOnly today) =>
        Spans(stretches).FirstOrDefault(s =>
            s.Kind == AwayKind.Away && s.From > today && s.From <= today.AddDays(UpcomingDays));

    /// <summary>
    /// The day back of the latest span that has ended within the last <see cref="BackDays"/> days,
    /// counting the day back itself. Null when there is none, or when another span is on today.
    /// </summary>
    public static DateOnly? BackDay(IEnumerable<AwayStretch> stretches, DateOnly today)
    {
        var spans = Spans(stretches);
        if (spans.Any(s => s.From <= today && today < s.Until))
            return null;
        return spans
            .Where(s => s.Until <= today && today < s.Until.AddDays(BackDays))
            .Select(s => (DateOnly?)s.Until)
            .LastOrDefault();
    }

    /// <summary>
    /// Whether a gap between two days, or the wait for a meter to read dry, left out of what the
    /// watering reminder learns. It is when it overlaps a stretch the plant wasn't watered in:
    /// nothing was meant to happen, so the length says nothing about its pace. A watering inside
    /// the stretch, e.g. by a plant-sitter, means it was looked after, so the gap counts.
    /// Touching a stretch doesn't overlap it.
    /// </summary>
    /// <param name="waterings">The days the plant got water.</param>
    public static bool LeavesOut(
        IEnumerable<AwayStretch> stretches, IReadOnlyCollection<DateOnly> waterings, DateOnly a, DateOnly b) =>
        stretches.Any(s => !s.IsDeleted
            && a < s.Until && b > s.From
            && !waterings.Any(d => s.From < d && d < s.Until));
}
