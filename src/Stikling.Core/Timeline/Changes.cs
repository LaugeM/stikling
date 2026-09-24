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
    public static IReadOnlyList<string> Describe(
        Plant before,
        Plant after,
        Func<Enum, string> label,
        Func<Guid, string?>? potName = null,
        Func<Guid, string?>? mixName = null)
    {
        var changes = new List<string>();

        if (before.Status != after.Status)
            changes.Add($"Status: {label(after.Status)} (was {label(before.Status)})");

        ChangeText.AddLocation(changes, before.Location, after.Location);
        ChangeText.AddMedium(changes, before.Medium, after.Medium, label);
        ChangeText.AddContainer(changes, before.Container, after.Container, "New pot");

        var name = potName ?? (_ => null);
        ChangeText.AddPot(changes, before.InnerPotId, after.InnerPotId, name, "Potted into", "Taken out of its pot");
        ChangeText.AddPot(changes, before.OuterPotId, after.OuterPotId, name, "Now stands in", "No longer in an outer pot");
        ChangeText.AddPot(changes, before.SoilMixId, after.SoilMixId, mixName ?? (_ => null), "Now in", "No longer in a saved mix");

        if (before.WaterInOuterPot != after.WaterInOuterPot)
            changes.Add(after.WaterInOuterPot ? "Now watered in the outer pot" : "No longer watered in the outer pot");

        // Tags are only labels and stay out of the history, but quarantine is something that happened
        if (before.InQuarantine != after.InQuarantine)
            changes.Add(after.InQuarantine ? "Put in quarantine" : "Out of quarantine");

        return changes;
    }
}

/// <summary>The same kind of description for propagations ("Stage: Rooting (was Started)").</summary>
public static class PropagationChanges
{
    public static IReadOnlyList<string> Describe(Propagation before, Propagation after, Func<Enum, string> label)
    {
        var changes = new List<string>();

        if (before.Stage != after.Stage)
            changes.Add($"Stage: {label(after.Stage)} (was {label(before.Stage)})");

        if (before.InitialCount != after.InitialCount)
            changes.Add($"Count: {after.InitialCount} (was {before.InitialCount})");

        ChangeText.AddLocation(changes, before.Location, after.Location);
        ChangeText.AddMedium(changes, before.Medium, after.Medium, label);
        ChangeText.AddContainer(changes, before.Container, after.Container, "New setup");

        return changes;
    }
}

internal static class ChangeText
{
    public static void AddLocation(List<string> changes, string? before, string? after)
    {
        if (!SameText(before, after))
            changes.Add((Clean(before), Clean(after)) switch
            {
                (null, { } to) => $"Placed in {to}",
                ({ } from, null) => $"Removed from {from}",
                var (from, to) => $"Moved from {from} to {to}"
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
