using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using static Stikling.Core.Timeline.HistoryText;

namespace Stikling.Core.Timeline;

/// <summary>
/// Describes what changed between two versions of a plant, for the automatic history entries
/// ("Moved from Living room to Bedroom", "Now grows in LECA (was Soil)").
/// </summary>
public static class PlantChanges
{
    /// <summary>The changes as lines in English, one text for each line.</summary>
    /// <param name="label">Turns enum values into display text, e.g. Leca becomes "LECA".</param>
    /// <param name="potName">Turns a pot id into its name. Pots the caller can't name are left unsaid.</param>
    /// <param name="mixName">The same for a soil mix.</param>
    /// <param name="placeName">The same for a room or spot, "Living room / Windowsill".</param>
    /// <param name="feedName">The same for a feed. A feed the caller can't name is left unsaid.</param>
    public static IReadOnlyList<string> Describe(
        Plant before,
        Plant after,
        Func<Enum, string> label,
        Func<Guid, string?>? potName = null,
        Func<Guid, string?>? mixName = null,
        Func<Guid, string?>? placeName = null,
        Func<Guid, string?>? feedName = null) =>
        [.. Lines(before, after, potName, mixName, placeName, feedName).Select(line => English(line, label))];

    /// <summary>The same changes as history lines, which can be shown in any language.</summary>
    public static IReadOnlyList<HistoryLine> Lines(
        Plant before,
        Plant after,
        Func<Guid, string?>? potName = null,
        Func<Guid, string?>? mixName = null,
        Func<Guid, string?>? placeName = null,
        Func<Guid, string?>? feedName = null)
    {
        var changes = new List<HistoryLine>();

        if (before.Status != after.Status)
            changes.Add(Line("StatusChanged", HistoryArg.Of(after.Status), HistoryArg.Of(before.Status)));

        if (after.CauseOfDeath is { } cause && before.CauseOfDeath != cause)
            changes.Add(Line("WhatHappened", HistoryArg.Of(cause)));

        if ((after.LeftTo != before.LeftTo || after.LeftFor != before.LeftFor
                || before.Status != after.Status) && after.DepartureLine() is { } departure)
            changes.Add(departure);

        ChangeText.AddLocation(changes, before.PlaceId, after.PlaceId, placeName);
        ChangeText.AddMedium(changes, before.Medium, after.Medium);

        if (before.HowMany != after.HowMany)
            changes.Add(Line("HowManyChanged", HistoryArg.Of(before.HowMany), HistoryArg.Of(after.HowMany)));

        if (before.Light != after.Light)
            changes.Add(after.Light is { } light
                ? before.Light is { } was
                    ? Line("LightChanged", HistoryArg.Of(light), HistoryArg.Of(was))
                    : Line("LightChangedFromUnset", HistoryArg.Of(light))
                : Line("LightCleared"));

        var name = potName ?? (_ => null);
        ChangeText.AddPot(changes, before.InnerPotId, after.InnerPotId, name, "PottedInto", "TakenOutOfPot");
        ChangeText.AddPot(changes, before.OuterPotId, after.OuterPotId, name, "NowStandsIn", "OuterPotRemoved");
        ChangeText.AddPot(changes, before.SoilMixId, after.SoilMixId, mixName ?? (_ => null), "NowInMix", "MixRemoved");

        ChangeText.AddWateringFeed(changes, before, after, feedName ?? (_ => null));

        if (before.PotsTaken != after.PotsTaken && after.HasLeft)
            changes.Add(PotsTakenLine(after));

        if (before.WaterInOuterPot != after.WaterInOuterPot)
            changes.Add(Line(after.WaterInOuterPot ? "WateredInOuterPot" : "NotWateredInOuterPot"));

        AddTags(changes, before.Tags, after.Tags);

        if (before.InQuarantine != after.InQuarantine)
            changes.Add(Line(after.InQuarantine ? "PutInQuarantine" : "OutOfQuarantine"));

        // A plant that died or left stops being dormant, and the status change already says so
        if (before.IsDormant != after.IsDormant && after.Status == PlantStatus.Active)
            changes.Add(DormancyLine(after.IsDormant));

        AddTransition(changes, before, after);

        return changes;
    }

