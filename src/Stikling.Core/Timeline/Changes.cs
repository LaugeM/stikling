using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>
/// Describes what changed between two versions of a plant, for the automatic history entries
/// ("Moved from Living room to Bedroom", "Now grows in LECA (was Soil)").
/// </summary>
public static class PlantChanges
{
    /// <param name="label">Turns enum values into display text, e.g. Leca becomes "LECA".</param>
    /// <param name="potName">Turns a pot id into its name. Pots the caller can't name are left unsaid.</param>
    /// <param name="mixName">The same for a soil mix.</param>
    /// <param name="placeName">The same for a room or spot, "Living room / Windowsill".</param>
    public static IReadOnlyList<string> Describe(
        Plant before,
        Plant after,
        Func<Enum, string> label,
        Func<Guid, string?>? potName = null,
        Func<Guid, string?>? mixName = null,
        Func<Guid, string?>? placeName = null)
    {
        var changes = new List<string>();

        if (before.Status != after.Status)
            changes.Add($"Status: {label(after.Status)} (was {label(before.Status)})");

        ChangeText.AddLocation(changes, before.PlaceId, after.PlaceId, placeName);
        ChangeText.AddMedium(changes, before.Medium, after.Medium, label);

        if (before.Light != after.Light)
            changes.Add(after.Light is { } light
                ? $"Light: {label(light)} (was {(before.Light is { } was ? label(was) : "not set")})"
                : "Light setting cleared");

        var name = potName ?? (_ => null);
        ChangeText.AddPot(changes, before.InnerPotId, after.InnerPotId, name, "Potted into", "Taken out of its pot");
        ChangeText.AddPot(changes, before.OuterPotId, after.OuterPotId, name, "Now stands in", "No longer in an outer pot");
        ChangeText.AddPot(changes, before.SoilMixId, after.SoilMixId, mixName ?? (_ => null), "Now in", "No longer in a saved mix");

        if (before.PotsTaken != after.PotsTaken && after.HasLeft)
            changes.Add(PotsTakenText(after));

        if (before.WaterInOuterPot != after.WaterInOuterPot)
            changes.Add(after.WaterInOuterPot ? "Now watered in the outer pot" : "No longer watered in the outer pot");

        // Tags are only labels and stay out of the history, but quarantine is something that happened
        if (before.InQuarantine != after.InQuarantine)
            changes.Add(after.InQuarantine ? "Put in quarantine" : "Out of quarantine");

        // A plant that died or left stops being dormant, and the status change already says so
        if (before.IsDormant != after.IsDormant && after.Status == PlantStatus.Active)
            changes.Add(DormancyText(after.IsDormant));

        return changes;
    }

    internal static string DormancyText(bool dormant) => dormant ? "Went dormant" : "Woke up from dormancy";

    private static string PotsTakenText(Plant plant) => plant.PotsTaken switch
    {
        PotsTaken.Inner => "Its pot went with it",
        PotsTaken.All when plant.InnerPotId is null => "Its outer pot went with it",
        PotsTaken.All => "Its pot and outer pot went with it",
        _ => "Its pots stayed here"
    };
}

/// <summary>The same kind of description for propagations ("Stage: Rooting (was Started)").</summary>
public static class PropagationChanges
{
    public static IReadOnlyList<string> Describe(
        Propagation before,
        Propagation after,
        Func<Enum, string> label,
        Func<Guid, string?>? placeName = null)
    {
        var changes = new List<string>();

        if (before.Stage != after.Stage)
            changes.Add($"Stage: {label(after.Stage)} (was {label(before.Stage)})");

        if (before.InitialCount != after.InitialCount)
            changes.Add($"Count: {after.InitialCount} (was {before.InitialCount})");

        ChangeText.AddLocation(changes, before.PlaceId, after.PlaceId, placeName);
        ChangeText.AddMedium(changes, before.Medium, after.Medium, label);
        ChangeText.AddContainer(changes, before.Container, after.Container, "New setup");
        AddMilestone(changes, before.FirstRootOn, after.FirstRootOn, after.DaysToFirstRoot, "First root");
        AddMilestone(changes, before.FirstLeafOn, after.FirstLeafOn, after.DaysToFirstLeaf, "First leaf");

        if (before.SeedsGerminated != after.SeedsGerminated)
            changes.Add(after.SeedsGerminated is { } up
                ? $"Seeds: {up} of {after.InitialCount} came up"
                : "Seed count cleared");

        if (!before.RootingAids.Order().SequenceEqual(after.RootingAids.Order()))
            changes.Add(after.RootingAids.Count == 0
                ? "No rooting aids"
                : $"Rooting aids: {string.Join(", ", after.RootingAids.Order().Select(aid => label(aid)))}");

        // Finishing ends dormancy too, and the stage change already says so
        if (before.IsDormant != after.IsDormant && after.IsActive)
            changes.Add(PlantChanges.DormancyText(after.IsDormant));

        return changes;
    }

    // "First root on day 12"
    private static void AddMilestone(List<string> changes, DateOnly? before, DateOnly? after, int? days, string name)
    {
        if (before == after)
            return;
        changes.Add(days is { } d ? $"{name} on day {d}" : $"{name} date cleared");
    }
}

internal static class ChangeText
{
    /// <param name="name">Turns a place id into "Living room / Windowsill". A move to a place
    /// it can't name is left unsaid.</param>
    public static void AddLocation(List<string> changes, Guid? before, Guid? after, Func<Guid, string?>? name)
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
            (_, null) => $"Removed from {from ?? "its room"}",
            (null, _) when before is null => $"Placed in {to}",
            (null, _) => $"Moved to {to}",
            _ => $"Moved from {from} to {to}"
        });
    }

    public static void AddMedium(List<string> changes, GrowingMedium before, GrowingMedium after, Func<Enum, string> label)
    {
        if (before != after)
            changes.Add($"Now grows in {label(after)} (was {label(before)})");
    }

    public static void AddContainer(List<string> changes, string? before, string? after, string prefix)
    {
        if (!SameText(before, after) && Clean(after) is { } container)
            changes.Add($"{prefix}: {container}");
    }

    public static void AddPot(
        List<string> changes,
        Guid? before,
        Guid? after,
        Func<Guid, string?> name,
        string into,
        string outOf)
    {
        if (before == after)
            return;

        if (after is null)
            changes.Add(outOf);
        else if (name(after.Value) is { } pot)
            changes.Add($"{into} {pot}");
    }

    private static bool SameText(string? a, string? b) =>
        string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
