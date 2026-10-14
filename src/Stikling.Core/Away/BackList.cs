using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Away;

/// <summary>Why a plant or propagation is on the list of what to look at first.</summary>
public enum BackReason
{
    /// <summary>An open pest case covers it.</summary>
    Pest,

    /// <summary>A cutting in water, which nobody has looked at for a while.</summary>
    InWater,

    /// <summary>Past the pace it is usually watered at.</summary>
    PastPace,

    /// <summary>Flagged as needing something.</summary>
    Flagged
}

/// <summary>
/// One row of "Look at these first". Only what the reason needs is filled in.
/// </summary>
/// <param name="Subject">The <see cref="Plant"/> or <see cref="Propagation"/>.</param>
/// <param name="Pest">The pest, for <see cref="BackReason.Pest"/>.</param>
/// <param name="CaseStart">The day the case started, for <see cref="BackReason.Pest"/>.</param>
/// <param name="DaysSinceLogged">Days since anything was logged on it, for <see cref="BackReason.InWater"/>.</param>
/// <param name="UsualDays">The usual days between waterings (or to dry down), for <see cref="BackReason.PastPace"/>.</param>
/// <param name="DaysSince">Days since it was last watered, for <see cref="BackReason.PastPace"/>.</param>
/// <param name="FlagReason">What the flag says, for <see cref="BackReason.Flagged"/>.</param>
/// <param name="FlagSince">The day it was flagged, for <see cref="BackReason.Flagged"/>.</param>
public sealed record BackRow(
    Entity Subject,
    string Name,
    BackReason Reason,
    Pest? Pest = null,
    DateOnly? CaseStart = null,
    int? DaysSinceLogged = null,
    int? UsualDays = null,
    int? DaysSince = null,
    string? FlagReason = null,
    DateOnly? FlagSince = null);

/// <summary>
/// The plants and propagations worth a look first once someone is back. It is a list to look at,
/// never a list of what is overdue, and it shows for a few days after the day back.
/// </summary>
public static class BackList
{
    /// <summary>How many rows are shown when the setting is left alone.</summary>
    public const int DefaultLength = 5;

