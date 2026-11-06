using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Rooms;

/// <summary>How a plant's time in a place ended, or that it hasn't.</summary>
public enum SpotStayEnding
{
    StillThere,
    MovedOn,
    Died,
    GivenAway,
    Sold
}

/// <summary>One unbroken stretch a plant stood in a place. Moves between spots in a room count as one when looking at the room.</summary>
/// <param name="From">The day it came. Null when the history doesn't say.</param>
/// <param name="FromLogged">False when the history has no line for it coming, so <paramref name="From"/> is the day it was added to the app and it may have stood there longer.</param>
/// <param name="To">The day it ended. Null while it's still there and when the history doesn't say.</param>
/// <param name="Spots">The spots it stood in, oldest first, when looking at a room. Empty for time in the room itself.</param>
public sealed record SpotStay(
    DateOnly? From, bool FromLogged, DateOnly? To, SpotStayEnding Ending, IReadOnlyList<string> Spots);

/// <summary>A plant that has stood in a place, and each time it did, newest first.</summary>
public sealed record SpotPlant(Plant Plant, IReadOnlyList<SpotStay> Stays);

/// <summary>
/// What has lived in a room or spot, read from the room lines in the plants' histories. It only says what
/// happened, and never adds it up into a verdict on the place. Nothing is stored for it.
/// </summary>
/// <remarks>
/// History lines hold the name a place had at the time, not its id. A name is matched to the places as they are now,
/// then to the names of places that were merged, and then to what the plants' own histories show: the line that
/// ends a stay can use a later name than the line that started it, and a plant's newest line goes with where
/// it is now. A rename that nothing shows leaves that stay out rather than guessing.
/// </remarks>
public static class SpotHistory
{
    private static readonly Func<Enum, string> EnumName = e => e.ToString();

    /// <summary>
    /// The plants that have stood in the place or, for a room, in a spot inside it: the ones there now first, then the
    /// ones that were there most recently. Deleted plants are left out, and so are plants that live with someone else.
    /// </summary>
    /// <param name="changes">The change entries on plants' histories, in any order.</param>
    public static IReadOnlyList<SpotPlant> Of(
        Guid placeId,
        Places places,
        IEnumerable<Plant> plants,
        IEnumerable<TimelineEntry> changes,
        Func<DateTimeOffset, DateOnly> localDay)
    {
        var names = new NameLookup(places);
        var entries = changes
            .Where(e => !e.IsDeleted && e.Kind == TimelineKind.Change && e.SubjectType == SubjectType.Plant)
            .ToLookup(e => e.SubjectId);

        var drafts = plants
            .Where(p => !p.IsDeleted && !p.LivesElsewhere)
            .OrderBy(p => p.Id)
            .Select(p =>
            {
                var ordered = Ordered(entries[p.Id]);
                return (Plant: p, Entries: ordered, Stays: ReadStays(p, ordered, names, localDay));
            })
            .ToList();

        names.Learn(drafts.Select(d => (d.Plant, d.Stays)));

        var result = new List<SpotPlant>();
        foreach (var (plant, ordered, stays) in drafts)
        {
            Settle(plant, stays, ordered, places, names, localDay);
            var shown = InPlace(stays, placeId, places);
            if (shown.Count > 0)
                result.Add(new SpotPlant(plant, shown));
        }

        return result
            .OrderByDescending(p => p.Stays[0].Ending == SpotStayEnding.StillThere)
            .ThenByDescending(p => p.Stays[0].To ?? p.Stays[0].From ?? DateOnly.MinValue)
            .ThenBy(p => p.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Plant.Id)
            .ToList();
    }

    private static List<TimelineEntry> Ordered(IEnumerable<TimelineEntry> entries) =>
        entries.OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();

    // One stay while it's being worked out
    private sealed class Stay
    {
        public string? OpenName;
        public string? CloseName;
        public Guid? PlaceId;
        public DateOnly? From;
        public bool FromLogged;
        public DateOnly? To;
        public bool Open = true;
        public SpotStayEnding Ending = SpotStayEnding.MovedOn;

        // Started by the same line that ended the one before, so there's no gap between them
        public bool FollowsOn;
    }

    private static List<Stay> ReadStays(
        Plant plant, List<TimelineEntry> entries, NameLookup names, Func<DateTimeOffset, DateOnly> localDay)
    {
        var stays = new List<Stay>();
        Stay? current = null;

        foreach (var entry in entries)
        {
            var day = localDay(entry.OccurredAt);
            foreach (var (from, to) in RoomLines(entry, names))
            {
                // The place it left, when the history hasn't said it came there. Before anything else
                // it was there from when it was added, as far as the history can tell.
                if (from is not null && current is null)
                {
                    current = new Stay
                    {
                        OpenName = from,
                        From = stays.Count == 0 ? Earlier(localDay(plant.CreatedAt), day) : null
                    };
                    stays.Add(current);
                }

                var followsOn = from is not null;
                if (current is not null)
                {
                    current.CloseName = from;
                    current.To = day;
                    current.Open = false;
                    current = null;
                }

                if (to is not null)
                {
                    current = new Stay { OpenName = to, From = day, FromLogged = true, FollowsOn = followsOn };
                    stays.Add(current);
                }
            }
        }

        return stays;
    }