    private static void AddTransition(List<HistoryLine> changes, Plant before, Plant after)
    {
        var medium = after.TransitionTo ?? before.TransitionTo;

        // Without a medium the line says semi-hydro, which is what the move is for
        HistoryLine Move(string key, string semiHydroKey) => medium is { } m
            ? Line(key, HistoryArg.Of(m))
            : Line(semiHydroKey);

        // A second move after a settled one has a new start day too
        if (after.TransitionSince is not null && before.TransitionSince != after.TransitionSince)
            changes.Add(Move("StartedMoveTo", "StartedMoveToSemiHydro"));
        if (before.RootsWashedOn is null && after.RootsWashedOn is not null)
            changes.Add(Line("RootsWashed"));
        if (before.FirstWaterRootOn is null && after.FirstWaterRootOn is not null)
            changes.Add(Line("FirstWaterRoot"));
        if (before.FirstNewLeafOn is null && after.FirstNewLeafOn is not null)
            changes.Add(Line("FirstNewLeaf"));
        if (!before.TransitionSettled && after.TransitionSettled
            && after.TransitionSince is { } since && after.TransitionSettledOn is { } settled)
        {
            var weeks = Math.Max(1, (settled.DayNumber - since.DayNumber) / 7);
            changes.Add(medium is { } m
                ? Line("SettledIn", HistoryArg.Of(m), HistoryArg.Of(weeks))
                : Line("SettledInSemiHydro", HistoryArg.Of(weeks)));
        }
        if (before.TransitionSince is not null && after.TransitionSince is null && !before.TransitionSettled)
            changes.Add(Move("StoppedTrackingMove", "StoppedTrackingMoveSemiHydro"));
    }

    /// <summary>
    /// "Tagged flowering" and "No longer tagged flowering", so a tag used for something the plant
    /// is going through still says when it started and ended after the tag is taken off.
    /// </summary>
    internal static void AddTags(List<HistoryLine> changes, IEnumerable<string> before, IEnumerable<string> after)
    {
        var was = PlantTags.Normalize(before);
        var now = PlantTags.Normalize(after);

        changes.AddRange(now.Where(tag => !was.Any(t => PlantTags.Same(t, tag))).Select(tag => Line("Tagged", HistoryArg.Of(tag))));
        changes.AddRange(was.Where(tag => !now.Any(t => PlantTags.Same(t, tag))).Select(tag => Line("Untagged", HistoryArg.Of(tag))));
    }

    internal static HistoryLine DormancyLine(bool dormant) => Line(dormant ? "WentDormant" : "WokeUp");

    internal static string DormancyText(bool dormant) => dormant ? "Went dormant" : "Woke up from dormancy";

    private static HistoryLine PotsTakenLine(Plant plant) => Line(plant.PotsTaken switch
    {
        PotsTaken.Inner => "PotWentWithIt",
        PotsTaken.All when plant.InnerPotId is null => "OuterPotWentWithIt",
        PotsTaken.All => "PotsWentWithIt",
        _ => "PotsStayed"
    });
}

/// <summary>The same kind of description for propagations ("Stage: Rooting (was Started)").</summary>
public static class PropagationChanges
{
    public static IReadOnlyList<string> Describe(
        Propagation before,
        Propagation after,
        Func<Enum, string> label,
        Func<Guid, string?>? placeName = null) =>
        [.. Lines(before, after, placeName).Select(line => English(line, label))];

    public static IReadOnlyList<HistoryLine> Lines(
        Propagation before,
        Propagation after,
        Func<Guid, string?>? placeName = null)
    {
        var changes = new List<HistoryLine>();

        if (before.Stage != after.Stage)
            changes.Add(Line("StageChanged", HistoryArg.Of(after.Stage), HistoryArg.Of(before.Stage)));

        if (before.InitialCount != after.InitialCount)
            changes.Add(Line("CountChanged", HistoryArg.Of(after.InitialCount), HistoryArg.Of(before.InitialCount)));

        // Only a correction made when editing changes these without a pot up or failure of its own
        if (before.PottedUpCount != after.PottedUpCount)
            changes.Add(Line("PottedUpChanged", HistoryArg.Of(after.PottedUpCount), HistoryArg.Of(before.PottedUpCount)));

        if (before.GivenAwayCount != after.GivenAwayCount)
            changes.Add(Line("GivenAwayChanged", HistoryArg.Of(after.GivenAwayCount), HistoryArg.Of(before.GivenAwayCount)));

        if (before.FailedCount != after.FailedCount)
            changes.Add(Line("FailedChanged", HistoryArg.Of(after.FailedCount), HistoryArg.Of(before.FailedCount)));

        ChangeText.AddLocation(changes, before.PlaceId, after.PlaceId, placeName);
        ChangeText.AddMedium(changes, before.Medium, after.Medium);
        ChangeText.AddContainer(changes, before.Container, after.Container, "NewSetup");
        AddMilestone(changes, before.FirstRootOn, after.FirstRootOn, after.DaysToFirstRoot, "FirstRootOnDay", "FirstRootCleared");
        AddMilestone(changes, before.FirstLeafOn, after.FirstLeafOn, after.DaysToFirstLeaf, "FirstLeafOnDay", "FirstLeafCleared");

        if (before.SeedsGerminated != after.SeedsGerminated)
            changes.Add(after.SeedsGerminated is { } up
                ? Line("SeedsCameUp", HistoryArg.Of(up), HistoryArg.Of(after.InitialCount))
                : Line("SeedCountCleared"));

        if (before.CormSizeMm != after.CormSizeMm)
            changes.Add(after.CormSizeMm is { } size ? Line("CormSize", HistoryArg.Of(size)) : Line("CormSizeCleared"));

        if (!before.RootingAids.Order().SequenceEqual(after.RootingAids.Order()))
            changes.Add(after.RootingAids.Count == 0
                ? Line("NoRootingAids")
                : Line("RootingAids", HistoryArg.OfAll(after.RootingAids.Order())));

        if (before.DriedForHours != after.DriedForHours)
        {
            if (after.DriedForHours is { } dried)
            {
                var (amount, inDays) = Drying.Split(dried);
                changes.Add(Line(inDays ? "CutEndDriedDays" : "CutEndDriedHours", HistoryArg.Of(amount)));
            }
            else
            {
                changes.Add(Line("CutEndNotDried"));
            }
        }

        // Finishing ends dormancy too, and the stage change already says so
        if (before.IsDormant != after.IsDormant && after.IsActive)
            changes.Add(PlantChanges.DormancyLine(after.IsDormant));

        return changes;
    }

