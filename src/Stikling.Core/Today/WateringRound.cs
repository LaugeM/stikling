using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Today;

/// <summary>A plant on Today for watering, and what the app has worked out about it.</summary>
/// <param name="DueNow">True from the day it comes up on Today. False for one coming up in the next days.</param>
/// <param name="ShowOn">
/// The day it comes up on Today. The day it's due, or with round days the round day before it.
/// </param>
/// <param name="Gathered">
/// True when round days are set and the plant isn't one that dries out faster than the time
/// between them. A gathered plant that isn't due yet is returned so the page can count it for the
/// next round, but isn't listed under Coming up.
/// </param>
public sealed record WateringDue(Plant Plant, WateringGuess Guess, bool DueNow, DateOnly ShowOn, bool Gathered);

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
    /// With round days set, a plant comes up on the round day before it's due, but never before
    /// the day after it was last watered, read with the meter, or answered "Still wet". Plants
    /// that dry out faster than the time between round days keep their own days. A plant that
    /// comes up on a round day (<see cref="WateringDue.Gathered"/>) and isn't due yet is returned
    /// only when it comes up on or before the next round day, and isn't a Coming up plant.
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
        IReadOnlyList<AwayStretch> stretches,
        Hemisphere hemisphere,
        TimeProvider time,
        PutOffs putOffs,
        IReadOnlyList<Weekday>? roundDays = null)
    {
        var today = time.Today();
        var nextRound = RoundSchedule.Next(roundDays, today);
        var logList = logs.Where(l => !l.IsDeleted).ToList();
        var setup = new Setup(plants, logList, timeline, lights, places, stretches, time);

        var due = new List<WateringDue>();
        foreach (var plant in setup.Active.Where(p => p.WateringReminder))
        {
            var guess = setup.GuessOf(plant, pot, hemisphere);
            if (guess.Mode == WateringMode.Learning || guess.CheckOn is not { } checkOn)
                continue;
            if (putOffs.IsPutOff(PutOffs.Water(plant.Id, guess.LastWatered), today))
                continue;

            var showOn = checkOn;
            var gathered = false;
            if (roundDays is { Count: > 0 })
            {
                gathered = !(guess.UsualDays is { } usual && usual < RoundSchedule.LongestGap(roundDays));
                showOn = RoundSchedule.ShowOn(roundDays, checkOn, Earliest(plant, guess, logList, putOffs), guess.UsualDays);
            }

            var dueNow = showOn <= today;
            if (!dueNow && (gathered ? showOn > nextRound : checkOn > today.AddDays(ComingUpDays)))
                continue;

            due.Add(new WateringDue(plant, guess, dueNow, showOn, gathered));
        }

        return [.. due
            .OrderByDescending(d => d.DueNow)
            .ThenBy(d => d.ShowOn)
            .ThenBy(d => d.Guess.CheckOn)
            .ThenBy(d => d.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    // The first day a check may come up: after the plant was last watered, read with the meter or
    // answered "Still wet", which logs nothing and only leaves its put-off
    private static DateOnly Earliest(Plant plant, WateringGuess guess, List<CareLog> logs, PutOffs putOffs)
    {
        var earliest = DateOnly.MinValue;
        if (guess.LastWatered is { } watered)
            earliest = Later(earliest, watered.AddDays(1));
        var reading = logs
            .Where(l => l.PlantId == plant.Id && l.Kind == CareKind.MoistureReading)
            .Select(l => (DateOnly?)l.OccurredOn)
            .Max();
        if (reading is { } read)
            earliest = Later(earliest, read.AddDays(1));
        if (putOffs.EndedOn(PutOffs.Water(plant.Id, guess.LastWatered)) is { } ended)
            earliest = Later(earliest, ended);
        return earliest;
    }

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;

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
        IReadOnlyList<AwayStretch> stretches,
        Hemisphere hemisphere,
        TimeProvider time)
    {
        return new Setup(plants, logs, timeline, lights, places, stretches, time).GuessOf(plant, pot, hemisphere);
    }

    /// <summary>
    /// The guess for each active plant, whether or not it has a reminder, worked out the way
    /// Today does it. For the list on the day back, which looks at pace without reminding.
    /// </summary>
    internal static IReadOnlyDictionary<Guid, WateringGuess> GuessesForAll(
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        IReadOnlyList<AwayStretch> stretches,
        Hemisphere hemisphere,
        TimeProvider time)
    {
        var setup = new Setup(plants, logs, timeline, lights, places, stretches, time);
        return setup.Active.ToDictionary(p => p.Id, p => setup.GuessOf(p, pot, hemisphere));
    }

    /// <summary>
    /// The guess for each active plant with the reminder on, worked out the way Today does it.
    /// A plant still learning, or with nothing to check on, has none.
    /// </summary>
    internal static IReadOnlyDictionary<Guid, WateringGuess> GuessesFor(
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        IReadOnlyList<AwayStretch> stretches,
        Hemisphere hemisphere,
        TimeProvider time)
    {
        var setup = new Setup(plants, logs, timeline, lights, places, stretches, time);
        var guesses = new Dictionary<Guid, WateringGuess>();
        foreach (var plant in setup.Active.Where(p => p.WateringReminder))
        {
            var guess = setup.GuessOf(plant, pot, hemisphere);
            if (guess.Mode != WateringMode.Learning && guess.CheckOn is not null)
                guesses[plant.Id] = guess;
        }
        return guesses;
    }

    // What every guess is made from, worked out once: the other plants shift this month's pace
    private sealed class Setup
    {
        private readonly List<CareLog> logs;
        private readonly List<TimelineEntry> history;
        private readonly List<GrowLight> lights;
        private readonly Dictionary<Guid, bool> lit;
        private readonly Places places;
        private readonly IReadOnlyList<AwayStretch> stretches;
        private readonly TimeProvider time;
        private readonly double? factor;

        public Setup(
            IEnumerable<Plant> plants, IEnumerable<CareLog> logs, IEnumerable<TimelineEntry> timeline,
            IEnumerable<GrowLight> lights, Places places, IReadOnlyList<AwayStretch> stretches, TimeProvider time)
        {
            Active = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
            this.logs = logs.Where(l => !l.IsDeleted).ToList();
            history = timeline.ToList();
            this.lights = lights.ToList();
            this.places = places;
            this.stretches = stretches;
            this.time = time;
            lit = Active.ToDictionary(p => p.Id, p => GrowLights.IsLit(p, this.lights, places));

            // The plants without a light, while awake
            factor = WateringSeasonFactor.For(
                Active.Where(p => !lit[p.Id]).Select(p => (p, Gaps(p))),
                time.Today().Month);
        }

        public List<Plant> Active { get; }

        public WateringGuess GuessOf(Plant plant, Func<Guid, Pot?>? pot, Hemisphere hemisphere) =>
            WateringGuess.Of(
                plant, logs, history, WateringForms.Of(plant, pot),
                lit.TryGetValue(plant.Id, out var isLit) ? isLit : GrowLights.IsLit(plant, lights, places),
                hemisphere, time.Today(), factor, stretches, time);

        private IReadOnlyList<(DateOnly start, int days)> Gaps(Plant plant) =>
            [.. WateringGuess.Gaps(plant, logs, DormantPeriods.Of(plant, history, time), dormant: false, stretches)
                .Select(g => (g.Start, g.Days))];
    }
}