    private static DateOnly Earlier(DateOnly a, DateOnly b) => a < b ? a : b;

    // The moves in an entry as where it left and where it went. Removed from a room has no "to", and
    // a removal that doesn't name the room has neither.
    private static IEnumerable<(string? From, string? To)> RoomLines(TimelineEntry entry, NameLookup names)
    {
        if (entry.Lines is { } lines)
        {
            foreach (var line in lines)
                if (RoomLine(line) is { } move)
                    yield return move;
            yield break;
        }

        foreach (var text in (entry.Text ?? "").Split('\n'))
        {
            if (!text.StartsWith("Moved ", StringComparison.Ordinal)
                && !text.StartsWith("Placed in ", StringComparison.Ordinal)
                && !text.StartsWith("Removed from ", StringComparison.Ordinal))
                continue;

            if (HistoryText.Parse(text, EnumName)[0] is { } line && RoomLine(line) is { } move)
                yield return line.Key == "MovedFromTo" ? BestSplit(move.From!, move.To!, names) : move;
        }
    }

    private static (string? From, string? To)? RoomLine(HistoryLine line) => line.Key switch
    {
        "MovedFromTo" when line.Args is [{ Text: { } from }, { Text: { } to }] => (from, to),
        "MovedTo" or "PlacedIn" when line.Args is [{ Text: { } to }] => (null, to),
        "RemovedFrom" when line.Args is [{ Text: { } from }] => (from, null),
        "RemovedFromRoom" => (null, null),
        _ => null
    };

    // Old lines are plain text, and "Moved from Next to the window to Bedroom" can be cut in more than
    // one place. The cut where the names are places that exist is the right one.
    private static (string? From, string? To) BestSplit(string from, string to, NameLookup names)
    {
        const string Word = " to ";
        var whole = from + Word + to;

        (string From, string To)? oneKnown = null;
        for (var cut = whole.IndexOf(Word, StringComparison.Ordinal); cut >= 0; cut = whole.IndexOf(Word, cut + 1, StringComparison.Ordinal))
        {
            var (a, b) = (whole[..cut], whole[(cut + Word.Length)..]);
            var (knowsA, knowsB) = (names.Resolve(a) is not null, names.Resolve(b) is not null);
            if (knowsA && knowsB)
                return (a, b);
            if (knowsA || knowsB)
                oneKnown ??= (a, b);
        }

        return oneKnown is { } found ? (found.From, found.To) : (from, to);
    }

    // Decides which place each stay was in, adds the stay the history doesn't have for where the plant
    // is now, and works in how the plant's time ended
    private static void Settle(
        Plant plant, List<Stay> stays, List<TimelineEntry> entries, Places places, NameLookup names, Func<DateTimeOffset, DateOnly> localDay)
    {
        foreach (var stay in stays)
            stay.PlaceId = names.Resolve(stay.OpenName) ?? names.Resolve(stay.CloseName);

        var now = places.Find(plant.PlaceId)?.Id;
        var last = stays.Count > 0 ? stays[^1] : null;

        if (last is { Open: true })
        {
            if (now is { } here && (last.PlaceId is null || last.PlaceId == here || RoomName.Same(last.OpenName, places.NameOf(here))))
            {
                last.PlaceId = here;
                last.Ending = SpotStayEnding.StillThere;
            }
            else
            {
                // It's somewhere else now, or nowhere, with no line saying when it left
                last.Open = false;
                if (now is not null)
                    stays.Add(new Stay { PlaceId = now, Ending = SpotStayEnding.StillThere });
            }
        }
        else if (now is not null)
        {
            // No room change logged at all, or one is missing since it last left a place
            stays.Add(new Stay
            {
                PlaceId = now,
                From = stays.Count == 0 ? localDay(plant.CreatedAt) : null,
                Ending = SpotStayEnding.StillThere
            });
        }

        ApplyStatus(plant, stays, entries, localDay);
    }

