using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;

namespace Stikling.Core.Today;

/// <summary>One plant or propagation on a round, with what the page needs to show it.</summary>
/// <param name="Subject">The <see cref="Plant"/> or <see cref="Propagation"/>.</param>
/// <param name="Heading">The spot it's in when the round is of a room, or its whole place on the due round. Null otherwise.</param>
/// <param name="Form">How a plant gets its water. Null for a propagation.</param>
/// <param name="Guess">What the app has worked out about watering. Null unless the reminder is on for the plant and it has something to say.</param>
/// <param name="DueNow">True when the guess says it's time to check. The page shows the reminder line only then.</param>
public sealed record RoundCard(
    Entity Subject, string Name, string? Heading, WateringForm? Form, WateringGuess? Guess, bool DueNow, bool Dormant);

/// <summary>A meter reading logged today on a card.</summary>
public sealed record RoundReading(Guid Id, int Value);

/// <summary>What has been logged today on one card.</summary>
/// <param name="WateredId">The watering, or topping up, logged today.</param>
/// <param name="LookedOverId">The timeline entry of a look over today.</param>
/// <param name="Photo">A photo, or a note with photos, was added today.</param>
public sealed record RoundDay(RoundReading? Reading, Guid? WateredId, Guid? LookedOverId, bool Photo)
{
    public static RoundDay Nothing { get; } = new(null, null, null, false);

    /// <summary>True when anything was logged today.</summary>
    public bool Any => Reading is not null || WateredId is not null || LookedOverId is not null || Photo;
}

/// <summary>What a finished round came to, counted from what is logged.</summary>
/// <param name="Skipped">Cards with nothing logged today.</param>
public sealed record RoundSummary(int Watered, int LookedOver, int Readings, int Photos, int Skipped);

/// <summary>
/// Going round a room, or the plants that are due, one card at a time. Everything here is worked
/// out from what is logged, so opening a round again later the same day picks up where it was.
/// </summary>
public static class GoingRound
{
    /// <summary>
    /// Everything active in a room, or in a spot. A room includes its spots. Plants come first,
    /// then propagations; in each, what is in the room itself comes first, then each spot by
    /// name, then by name. Dormant ones are kept and flagged. The heading is the spot a card is
    /// in, and none at all on a round of one spot.
    /// </summary>
    /// <param name="timeline">The change lines on plants' histories, for telling when plants were dormant.</param>
    /// <param name="wateringOn">The watering reminder in Settings. Without it no card has a guess.</param>
    /// <param name="putOffs">What is put off on Today. A plant answered "not yet" there isn't due here either.</param>
    public static IReadOnlyList<RoundCard> ForPlace(
        Guid placeId,
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        IReadOnlyList<AwayStretch> stretches,
        Hemisphere hemisphere,
        TimeProvider time,
        bool wateringOn,
        PutOffs? putOffs = null)
    {
        var scope = places.Find(placeId);
        if (scope is null)
            return [];

        var plantList = plants.ToList();
        var guesses = wateringOn
            ? WateringRound.GuessesFor(plantList, logs, timeline, pot, lights, places, stretches, hemisphere, time)
            : new Dictionary<Guid, WateringGuess>();
        var today = time.Today();

        var cards = Ordered(
            plantList.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active && places.IsIn(p.PlaceId, scope.Id)),
            p => p.PlaceId, p => p.DisplayName, scope, places)
            .Select(x =>
            {
                var plant = x.Item;
                var guess = guesses.GetValueOrDefault(plant.Id);
                return new RoundCard(
                    plant, plant.DisplayName, x.Heading, WateringForms.Of(plant, pot), guess,
                    guess is { CheckOn: { } checkOn } && checkOn <= today
                        && putOffs?.IsPutOff(PutOffs.Water(plant.Id, guess.LastWatered), today) != true,
                    plant.IsDormant);
            });

