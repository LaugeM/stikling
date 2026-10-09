using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Today;

/// <summary>A plant on Today for watering, and what the app has worked out about it.</summary>
/// <param name="DueNow">True from the day it's due. False for one coming up in the next days.</param>
public sealed record WateringDue(Plant Plant, WateringGuess Guess, bool DueNow);

/// <summary>The plants Today suggests a look at for watering.</summary>
public static class WateringRound
{
    /// <summary>Plants due within this many days are shown ahead of time.</summary>
    public const int ComingUpDays = 2;

    /// <summary>
    /// The active plants with a reminder that are due, or due within two days, and haven't been
    /// put off. Those due now come first, the longest overdue at the top, then the ones coming
    /// up, and the plant's name decides between two on the same day. A plant still learning is
    /// left out. Whether the reminder is on at all is for the page to check.
    /// </summary>
    /// <param name="timeline">Timeline entries for any subject, for telling when plants were dormant.</param>
    /// <param name="pot">Finds a pot by id, to see whether it waters itself.</param>
    public static IReadOnlyList<WateringDue> Due(
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        Hemisphere hemisphere,
        TimeProvider time,
        PutOffs putOffs)
    {
        var today = time.Today();
        var all = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
        var careLogs = logs.Where(l => !l.IsDeleted).ToList();
        var history = timeline.ToList();
        var lightList = lights.ToList();
        var lit = all.ToDictionary(p => p.Id, p => GrowLights.IsLit(p, lightList, places));
        var factor = SeasonFactor(all, lit, careLogs, history, time);

        var due = new List<WateringDue>();
        foreach (var plant in all.Where(p => p.WateringReminder))
        {
            var guess = WateringGuess.Of(
                plant, careLogs, history, WateringForms.Of(plant, pot), lit[plant.Id], hemisphere, today, factor, time);
            if (guess.Mode == WateringMode.Learning || guess.CheckOn is not { } checkOn)
                continue;
            if (checkOn > today.AddDays(ComingUpDays) || putOffs.IsPutOff(PutOffs.Water(plant.Id, guess.LastWatered), today))
                continue;

            due.Add(new WateringDue(plant, guess, checkOn <= today));
        }

        return [.. due
            .OrderByDescending(d => d.DueNow)
            .ThenBy(d => d.Guess.CheckOn)
            .ThenBy(d => d.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// The guess for one plant, worked out the way Today does it, so the plant's page and Today
    /// say the same. Pass every plant, since the others' pace in this month is part of it.
    /// </summary>
    public static WateringGuess GuessFor(
        Plant plant,
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        Hemisphere hemisphere,
        TimeProvider time)
    {
        var today = time.Today();
        var all = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
        var careLogs = logs.Where(l => !l.IsDeleted).ToList();
        var history = timeline.ToList();
        var lightList = lights.ToList();
        var lit = all.ToDictionary(p => p.Id, p => GrowLights.IsLit(p, lightList, places));
        var factor = SeasonFactor(all, lit, careLogs, history, time);
        return WateringGuess.Of(
            plant, careLogs, history, WateringForms.Of(plant, pot),
            GrowLights.IsLit(plant, lightList, places), hemisphere, today, factor, time);
    }

    // How the other plants shift this month, from the plants without a light and while awake
    private static double? SeasonFactor(
        List<Plant> all, Dictionary<Guid, bool> lit, List<CareLog> careLogs, List<TimelineEntry> history, TimeProvider time) =>
        WateringSeasonFactor.For(
            all.Where(p => !lit[p.Id]).Select(p => (p, Gaps(p, careLogs, history, time))),
            time.Today().Month);

    private static IReadOnlyList<(DateOnly start, int days)> Gaps(
        Plant plant, List<CareLog> logs, List<TimelineEntry> history, TimeProvider time) =>
        [.. WateringGuess.Gaps(plant, logs, DormantPeriods.Of(plant, history, time), dormant: false)
            .Select(g => (g.Start, g.Days))];
}
