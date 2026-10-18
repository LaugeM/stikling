using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>
/// Things on Today put off for a while without logging anything, each under a key with the day
/// it comes back. They belong to the collection, so putting something off hides it for everyone,
/// and each one is saved as a <see cref="PutOff"/> record that goes in backups.
/// </summary>
public sealed class PutOffs
{
    /// <summary>The choices offered: tomorrow, in 3 days and in a week.</summary>
    public static readonly IReadOnlyList<int> Days = [1, 3, 7];

    /// <summary>The key for the monthly photo reminder as a whole.</summary>
    public const string PhotoRound = "photos";

    private readonly Dictionary<string, DateOnly> until;
    private readonly Dictionary<string, DateOnly> ended;

    private PutOffs(Dictionary<string, DateOnly> until, Dictionary<string, DateOnly>? ended = null)
    {
        this.until = until;
        this.ended = ended ?? new(until);
    }

    public static PutOffs Empty() => new([]);

    /// <summary>The key for a propagation waiting to be looked at.</summary>
    public static string Check(Guid propagationId) => $"check:{propagationId}";

    /// <summary>
    /// The key for a flag. The day it was raised is part of it, so a flag cleared and raised
    /// again later isn't still hidden.
    /// </summary>
    public static string Flag(Guid subjectId, Attention attention) =>
        $"attention:{subjectId}:{attention.Since:yyyy-MM-dd}";

    /// <summary>
    /// The key for a problem to look at again. The look again day is part of it, so a new day
    /// picked later isn't still hidden by an old put-off.
    /// </summary>
    public static string Problem(Guid problemId, DateOnly lookAgainOn) =>
        $"problem:{problemId}:{lookAgainOn:yyyy-MM-dd}";

    /// <summary>
    /// The key for a quarantine that is up. The day it started is part of it, so a plant put back
    /// in quarantine later isn't still hidden.
    /// </summary>
    public static string Quarantine(Guid plantId, DateOnly since) =>
        $"quarantine:{plantId}:{since:yyyy-MM-dd}";

    /// <summary>
    /// The key for asking whether a plant has gone dormant. The day the asking window started is
    /// part of it, so next year's season isn't still hidden.
    /// </summary>
    public static string Rest(Guid plantId, DateOnly windowStart) => $"rest:{plantId}:{windowStart:yyyy-MM-dd}";

    /// <summary>The key for a pest case that is ready to be resolved, so Today can wait with asking.</summary>
    public static string Resolve(Guid caseId) => $"resolve:{caseId}";

    /// <summary>
    /// The key for a plant that's due for water. The day it was last watered is part of it, so
    /// once it has been watered the next reminder isn't still hidden.
    /// </summary>
    public static string Water(Guid plantId, DateOnly? lastWatered) =>
        $"water:{plantId}:{lastWatered:yyyy-MM-dd}";

    /// <summary>
    /// The key for a plant that's due for a care interval. The day it was last done is part of it,
    /// so once it has been done the next one isn't still hidden.
    /// </summary>
    public static string Interval(Guid intervalId, Guid plantId, DateOnly? last) =>
        $"interval:{intervalId}:{plantId}:{last:yyyy-MM-dd}";

    /// <summary>
    /// The key for the list of what to look at first on the day back ("Done with this list"). The
    /// day back is part of it, so a later trip gets a list of its own.
    /// </summary>
    public static string BackList(DateOnly backDay) => $"back:{backDay:yyyy-MM-dd}";

    /// <summary>
    /// The key for the daily round as a whole. Put off until tomorrow when it's finished or
    /// skipped for the day.
    /// </summary>
    public const string DailyRound = "round";

    /// <summary>The key for one room put off, so the daily round suggests the next one.</summary>
    public static string Round(Guid roomId) => $"round:{roomId}";

    public bool IsPutOff(string key, DateOnly today) =>
        until.TryGetValue(key, out var back) && today < back;

    /// <summary>Hides it until the given day, when it shows again.</summary>
    public void PutOff(string key, DateOnly back) => until[key] = ended[key] = back;

    /// <summary>
    /// The day the latest put-off under the key comes back, also when it has expired, or null when
    /// there never was one. It's the only trace of a check answered with "Still wet" when no meter
    /// reading is logged.
    /// </summary>
    public DateOnly? EndedOn(string key) => ended.TryGetValue(key, out var day) ? day : null;

    /// <summary>
    /// Builds it from the saved records, keeping only what hasn't come back yet as put off. The
    /// records of put-offs that have expired stay in the store, and only <see cref="EndedOn"/> sees them.
    /// </summary>
    public static PutOffs From(IEnumerable<PutOff> saved, DateOnly today)
    {
        var live = saved.Where(p => !p.IsDeleted).ToList();
        return new(
            live.Where(p => p.Until > today).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Max(p => p.Until)),
            live.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Max(p => p.Until)));
    }
}