    // A plant that died or left ended its time where it stood that day. Moving it afterwards tidies the records,
    // so it doesn't count as having stood there.
    private static void ApplyStatus(Plant plant, List<Stay> stays, List<TimelineEntry> entries, Func<DateTimeOffset, DateOnly> localDay)
    {
        SpotStayEnding? ending = plant.Status switch
        {
            PlantStatus.Died => SpotStayEnding.Died,
            PlantStatus.GivenAway => SpotStayEnding.GivenAway,
            PlantStatus.Sold => SpotStayEnding.Sold,
            _ => null
        };
        if (ending is not { } how)
            return;

        var day = plant.StatusSince ?? LastStatusDay(entries, localDay);
        if (day is { } on)
            stays.RemoveAll(s => s.FromLogged && s.From > on);
        if (stays.Count == 0)
            return;

        // It had already left the place when this happened
        var last = stays[^1];
        if (day is { } ended && last.To is { } to && to < ended)
            return;

        last.Ending = how;
        last.To = day ?? last.To;
    }

    private static DateOnly? LastStatusDay(List<TimelineEntry> entries, Func<DateTimeOffset, DateOnly> localDay)
    {
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var isStatus = entries[i].Lines is { } lines
                ? lines.Any(l => l.Key == "StatusChanged")
                : (entries[i].Text ?? "").Split('\n').Any(l => l.StartsWith("Status: ", StringComparison.Ordinal));
            if (isStatus)
                return localDay(entries[i].OccurredAt);
        }
        return null;
    }

    // The plant's stays in this place, newest first, with stays that follow straight on from each other as one
    private static List<SpotStay> InPlace(List<Stay> stays, Guid placeId, Places places)
    {
        var shown = new List<SpotStay>();
        var followsShown = false;
        foreach (var stay in stays)
        {
            if (!places.IsIn(stay.PlaceId, placeId))
            {
                followsShown = false;
                continue;
            }

            string[] spots = places.Find(stay.PlaceId) is { IsSpot: true } place && place.Id != places.Find(placeId)?.Id
                ? [place.Name]
                : [];

            if (followsShown && stay.FollowsOn)
            {
                shown[^1] = shown[^1] with
                {
                    To = stay.To,
                    Ending = stay.Ending,
                    Spots = [.. shown[^1].Spots.Concat(spots).Distinct(StringComparer.OrdinalIgnoreCase)]
                };
            }
            else
                shown.Add(new SpotStay(stay.From, stay.FromLogged, stay.To, stay.Ending, spots));

            followsShown = true;
        }

        shown.Reverse();
        return shown;
    }

    // Turns a name written in a history line into the place it means
    private sealed class NameLookup
    {
        private readonly Places places;
        private readonly Dictionary<string, Guid> known = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Guid> learned = new(StringComparer.OrdinalIgnoreCase);

        public NameLookup(Places places)
        {
            this.places = places;
            foreach (var (name, id) in places.NamesEverUsed())
                known.TryAdd(name, id);
        }

        public Guid? Resolve(string? name)
        {
            if (RoomName.Clean(name) is not { } clean)
                return null;

            if (Direct(clean) is { } direct)
                return direct;

            // A room that was renamed, with the spot's own name unchanged. A spot that was removed
            // still says which room it stood in.
            var (roomName, spotName) = RoomName.Split(clean);
            if (spotName is null || Direct(roomName!) is not { } roomId)
                return null;
            return places.SpotNamed(roomId, spotName)?.Id ?? roomId;
        }

        private Guid? Direct(string clean) =>
            known.TryGetValue(clean, out var id) ? id
            : learned.TryGetValue(clean, out var alias) ? alias
            : null;

        /// <summary>Works out what old names stand for, from the stays that show two names for one place.</summary>
        public void Learn(IEnumerable<(Plant Plant, List<Stay> Stays)> plants)
        {
            var pairs = new List<(string? A, string? B, Guid? Here)>();
            foreach (var (plant, stays) in plants)
                foreach (var stay in stays)
                    pairs.Add((stay.OpenName, stay.CloseName, stay.Open ? places.Find(plant.PlaceId)?.Id : null));

            // A rename shown by one plant can be what makes the next pair readable
            var again = true;
            while (again)
            {
                again = false;
                foreach (var (a, b, here) in pairs)
                {
                    var (placeA, placeB) = (Resolve(a), Resolve(b) ?? here);
                    if (placeA is null && placeB is { } forA)
                        again |= Teach(a, forA);
                    else if (placeB is null && placeA is { } forB)
                        again |= Teach(b, forB);
                }
            }

            // Two places claiming one old name means nothing can be said about it
            var unclear = new List<string>();
            foreach (var (a, b, here) in pairs)
                if (RoomName.Clean(a) is { } name && learned.TryGetValue(name, out var taught)
                    && (Resolve(b) ?? here) is { } other && other != taught)
                    unclear.Add(name);
            foreach (var name in unclear)
                learned.Remove(name);
        }

        private bool Teach(string? name, Guid id)
        {
            if (RoomName.Clean(name) is not { } clean)
                return false;
            learned[clean] = id;
            return true;
        }
    }
}