    /// <summary>
    /// The list, in order: pests, cuttings in water, plants past their usual pace, then flagged
    /// ones. Each shows once, under the first reason that fits. One that has been looked over, or
    /// watered or topped up, on or after the day back is off it.
    /// </summary>
    /// <param name="backDay">The day back, from <see cref="AwayTime.BackDay"/>.</param>
    /// <param name="length">The setting: null for 5, 0 for all of them.</param>
    public static IReadOnlyList<BackRow> Of(
        DateOnly backDay,
        IReadOnlyList<Plant> plants,
        IReadOnlyList<Propagation> propagations,
        IReadOnlyList<CareLog> logs,
        IReadOnlyList<TimelineEntry> timeline,
        IReadOnlyList<PestCase> cases,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        IReadOnlyList<AwayStretch> stretches,
        Hemisphere hemisphere,
        TimeProvider time,
        int? length)
    {
        var today = time.Today();
        var activePlants = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
        var activeCuttings = propagations.Where(p => !p.IsDeleted && p.IsActive).ToList();
        var liveLogs = logs.Where(l => !l.IsDeleted).ToList();
        var liveTimeline = timeline.Where(e => !e.IsDeleted).ToList();

        // Seen to since the day back
        var seen = liveTimeline
            .Where(e => TimelineEvents.Of(e) == TimelineEvent.LookedOver && time.LocalDay(e.OccurredAt) >= backDay)
            .Select(e => e.SubjectId)
            .Concat(liveLogs
                .Where(l => l.Kind is CareKind.Watered or CareKind.ToppedUp && l.OccurredOn >= backDay)
                .Select(l => l.PlantId))
            .ToHashSet();

        var rows = new List<BackRow>();
        var listed = new HashSet<Guid>();

        void Add(BackRow row)
        {
            if (!seen.Contains(row.Subject.Id) && listed.Add(row.Subject.Id))
                rows.Add(row);
        }

        // 1. Pests. An everywhere case covers everything, so it doesn't point at anything here.
        var withCase = activePlants
            .Select(p => (Plant: p, Case: PestService.CasesFor(p, cases, places).FirstOrDefault(c => c.Scope != PestScope.Everywhere)))
            .Where(x => x.Case is not null)
            .OrderBy(x => x.Case!.StartedOn)
            .ThenBy(x => x.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase);
        foreach (var (plant, found) in withCase)
            Add(new BackRow(plant, plant.DisplayName, BackReason.Pest, Pest: found!.Pest, CaseStart: found.StartedOn));

        // 2. Cuttings in water, the longest since anyone logged anything first
        var inWater = activeCuttings
            .Where(p => p.Medium == GrowingMedium.Water)
            .Select(p => (Cutting: p, Since: DaysSinceLogged(p, liveTimeline, liveLogs, time, today)))
            .OrderByDescending(x => x.Since)
            .ThenBy(x => x.Cutting.DisplayName, StringComparer.CurrentCultureIgnoreCase);
        foreach (var (cutting, since) in inWater)
            Add(new BackRow(cutting, cutting.ListName, BackReason.InWater, DaysSinceLogged: since));

        // 3. Past the usual pace, whether or not the reminder is on
        var guesses = WateringRound.GuessesForAll(activePlants, liveLogs, liveTimeline, pot, lights, places, stretches, hemisphere, time);
        var pastPace = activePlants
            .Select(p => (Plant: p, Guess: guesses.GetValueOrDefault(p.Id)))
            .Where(x => x.Guess is { Mode: WateringMode.Interval or WateringMode.Meter, DaysSince: { } since, UsualDays: { } usual }
                && since > usual)
            .Select(x => (x.Plant, Guess: x.Guess!, Over: (x.Guess!.DaysSince!.Value - x.Guess.UsualDays!.Value) / (double)x.Guess.UsualDays.Value))
            .OrderByDescending(x => x.Over)
            .ThenBy(x => x.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase);
        foreach (var (plant, guess, _) in pastPace)
            Add(new BackRow(plant, plant.DisplayName, BackReason.PastPace, UsualDays: guess.UsualDays, DaysSince: guess.DaysSince));

        // 4. Flagged, the longest ago first
        var flagged = activePlants.Select(p => (Subject: (Entity)p, p.DisplayName, p.Attention, p.AttentionLater))
            .Concat(activeCuttings.Select(p => (Subject: (Entity)p, DisplayName: p.ListName, p.Attention, p.AttentionLater)))
            .Where(x => x.Attention is not null && !AttentionLater.IsWaiting(x.Attention, x.AttentionLater, today))
            .OrderBy(x => x.Attention!.Since)
            .ThenBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase);
        foreach (var (subject, name, attention, _) in flagged)
            Add(new BackRow(subject, name, BackReason.Flagged, FlagReason: attention!.Reason, FlagSince: attention.Since));

        var shown = length ?? DefaultLength;
        return shown <= 0 ? rows : [.. rows.Take(shown)];
    }

    /// <summary>
    /// True while the list is showing: there is something on it and it hasn't been dismissed.
    /// While it does, Today holds back Water and Worth a look. Pests, flags and quarantine aren't held.
    /// </summary>
    public static bool IsHolding(DateOnly? backDay, IReadOnlyList<BackRow> rows, PutOffs putOffs, DateOnly today) =>
        backDay is { } day && rows.Count > 0 && !putOffs.IsPutOff(PutOffs.BackList(day), today);

    // Days since the newest entry on the timeline or care log, or since it was started
    private static int DaysSinceLogged(
        Propagation cutting, List<TimelineEntry> timeline, List<CareLog> logs, TimeProvider time, DateOnly today)
    {
        var last = cutting.StartedOn;
        foreach (var entry in timeline.Where(e => e.SubjectId == cutting.Id))
        {
            var day = time.LocalDay(entry.OccurredAt);
            if (day > last)
                last = day;
        }
        foreach (var log in logs.Where(l => l.PlantId == cutting.Id))
        {
            if (log.OccurredOn > last)
                last = log.OccurredOn;
        }
        return Math.Max(0, today.DayNumber - last.DayNumber);
    }
}
