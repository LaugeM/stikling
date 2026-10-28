using Stikling.Core.Models;
using Stikling.Core.Names;

namespace Stikling.Core.Wishlist;

/// <summary>
/// One search over wishes and what you already have. Every word typed has to appear somewhere in the
/// names, the everyday name or the name it goes by now, so "thai monstera" finds
/// "Monstera deliciosa 'Thai Constellation'".
/// </summary>
public static class WishSearch
{
    /// <summary>The open wishes that match. Nothing typed matches nothing, since the plant list shows them only for a search.</summary>
    public static IReadOnlyList<Wish> OpenWishes(IEnumerable<Wish> wishes, string? text, PlantDictionary? dictionary = null) =>
        Wishes(wishes.Where(w => !w.IsGot), text, dictionary);

    /// <summary>The wishes that match, got ones too.</summary>
    public static IReadOnlyList<Wish> Wishes(IEnumerable<Wish> wishes, string? text, PlantDictionary? dictionary = null) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : wishes
                .Where(w => !w.IsDeleted)
                .Where(w => Matches(text, w.Name, w.Genus, w.KnownSpecies, w.Cultivar, dictionary))
                .OrderBy(w => w.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

    /// <summary>The plants in the collection that match, for the wishlist page.</summary>
    public static IReadOnlyList<Plant> Plants(IEnumerable<Plant> plants, string? text, PlantDictionary? dictionary = null) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : plants
                .Where(p => !p.IsDeleted && p.InCollection)
                .Where(p => Matches(text, p.Nickname, p.Genus, p.KnownSpecies, p.Cultivar, dictionary))
                .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

    /// <summary>The propagations still going that match, for the wishlist page.</summary>
    public static IReadOnlyList<Propagation> Propagations(IEnumerable<Propagation> propagations, string? text, PlantDictionary? dictionary = null) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : propagations
                .Where(p => !p.IsDeleted && p.Stage is not (PropagationStage.Failed or PropagationStage.Done))
                .Where(p => Matches(text, p.Nickname, p.Genus, p.KnownSpecies, p.Cultivar, dictionary))
                .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

    private static bool Matches(string text, string? name, string? genus, string? species, string? cultivar, PlantDictionary? dictionary)
    {
        var (nowGenus, nowSpecies) = dictionary?.Current(genus, species) ?? (genus, species);
        var everyday = dictionary?.EverydayNames;
        var haystack = string.Join(' ', new[]
        {
            name, genus, species, cultivar, nowGenus, nowSpecies,
            everyday?.For(genus, species, cultivar), everyday?.For(nowGenus, nowSpecies, cultivar)
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => haystack.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }
}
