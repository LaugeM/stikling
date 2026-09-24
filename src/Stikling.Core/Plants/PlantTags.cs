using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>A tag in use and how many plants carry it.</summary>
public sealed record TagCount(string Name, int Plants);

/// <summary>
/// Rules for the labels you put on plants. A tag is free text, and two tags are the same when
/// they only differ in case or spacing, so "For swap" and "for  swap" never become two tags.
/// </summary>
public static class PlantTags
{
    /// <summary>Offered before you have made any of your own.</summary>
    public static readonly IReadOnlyList<string> Common = ["Variegated", "Rare", "For swap"];

    /// <summary>Tidies a typed tag, or null when nothing worth keeping was typed.</summary>
    public static string? Clean(string? tag) =>
        string.IsNullOrWhiteSpace(tag)
            ? null
            : string.Join(' ', tag.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static bool Same(string? a, string? b) =>
        Clean(a) is { } x && string.Equals(x, Clean(b), StringComparison.OrdinalIgnoreCase);

    public static bool Has(Plant plant, string? tag) => plant.Tags.Any(t => Same(t, tag));

    /// <summary>The tags tidied, without blanks or repeats. The first spelling of a repeat is kept.</summary>
    public static List<string> Normalize(IEnumerable<string?> tags) =>
        [.. tags.Select(Clean).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The plant's tags with this one added, unless it already has it.</summary>
    public static List<string> Add(IEnumerable<string> tags, string? tag) => Normalize([.. tags, tag]);

    public static List<string> Remove(IEnumerable<string> tags, string? tag) =>
        Normalize(tags.Where(t => !Same(t, tag)));

    /// <summary>
    /// Every tag on these plants, sorted by name. Plants that are gone still count, so a tag
    /// doesn't vanish from the picker because the only plant carrying it was given away.
    /// </summary>
    public static IReadOnlyList<TagCount> InUse(IEnumerable<Plant> plants)
    {
        var counts = new Dictionary<string, TagCount>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in plants.Where(p => !p.IsDeleted).SelectMany(p => Normalize(p.Tags)))
        {
            // The first spelling seen wins, the same way rooms work
            counts[tag] = counts.TryGetValue(tag, out var seen) ? seen with { Plants = seen.Plants + 1 } : new TagCount(tag, 1);
        }

        return [.. counts.Values.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>The tags in use, then the common ones not yet used, for the picker on the plant form.</summary>
    public static IReadOnlyList<string> Suggestions(IEnumerable<Plant> plants) =>
        [.. InUse(plants).Select(t => t.Name).Concat(Common).Distinct(StringComparer.OrdinalIgnoreCase)];
}
