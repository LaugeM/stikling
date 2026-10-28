using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Today;

/// <summary>The room the daily round suggests, and how many plants and propagations are in it.</summary>
public sealed record DailyRoundSuggestion(Place Room, int Plants, int Propagations);

/// <summary>
/// The daily round: one room a day on Today, the one whose plants and propagations have gone
/// longest without anything logged on them.
/// </summary>
public static class DailyRound
{
    /// <summary>
    /// The room to suggest today, or null when the round is put off for the day, or no room has
    /// anything in it, or when round days are set and today isn't one. Only days before today count as activity, so the suggestion stays the same
    /// all day, also while the plants in the room are being gone over. A room nobody has logged
    /// anything in counts as the longest waiting, and the room's name decides between two equals.
    /// </summary>
    /// <param name="lastTimelineActivity">
    /// The newest moment per plant or propagation of a note, photos, or a look over, from before
    /// today (<see cref="Timeline.ITimelineRepository.GetLastActivityBeforeAsync"/>).
    /// </param>
    public static DailyRoundSuggestion? Suggest(
        Places places,
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IEnumerable<CareLog> logs,
        IReadOnlyDictionary<Guid, DateTimeOffset> lastTimelineActivity,
        PutOffs putOffs,
        TimeProvider time,
        IReadOnlyList<Weekday>? roundDays = null)
    {
        var today = time.Today();
        if (putOffs.IsPutOff(PutOffs.DailyRound, today))
            return null;
        // With round days set, the round is only on those days
        if (roundDays is { Count: > 0 } && !RoundSchedule.IsRoundDay(roundDays, today))
            return null;

        var activePlants = plants.Where(p => !p.IsDeleted && p.InCollection).ToList();
        var activePropagations = propagations.Where(p => !p.IsDeleted && p.IsActive).ToList();
        var roomOfPlant = activePlants.ToDictionary(p => p.Id, p => places.RoomOf(p.PlaceId)?.Id);

        // The latest day before today anything was logged, by room
        var last = new Dictionary<Guid, DateOnly>();
        void Seen(Guid? roomId, DateOnly day)
        {
            if (roomId is not { } id || day >= today)
                return;
            if (!last.TryGetValue(id, out var seen) || day > seen)
                last[id] = day;
        }

        foreach (var log in logs.Where(l => !l.IsDeleted))
        {
            if (roomOfPlant.TryGetValue(log.PlantId, out var room))
                Seen(room, log.OccurredOn);
        }
        foreach (var subject in activePlants.Select(p => (p.Id, p.PlaceId))
                     .Concat(activePropagations.Select(p => (p.Id, p.PlaceId))))
        {
            if (lastTimelineActivity.TryGetValue(subject.Id, out var moment))
                Seen(places.RoomOf(subject.PlaceId)?.Id, time.LocalDay(moment));
        }

        var plantCount = activePlants.Where(p => roomOfPlant[p.Id] is not null)
            .GroupBy(p => roomOfPlant[p.Id]!.Value).ToDictionary(g => g.Key, g => g.Count());
        var propagationCount = activePropagations.Select(p => places.RoomOf(p.PlaceId)?.Id)
            .OfType<Guid>().GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

        var candidates = places.Rooms
            .Where(r => (plantCount.GetValueOrDefault(r.Id) + propagationCount.GetValueOrDefault(r.Id)) > 0
                && !putOffs.IsPutOff(PutOffs.Round(r.Id), today))
            .OrderBy(r => last.TryGetValue(r.Id, out var day) ? day : DateOnly.MinValue)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return candidates.FirstOrDefault() is { } best
            ? new DailyRoundSuggestion(best, plantCount.GetValueOrDefault(best.Id), propagationCount.GetValueOrDefault(best.Id))
            : null;
    }

    /// <summary>How many of the plants due for a check on watering now are in the room, for the line on Today.</summary>
    public static int DueIn(Guid roomId, IEnumerable<WateringDue> due, Places places) =>
        due.Count(d => d.DueNow && places.RoomOf(d.Plant.PlaceId)?.Id == roomId);
}
