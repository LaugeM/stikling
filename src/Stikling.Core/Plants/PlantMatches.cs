using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>Finds the plants in the collection that look like the one being added,
/// so a second one is only added on purpose.</summary>
public static class PlantMatches
{
    /// <summary>Plants still in the collection with the same nickname, or the same genus, species
    /// and cultivar. Case and spaces at the ends don't count. A name with no genus only matches
    /// on the nickname, since a lone species or cultivar says too little.</summary>
    public static IReadOnlyList<Plant> SameName(Plant draft, IEnumerable<Plant> plants) =>
        plants
            .Where(p => p.Id != draft.Id && !p.IsDeleted && p.Status == PlantStatus.Active)
            .Where(p => SameNickname(p, draft) || SameBotanicalName(p, draft))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Plants that died with the same name as the draft, by the same rules as <see cref="SameName"/>,
    /// so someone can look at what went wrong before buying the same plant again.</summary>
    public static IReadOnlyList<Plant> DiedBefore(Plant draft, IEnumerable<Plant> plants) =>
        plants
            .Where(p => p.Id != draft.Id && !p.IsDeleted && p.Status == PlantStatus.Died)
            .Where(p => SameNickname(p, draft) || SameBotanicalName(p, draft))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static bool SameNickname(Plant a, Plant b) =>
        Clean(b.Nickname) is { } nickname && nickname == Clean(a.Nickname);

    private static bool SameBotanicalName(Plant a, Plant b) =>
        Clean(b.Genus) is { } genus && genus == Clean(a.Genus)
        && Clean(a.Species) == Clean(b.Species)
        && Clean(a.Cultivar) == Clean(b.Cultivar);

    private static string? Clean(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLowerInvariant();
}