        var propagationCards = Ordered(
            propagations.Where(p => !p.IsDeleted && p.IsActive && places.IsIn(p.PlaceId, scope.Id)),
            p => p.PlaceId, p => p.DisplayName, scope, places)
            .Select(x => new RoundCard(x.Item, x.Item.ListName, x.Heading, null, null, false, x.Item.IsDormant));

        return [.. cards, .. propagationCards];
    }

    /// <summary>
    /// The plants Today says are due for a check on watering, in the order of the rooms: by room
    /// name, what is in the room itself before its spots, the spot's name, then the plant's name.
    /// Plants with no place come last. Propagations have no watering, so none are on it.
    /// </summary>
    public static IReadOnlyList<RoundCard> ForDue(
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
        IReadOnlyList<Weekday>? roundDays = null) =>
        InRoomOrder(
            WateringRound.Due(plants, logs, timeline, pot, lights, places, stretches, hemisphere, time, putOffs, roundDays)
                .Where(d => d.DueNow),
            places);

    /// <summary>
    /// The plants gathered onto the next round day, which aren't due yet, in the same order as
    /// <see cref="ForDue"/>. Empty when round days are off, or today is a round day.
    /// </summary>
    public static IReadOnlyList<RoundCard> ForNext(
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
        IReadOnlyList<Weekday>? roundDays)
    {
        var next = RoundSchedule.Next(roundDays, time.Today());
        return InRoomOrder(
            WateringRound.Due(plants, logs, timeline, pot, lights, places, stretches, hemisphere, time, putOffs, roundDays)
                .Where(d => d.Gathered && !d.DueNow && d.ShowOn == next),
            places);
    }

    private static IReadOnlyList<RoundCard> InRoomOrder(IEnumerable<WateringDue> due, Places places) =>
        [.. due
            .Select(d => (Due: d, Place: places.Find(d.Plant.PlaceId), Room: places.RoomOf(d.Plant.PlaceId)))
            .OrderBy(x => x.Room is null)
            .ThenBy(x => x.Room?.Name ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Place is { IsSpot: true } && x.Place.Id != x.Room?.Id)
            .ThenBy(x => x.Place is { IsSpot: true } && x.Place.Id != x.Room?.Id ? x.Place.Name : "",
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Due.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new RoundCard(
                x.Due.Plant, x.Due.Plant.DisplayName, places.NameOf(x.Due.Plant.PlaceId), x.Due.Guess.Form,
                x.Due.Guess, true, x.Due.Plant.IsDormant))];

    /// <summary>
    /// The cards for the list on the day back, in the order of the list. The heading is the plant's
    /// place. A plant has a guess when the reminder is on in Settings and for the plant, and
    /// a propagation has none.
    /// </summary>
    /// <param name="plants">Every plant, since the others' pace in this month is part of a guess.</param>
    public static IReadOnlyList<RoundCard> ForBack(
        IReadOnlyList<BackRow> rows,
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        IReadOnlyList<AwayStretch> stretches,
        Hemisphere hemisphere,
        TimeProvider time,
        bool wateringOn,
        PutOffs? putOffs = null)
    {
        var guesses = wateringOn
            ? WateringRound.GuessesFor(plants, logs, timeline, pot, lights, places, stretches, hemisphere, time)
            : new Dictionary<Guid, WateringGuess>();
        var today = time.Today();

        return [.. rows.Select(row =>
        {
            if (row.Subject is not Plant plant)
            {
                var propagation = (Propagation)row.Subject;
                return new RoundCard(propagation, row.Name, places.NameOf(propagation.PlaceId), null, null, false, propagation.IsDormant);
            }

            var guess = guesses.GetValueOrDefault(plant.Id);
            return new RoundCard(
                plant, row.Name, places.NameOf(plant.PlaceId), WateringForms.Of(plant, pot), guess,
                guess is { CheckOn: { } checkOn } && checkOn <= today
                    && putOffs?.IsPutOff(PutOffs.Water(plant.Id, guess.LastWatered), today) != true,
                plant.IsDormant);
        })];
    }

    /// <summary>What was logged today on each card, in the order of the cards.</summary>
    public static IReadOnlyList<RoundDay> Days(
        IReadOnlyList<RoundCard> cards,
        IEnumerable<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        TimeProvider time)
    {
        var today = time.Today();
        var careToday = logs
            .Where(l => !l.IsDeleted && l.OccurredOn == today)
            .ToLookup(l => l.PlantId);
        var entriesToday = timeline
            .Where(e => !e.IsDeleted && time.LocalDay(e.OccurredAt) == today)
            .ToLookup(e => e.SubjectId);

        return [.. cards.Select(card => DayOf(card, careToday[card.Subject.Id], entriesToday[card.Subject.Id]))];
    }

    /// <summary>What was logged today on one card.</summary>
    public static RoundDay DayOf(
        RoundCard card, IEnumerable<CareLog> logs, IEnumerable<TimelineEntry> timeline, TimeProvider time) =>
        Days([card], logs, timeline, time)[0];

    private static RoundDay DayOf(RoundCard card, IEnumerable<CareLog> care, IEnumerable<TimelineEntry> entries)
    {
        // The newest of the day, going by when it was saved and then by the order given
        var reading = care
            .Where(l => l.Kind == CareKind.MoistureReading && l.Moisture is not null)
            .OrderBy(l => l.CreatedAt)
            .LastOrDefault();
        var watered = card.Form is { } form
            ? care.Where(l => l.Kind == WateringForms.DoneKind(form)).OrderBy(l => l.CreatedAt).LastOrDefault()
            : null;
        var lookedOver = entries
            .Where(e => TimelineEvents.Of(e) == TimelineEvent.LookedOver)
            .OrderBy(e => e.CreatedAt)
            .LastOrDefault();
        var photo = entries.Any(e => e.Kind == TimelineKind.Photo || (e.Kind == TimelineKind.Note && e.PhotoIds.Count > 0));

        return new RoundDay(
            reading is null ? null : new RoundReading(reading.Id, reading.Moisture!.Value),
            watered?.Id,
            lookedOver?.Id,
            photo);
    }

    /// <summary>
    /// The first card with nothing logged today, which is where a round opened again picks up.
    /// The number of cards when every one has something.
    /// </summary>
    public static int StartAt(IReadOnlyList<RoundCard> cards, IReadOnlyList<RoundDay> days)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            if (i >= days.Count || !days[i].Any)
                return i;
        }
        return cards.Count;
    }

    /// <summary>
    /// What the round came to. A card looked over and watered counts as watered. A card with
    /// nothing logged is skipped.
    /// </summary>
    public static RoundSummary Summarise(IReadOnlyList<RoundDay> days) =>
        new(
            days.Count(d => d.WateredId is not null),
            days.Count(d => d.LookedOverId is not null && d.WateredId is null),
            days.Count(d => d.Reading is not null),
            days.Count(d => d.Photo),
            days.Count(d => !d.Any));

    // In the order of a round: the room itself, then each spot by name, then by name
    internal static IEnumerable<(T Item, string? Heading)> Ordered<T>(
        IEnumerable<T> items, Func<T, Guid?> placeOf, Func<T, string> nameOf, Place scope, Places places) =>
        items
            .Select(item => (Item: item, Place: places.Find(placeOf(item))))
            .Select(x => (x.Item, x.Place, InSpot: x.Place is not null && x.Place.Id != scope.Id))
            .OrderBy(x => x.InSpot)
            .ThenBy(x => x.InSpot ? x.Place!.Name : "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => nameOf(x.Item), StringComparer.CurrentCultureIgnoreCase)
            .Select(x => (x.Item, scope.IsSpot || !x.InSpot ? null : x.Place!.Name));
}
