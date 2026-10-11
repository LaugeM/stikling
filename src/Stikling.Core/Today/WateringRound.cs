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
        var setup = new Setup(plants, logs, timeline, lights, places, time);

        var due = new List<WateringDue>();
        foreach (var plant in setup.Active.Where(p => p.WateringReminder))
        {
            var guess = setup.GuessOf(plant, pot, hemisphere);
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
        return new Setup(plants, logs, timeline, lights, places, time).GuessOf(plant, pot, hemisphere);
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
        Hemisphere hemisphere,
        TimeProvider time)
    {
        var setup = new Setup(plants, logs, timeline, lights, places, time);
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
        private readonly TimeProvider time;
        private readonly double? factor;

        public Setup(
            IEnumerable<Plant> plants, IEnumerable<CareLog> logs, IEnumerable<TimelineEntry> timeline,
            IEnumerable<GrowLight> lights, Places places, TimeProvider time)
        {
            Active = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
            this.logs = logs.Where(l => !l.IsDeleted).ToList();
            history = timeline.ToList();
            this.lights = lights.ToList();
            this.places = places;
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
                hemisphere, time.Today(), factor, time);

        private IReadOnlyList<(DateOnly start, int days)> Gaps(Plant plant) =>
            [.. WateringGuess.Gaps(plant, logs, DormantPeriods.Of(plant, history, time), dormant: false)
                .Select(g => (g.Start, g.Days))];
    }
}
