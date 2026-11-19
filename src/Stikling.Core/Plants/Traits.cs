using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>
/// Short texts on plants and propagations for what they show, like "dark new leaves" or "silver
/// splash". They work like tags: two are the same when they only differ in case or spacing, and the
/// suggestions are the ones already used in the collection.
/// </summary>
public static class Traits
{
    public static List<string> Normalize(IEnumerable<string?> traits) => PlantTags.Normalize(traits);

    /// <summary>The traits already used on plants and propagations, by name. Nothing is suggested before any exists.</summary>
    public static IReadOnlyList<string> Suggestions(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations) =>
        [.. plants.Where(p => !p.IsDeleted).SelectMany(p => Normalize(p.Traits))
            .Concat(propagations.Where(p => !p.IsDeleted).SelectMany(p => Normalize(p.Traits)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)];
}