    // "First root on day 12"
    private static void AddMilestone(List<HistoryLine> changes, DateOnly? before, DateOnly? after, int? days, string onDay, string cleared)
    {
        if (before == after)
            return;
        changes.Add(days is { } d ? Line(onDay, HistoryArg.Of(d)) : Line(cleared));
    }
}

internal static class ChangeText
{
    /// <param name="name">Turns a place id into "Living room / Windowsill". A move to a place
    /// it can't name is left unsaid.</param>
    public static void AddLocation(List<HistoryLine> changes, Guid? before, Guid? after, Func<Guid, string?>? name)
    {
        if (before == after)
            return;

        var from = before is { } b ? name?.Invoke(b) : null;
        var to = after is { } a ? name?.Invoke(a) : null;

        // Two ids with one name are a place and the one it was merged into, which is no move
        if (after is not null && (to is null || SameText(from, to)))
            return;

        changes.Add((from, to) switch
        {
            (_, null) => from is null ? Line("RemovedFromRoom") : Line("RemovedFrom", HistoryArg.Of(from)),
            (null, _) when before is null => Line("PlacedIn", HistoryArg.Of(to!)),
            (null, _) => Line("MovedTo", HistoryArg.Of(to!)),
            _ => Line("MovedFromTo", HistoryArg.Of(from!), HistoryArg.Of(to!))
        });
    }

    public static void AddMedium(List<HistoryLine> changes, GrowingMedium before, GrowingMedium after)
    {
        if (before != after)
            changes.Add(Line("MediumChanged", HistoryArg.Of(after), HistoryArg.Of(before)));
    }

    public static void AddContainer(List<HistoryLine> changes, string? before, string? after, string key)
    {
        if (!SameText(before, after) && Clean(after) is { } container)
            changes.Add(Line(key, HistoryArg.Of(container)));
    }

    public static void AddPot(
        List<HistoryLine> changes,
        Guid? before,
        Guid? after,
        Func<Guid, string?> name,
        string into,
        string outOf)
    {
        if (before == after)
            return;

        if (after is null)
            changes.Add(Line(outOf));
        else if (name(after.Value) is { } pot)
            changes.Add(Line(into, HistoryArg.Of(pot)));
    }

    /// <summary>"Fed with the watering: Aroid feed, every second watering", or that it stopped.</summary>
    public static void AddWateringFeed(List<HistoryLine> changes, Plant before, Plant after, Func<Guid, string?> feedName)
    {
        int? every = after.WateringFeedId is null ? null : Math.Clamp(after.WateringFeedEvery ?? 1, 1, 3);
        int? wasEvery = before.WateringFeedId is null ? null : Math.Clamp(before.WateringFeedEvery ?? 1, 1, 3);
        if (before.WateringFeedId == after.WateringFeedId && wasEvery == every)
            return;

        if (after.WateringFeedId is not { } feed)
            changes.Add(Line("FedStopped"));
        else if (feedName(feed) is { } name)
            changes.Add(Line(every switch
            {
                2 => "FedEverySecond",
                3 => "FedEveryThird",
                _ => "FedEveryWatering"
            }, HistoryArg.Of(name)));
    }

    private static bool SameText(string? a, string? b) =>
        string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
