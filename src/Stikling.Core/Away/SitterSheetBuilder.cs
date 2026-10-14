using System.Globalization;
using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Away;

/// <summary>
/// Works out the plant-sitter sheet. It never says when to water, only when to check: the boxes
/// on a visit are "check it" boxes, and the steps for watering always come after the check and
/// read "If it needs water".
/// </summary>
public static class SitterSheetBuilder
{
    public const string Title = "Looking after the plants";
    public const string Elsewhere = "Elsewhere";
    public const string EveryVisit = "Check it each time you visit";
    public const string FineUntilBack = "Should be fine until you're back";

    /// <summary>
    /// The sheet for the Away stretch that is on, else the next one. With none, the sheet has no
    /// dates and every plant is checked on each visit.
    /// </summary>
    /// <param name="plants">Every plant, since the others' pace in this month is part of a guess.</param>
    public static SitterSheetView Build(
        SitterSheet sheet,
        IReadOnlyList<AwayStretch> stretches,
        IReadOnlyList<Plant> plants,
        IReadOnlyList<Propagation> propagations,
        IReadOnlyList<CareLog> logs,
        IEnumerable<TimelineEntry> timeline,
        Func<Guid, Pot?>? pot,
        IEnumerable<GrowLight> lights,
        Places places,
        Hemisphere hemisphere,
        TimeProvider time)
    {
        var today = time.Today();
        var span = AwayTime.Spans(stretches).FirstOrDefault(s => s.Kind == AwayKind.Away && s.Until > today)?.Trip;
        var visits = span is null ? [] : SitterVisits.Dates(sheet.Visits, span.From, span.Until);

        var activePlants = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
        var activeCuttings = propagations.Where(p => !p.IsDeleted && p.IsActive).ToList();
        var guesses = WateringRound.GuessesForAll(
            activePlants, logs.Where(l => !l.IsDeleted), timeline, pot, lights, places, stretches, hemisphere, time);

        var ownWords = sheet.UseOwnWords && !string.IsNullOrWhiteSpace(sheet.OwnWords) ? sheet.OwnWords.Trim() : null;

        var items = new List<Item>();
        foreach (var plant in activePlants)
        {
            items.Add(new Item(plant, true, plant.DisplayName, plant.Nickname, plant.BotanicalName, plant.PlaceId,
                plant.CoverPhotoId, WateringForms.Of(plant, pot), plant.WaterBy, guesses.GetValueOrDefault(plant.Id)));
        }
        foreach (var cutting in activeCuttings)
        {
            var form = cutting.Medium == GrowingMedium.Water ? WateringForm.InWater : WateringForm.TopWatered;
            items.Add(new Item(cutting, false, cutting.DisplayName, cutting.Nickname, cutting.BotanicalName, cutting.PlaceId,
                cutting.CoverPhotoId, form, null, null));
        }

        // The order of a walk round: by room, plants before propagations, what is in the room
        // itself before its spots, then by name. What has no place comes last.
        var ordered = items
            .Select(i => (Item: i, Room: places.RoomOf(i.PlaceId), Place: places.Find(i.PlaceId)))
            .OrderBy(x => x.Room is null)
            .ThenBy(x => x.Room?.Name ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => !x.Item.IsPlant)
            .ThenBy(x => x.Place is { IsSpot: true } && x.Place.Id != x.Room?.Id)
            .ThenBy(x => x.Place is { IsSpot: true } && x.Place.Id != x.Room?.Id ? x.Place.Name : "",
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var left = sheet.LeftOff.ToHashSet();
        var onSheet = ordered.Where(x => !left.Contains(x.Item.Entity.Id)).ToList();

        // The ways of watering the cards use, so the box can say how to do each
        var differing = onSheet
            .Select(x => x.Item)
            .Where(i => i.IsPlant && i.Form == WateringForm.TopWatered && DiffersFromUsual(i.WaterBy, sheet, ownWords))
            .Select(i => i.WaterBy!.Value)
            .Distinct()
            .Order()
            .ToList();
        var topsUp = onSheet.Any(x => x.Item.Form != WateringForm.TopWatered);

        var subjects = new List<(SitterSubject Subject, string Heading)>();
        var due = new List<string>();
        foreach (var (item, room, place) in onSheet)
        {
            var subject = Subject(item, room, place, sheet, ownWords, span?.From, visits);
            subjects.Add((subject, room?.Name ?? Elsewhere));
            if (subject.DueBeforeYouGo)
                due.Add(subject.Name);
        }

        var groups = Group(subjects);
        var byVisit = visits
            .Select(date => new SitterVisit(date, Group(subjects, s => s.Visits.Contains(date))))
            .ToList();

        var candidates = ordered
            .Select(x => new SitterCandidate(
                x.Item.Entity.Id, x.Item.IsPlant, x.Item.Name, places.NameOf(x.Item.PlaceId)?.Replace(" / ", " · "),
                !left.Contains(x.Item.Entity.Id), x.Item.IsPlant && x.Item.Form == WateringForm.TopWatered, x.Item.WaterBy))
            .ToList();

        var anyPace = onSheet.Any(x => x.Item.IsPlant && PaceOf(x.Item.Guess) is not null);
        var cuttings = onSheet.Where(x => !x.Item.IsPlant).Select(x => (Propagation)x.Item.Entity).ToList();

        return new SitterSheetView(
            sheet.Layout, Title,
            HeaderLine(span, visits.Count > 0 ? sheet.Visits : VisitPattern.None, onSheet.Count(x => x.Item.IsPlant), cuttings),
            span?.From, span?.Until,
            string.IsNullOrWhiteSpace(sheet.GeneralNote) ? null : sheet.GeneralNote.Trim(),
            BoxFor(sheet, ownWords, differing, topsUp),
            visits, groups, byVisit, candidates, due, anyPace, !sheet.HidePhotos);
    }

    private sealed record Item(
        Entity Entity, bool IsPlant, string Name, string? Nickname, string? Botanical, Guid? PlaceId,
        Guid? CoverPhotoId, WateringForm Form, WaterMethod? WaterBy, WateringGuess? Guess);

    // A way the plant says it is watered, which isn't the sheet's usual way
    private static bool DiffersFromUsual(WaterMethod? method, SitterSheet sheet, string? ownWords) =>
        method is { } m && (ownWords is not null || m != sheet.DefaultMethod);

    private static SitterSubject Subject(
        Item item, Place? room, Place? place, SitterSheet sheet, string? ownWords, DateOnly? from, IReadOnlyList<DateOnly> visits)
    {
        var spot = place is { IsSpot: true } && place.Id != room?.Id ? place.Name : null;
        var where = room is null ? null : spot is null ? room.Name : $"{room.Name} · {spot}";

        // How to water is only said when it differs from the way the sheet usually does it
        string? how = null;
        if (item.Form == WateringForm.Reservoir)
            how = "Top up the reservoir";
        else if (item.Form == WateringForm.InWater)
            how = "Top up the water";
        else if (item.IsPlant && DiffersFromUsual(item.WaterBy, sheet, ownWords))
            how = SitterSheetText.MethodName(item.WaterBy!.Value);

        string? meter = null;
        if (sheet.HasMeter && item.IsPlant && item.Form == WateringForm.TopWatered)
            meter = $"Water only at {((Plant)item.Entity).WaterAt ?? Plant.DefaultWaterAt} or below on the meter.";

        var pace = item.IsPlant ? PaceOf(item.Guess) : null;
        string paceLine;
        if (item.Form == WateringForm.InWater)
            paceLine = "Check the water every visit.";
        else if (!item.IsPlant)
            paceLine = "Check it every visit.";
        else if (pace is { } topUp && item.Form == WateringForm.Reservoir)
            paceLine = $"Usually needs topping up about every {topUp.UsualDays} days. Check the level first, and leave it if there's still water in it.";
        else if (pace is { } known)
            paceLine = $"Usually dry enough to need water about every {known.UsualDays} days. Check it first, and leave it if it's still damp.";
        else
            paceLine = "Not learned yet, so check it every visit.";

        var note = item.Entity switch
        {
            Plant p => p.SitterNote,
            Propagation p => p.SitterNote,
            _ => null
        };

        var dueBeforeYouGo = false;
        IReadOnlyList<DateOnly> own = visits;
        if (pace is { } learned && item.Form != WateringForm.InWater && from is { } start && visits.Count > 0)
        {
            var first = learned.CheckOn;
            if (first < start)
            {
                dueBeforeYouGo = true;
                first = start.AddDays(learned.UsualDays);
            }
            own = Map(visits, first, learned.UsualDays, sheet.HasMeter);
        }

        var checkNote = visits.Count == 0 ? EveryVisit : own.Count == 0 ? FineUntilBack : null;

        return new SitterSubject(
            item.Entity.Id, item.IsPlant, item.Name,
            !string.IsNullOrWhiteSpace(item.Nickname) ? item.Botanical : null,
            room?.Name, sheet.HidePlaces ? null : spot,
            sheet.HidePlaces ? null : where,
            sheet.HidePhotos ? null : item.CoverPhotoId,
            sheet.HideHowWatered ? null : how,
            meter,
            sheet.HidePace ? null : paceLine,
            sheet.HideNotes || string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            own, dueBeforeYouGo, checkNote,
            item.Entity switch
            {
                Plant p => CollectionNumbers.Normalize(p.CollectionNumber),
                Propagation p => CollectionNumbers.Normalize(p.CollectionNumber),
                _ => null
            });
    }

    private static (int UsualDays, DateOnly CheckOn)? PaceOf(WateringGuess? guess) =>
        guess is { Mode: WateringMode.Interval or WateringMode.Meter, UsualDays: { } days, CheckOn: { } on }
            ? (days, on)
            : null;

    /// <summary>
    /// The visits a plant is checked on, from the day it is first due. With a meter that is the
    /// visit nearest the day (a tie goes to the later one), and without one the first visit on
    /// or after it, never one before. The next day is that visit plus the usual days, and so on
    /// until a day falls after the last visit.
    /// </summary>
    public static IReadOnlyList<DateOnly> Map(IReadOnlyList<DateOnly> visits, DateOnly firstDue, int usualDays, bool hasMeter)
    {
        var chosen = new List<DateOnly>();
        var due = firstDue;
        var previous = DateOnly.MinValue;

        while (visits.Count > 0 && due <= visits[^1])
        {
            // Always a later visit than the last one, so a short pace on far apart visits can't stand still
            var later = visits.Where(v => v > previous).ToList();
            if (later.Count == 0)
                break;

            var visit = hasMeter
                ? later.OrderBy(v => Math.Abs(v.DayNumber - due.DayNumber)).ThenByDescending(v => v).First()
                : later.First(v => v >= due);

            chosen.Add(visit);
            previous = visit;
            due = visit.AddDays(Math.Max(1, usualDays));
        }
        return chosen;
    }

    private static List<SitterGroup> Group(
        List<(SitterSubject Subject, string Heading)> subjects, Func<SitterSubject, bool>? where = null) =>
        subjects
            .Where(x => where is null || where(x.Subject))
            .GroupBy(x => x.Heading)
            .Select(g => new SitterGroup(g.Key, [.. g.Select(x => x.Subject)]))
            .ToList();

    private static SitterBox BoxFor(SitterSheet sheet, string? ownWords, List<WaterMethod> differing, bool topsUp)
    {
        List<string> check;
        if (sheet.HasMeter)
        {
            var where = string.IsNullOrWhiteSpace(sheet.MeterWhere) ? "" : ", " + sheet.MeterWhere.Trim().TrimEnd('.');
            check =
            [
                $"There's a moisture meter{where}. Push it into the soil about halfway down and read the number. "
                    + "Water only if it reads at or below the number on the plant's card. "
                    + "If it's higher, leave it: it's fine until your next visit.",
                "No reading? Push a finger 2 to 3 cm into the soil, and water only if it's dry all the way down."
            ];
        }
        else
        {
            check =
            [
                "Push a finger 2 to 3 cm into the soil. Water only if it's dry all the way down. "
                    + "If it's damp, or you're not sure, leave it. It's safe to wait until your next visit."
            ];
        }

        var steps = new List<SitterStep>();
        if (!sheet.HideHowWatered)
        {
            steps.Add(ownWords is not null
                ? new SitterStep("Usual way", ownWords)
                : new SitterStep(SitterSheetText.MethodName(sheet.DefaultMethod), SitterSheetText.MethodSteps(sheet.DefaultMethod)));
            steps.AddRange(differing.Select(m => new SitterStep(SitterSheetText.MethodName(m), SitterSheetText.MethodSteps(m))));

            // A single way needs no label
            if (steps.Count == 1)
                steps[0] = steps[0] with { Name = null };
        }

        return new SitterBox(
            check, steps,
            !sheet.HideHowWatered && topsUp ? "Plants in water or on a reservoir only need topping up." : null);
    }

    private static string HeaderLine(AwaySpan? span, VisitPattern pattern, int plants, IReadOnlyList<Propagation> cuttings)
    {
        var parts = new List<string>();
        if (span is not null)
            parts.Add($"{SitterSheetText.Day(span.From)} to {SitterSheetText.Day(span.Until)}");

        if (plants > 0)
            parts.Add(Count(plants, "plant", "plants"));
        if (cuttings.Count > 0)
        {
            parts.Add(cuttings.All(c => c.Type == PropagationType.Cutting)
                ? Count(cuttings.Count, "cutting", "cuttings")
                : Count(cuttings.Count, "propagation", "propagations"));
        }

        var visit = pattern switch
        {
            VisitPattern.EveryDay => "a visit every day",
            VisitPattern.EveryTwoDays => "a visit every 2 days",
            VisitPattern.EveryThreeDays => "a visit every 3 days",
            VisitPattern.TwiceAWeek => "a visit twice a week",
            VisitPattern.Weekly => "a visit once a week",
            _ => null
        };
        if (visit is not null)
            parts.Add(visit);

        return string.Join(" · ", parts);
    }

    private static string Count(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : string.Create(CultureInfo.InvariantCulture, $"{count} {plural}");
}
