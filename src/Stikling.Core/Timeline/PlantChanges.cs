using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>
/// Describes what changed between two versions of a plant, for the automatic history entries
/// ("Moved from Living room to Bedroom", "Now grows in LECA (was Soil)").
/// </summary>
public static class PlantChanges
{
    /// <param name="label">Turns enum values into display text, e.g. Leca → "LECA".</param>
    public static IReadOnlyList<string> Describe(Plant before, Plant after, Func<Enum, string> label)
    {
        var changes = new List<string>();

        if (before.Status != after.Status)
            changes.Add($"Status: {label(after.Status)} (was {label(before.Status)})");

        if (!SameText(before.Location, after.Location))
            changes.Add((Clean(before.Location), Clean(after.Location)) switch
            {
                (null, { } to) => $"Placed in {to}",
                ({ } from, null) => $"Removed from {from}",
                var (from, to) => $"Moved from {from} to {to}"
            });

        if (before.Medium != after.Medium)
            changes.Add($"Now grows in {label(after.Medium)} (was {label(before.Medium)})");

        if (!SameText(before.Container, after.Container) && Clean(after.Container) is { } container)
            changes.Add($"New pot: {container}");

        return changes;
    }

    private static bool SameText(string? a, string? b) =>
        string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
