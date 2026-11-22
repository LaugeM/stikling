using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>
/// The marks on a plant that tell a helper it needs a hand, like "High up", "Hanging" or "Heavy". They are
/// short texts like tags and follow the same rules (<see cref="PlantTags"/>): two marks are the same when
/// they only differ in case or spacing.
/// </summary>
public static class NeedsAHand
{
    /// <summary>The marks tidied, without blanks or repeats.</summary>
    public static List<string> Normalize(IEnumerable<string?> marks) => PlantTags.Normalize(marks);

    /// <summary>
    /// The marks to offer on the plant form: the ones already in use in the collection, then the common
    /// ones not yet used. The common ones are text people see, so the caller passes them in.
    /// </summary>
    public static IReadOnlyList<string> Suggestions(IEnumerable<Plant> plants, IEnumerable<string> common) =>
        [.. Normalize(plants.Where(p => !p.IsDeleted).SelectMany(p => p.NeedsAHand))
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .Concat(common)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The plant's marks as one line in lower case, "high up, heavy", or null when it has none.</summary>
    public static string? Line(Plant plant)
    {
        var marks = Normalize(plant.NeedsAHand);
        return marks.Count == 0
            ? null
            : string.Join(", ", marks.Select(m => m.ToLower(CultureInfo.CurrentCulture)));
    }
}
