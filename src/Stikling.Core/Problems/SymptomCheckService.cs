using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Problems;

/// <summary>
/// Gathers what the app knows about a plant for the symptom check, and the lines that show it.
/// It only reads. Nothing about the check is saved.
/// </summary>
public sealed class SymptomCheckService(
    IPlantRepository plants, ICareLogRepository care, ITimelineRepository timeline, IPotRepository pots,
    IGrowLightRepository lights, IPlaceRepository places, IAwayStretchRepository away,
    IPestCaseRepository pestCases, TimeProvider time)
{
    public async Task<SymptomContext> ForAsync(Plant plant)
    {
        var today = time.Today();
        var hemisphere = Seasons.HemisphereOf(time.LocalTimeZone);
        var allPlants = await plants.GetAllAsync();
        var allCare = await care.GetAllAsync();
        var changes = await timeline.GetPlantChangesAsync();
        var potList = await pots.GetAllAsync();
        var placeList = await places.GetPlacesAsync();

        var own = allCare.Where(l => !l.IsDeleted && l.PlantId == plant.Id).ToList();
        Pot? Pot(Guid id) => potList.FirstOrDefault(p => p.Id == id);

        // The guess is worked out the way Today does it, so the plant's page and this agree
        var guess = WateringRound.GuessFor(
            plant, allPlants, allCare, changes, Pot, await lights.GetAllAsync(), placeList,
            await away.GetAllAsync(), hemisphere, time);

        var recent = RecentChanges.Of(plant, own, changes, today, SymptomScoring.RecentChangeDays, time.LocalDay);
        var meter = FreshMeter(plant, own, guess.LastWatered, today);
        var feedings = own.Where(l => l.Kind == CareKind.Fertilised).Select(l => l.OccurredOn).Distinct().ToList();
        var feedingDays = feedings.Where(d => Age(d, today) is >= 0 and < SymptomScoring.FeedingWindowDays).ToList();

        var resting = plant.IsDormant
            || RestSeason.WindowStart(plant, DormantPeriods.Of(plant, changes, time), hemisphere, today) is not null;
        var darkerHalf = !Seasons.InBrighterHalf(today, hemisphere);

        var facts = new SymptomFacts
        {
            Changes = recent,
            Light = CareLikes.Fit(plant) is { } fit and not LightFit.Fits ? fit : null,
            LikesDryOut = plant.LikesWater == WaterNeed.DryOut,
            LikesHighHumidity = plant.LikesHumidity == HumidityNeed.High,
            Meter = meter,
            DryAt = plant.WaterAt ?? SymptomScoring.MeterDryTo,
            WateredRecently = guess.DaysSince is { } since && since <= SymptomScoring.WateredRecentlyDays,
            LongerThanUsual = guess.Mode != WateringMode.Learning
                && guess.UsualDays is { } usual && guess.DaysSince is { } days && days > usual,
            FedLastMonth = feedingDays.Any(d => Age(d, today) <= SymptomScoring.FedLastMonthDays),
            // Not fed says nothing while it rests, so it isn't offered then
            FeedingsLast3Months = feedingDays.Count == 0 && resting ? null : feedingDays.Count,
            NotRepottedInTwoYears = NotRepottedLongAgo(plant, own, today),
            OpenPestCase = PestService.CasesFor(plant, await pestCases.GetAllAsync(), placeList).Count > 0,
            Resting = resting,
            WaterCulture = guess.Form != WateringForm.TopWatered,
            DarkerHalf = darkerHalf,
            HeatingHalf = darkerHalf
        };

        SoilNow? soil = null;
        DateOnly? meterDay = null;
        if (meter is { } reading && reading.DaysAgo <= SymptomScoring.MeterFillsSoilDays)
        {
            soil = SymptomCheck.SoilFromMeter(reading.Value, facts.DryAt);
            meterDay = reading.On;
        }

        return new SymptomContext(facts, Lines(plant, facts, guess, today), soil, meterDay);
    }

    private static int Age(DateOnly day, DateOnly today) => today.DayNumber - day.DayNumber;

    // The newest reading, when it is recent and no watering came after it, since it then says nothing about the soil now
    private static MoistureReading? FreshMeter(Plant plant, List<CareLog> own, DateOnly? lastWatered, DateOnly today)
    {
        var newest = own
            .Where(l => l.Kind == CareKind.MoistureReading && l.Moisture is not null && l.OccurredOn <= today
                && (plant.WateringSince is not { } since || l.OccurredOn >= since))
            .OrderByDescending(l => l.OccurredOn).ThenByDescending(l => l.CreatedAt)
            .FirstOrDefault();
        if (newest is null)
            return null;

        var daysAgo = Age(newest.OccurredOn, today);
        if (daysAgo > SymptomScoring.MeterFreshDays || (lastWatered is { } watered && newest.OccurredOn <= watered))
            return null;
        return new MoistureReading(newest.Moisture!.Value, newest.OccurredOn, daysAgo);
    }

    // The last repot from the care log, or the day it was got when it was never repotted here. Never knowing either counts as long ago.
    private static bool NotRepottedLongAgo(Plant plant, List<CareLog> own, DateOnly today)
    {
        DateOnly? last = own.Where(l => l.Kind == CareKind.Repotted).Select(l => (DateOnly?)l.OccurredOn).Max()
            ?? plant.AcquiredOn?.Start;
        return last is not { } day || day.AddYears(SymptomScoring.NotRepottedYears) < today;
    }

    private static List<SymptomFactLine> Lines(Plant plant, SymptomFacts f, WateringGuess guess, DateOnly today)
    {
        var lines = new List<SymptomFactLine>();
        void Add(SymptomFactKind kind, string text) => lines.Add(new SymptomFactLine(kind, text));

        foreach (var change in f.Changes)
        {
            var days = Age(change.Day, today);
            switch (change.Kind)
            {
                case RecentChangeKind.Arrived: Add(SymptomFactKind.Arrived, SymptomText.Arrived(days)); break;
                case RecentChangeKind.Repotted: Add(SymptomFactKind.Repotted, SymptomText.Repotted(days)); break;
                default: Add(SymptomFactKind.Moved, SymptomText.Moved(days)); break;
            }
        }

        foreach (var kind in new[] { SymptomFactKind.DarkerThanItLikes, SymptomFactKind.BrighterThanItLikes })
        {
            if (f.Has(kind))
                Add(kind, SymptomText.Plain(kind));
        }

        if (f.Meter is { } meter)
            Add(SymptomFactKind.MeterReading, SymptomText.Meter(meter.Value, meter.DaysAgo));
        if (f.WateredRecently && guess.DaysSince is { } since)
            Add(SymptomFactKind.WateredRecently, SymptomText.Watered(since));
        if (f.LongerThanUsual && guess.DaysSince is { } sinceWater && guess.UsualDays is { } usual)
            Add(SymptomFactKind.LongerThanUsual, SymptomText.LongerThanUsual(sinceWater, usual));

        foreach (var kind in new[] { SymptomFactKind.LikesDryOut, SymptomFactKind.LikesHumidity, SymptomFactKind.FedLastMonth })
        {
            if (f.Has(kind))
                Add(kind, SymptomText.Plain(kind));
        }

        if (f.Has(SymptomFactKind.FedOften))
            Add(SymptomFactKind.FedOften, SymptomText.FedOften(f.FeedingsLast3Months!.Value));
        if (f.Has(SymptomFactKind.NotFed))
            Add(SymptomFactKind.NotFed, SymptomText.Plain(SymptomFactKind.NotFed));
        if (f.NotRepottedInTwoYears)
            Add(SymptomFactKind.NotRepotted, SymptomText.Plain(SymptomFactKind.NotRepotted));
        if (f.OpenPestCase)
            Add(SymptomFactKind.PestCase, SymptomText.Plain(SymptomFactKind.PestCase));
        if (f.Resting)
            Add(SymptomFactKind.Resting, plant.IsDormant ? SymptomText.Dormant : SymptomText.Plain(SymptomFactKind.Resting));

        foreach (var kind in new[] { SymptomFactKind.WaterCulture, SymptomFactKind.DarkerHalf, SymptomFactKind.HeatingHalf })
        {
            if (f.Has(kind))
                Add(kind, SymptomText.Plain(kind));
        }

        return lines;
    }
}
