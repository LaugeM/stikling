using Stikling.Core.Models;
using Stikling.Core.Names;

namespace Stikling.Core.Wishlist;

/// <summary>What you already have of a wish: how many of the same, or another of the same species.</summary>
/// <param name="Same">Plants in the collection with the same genus, species and cultivar.</param>
/// <param name="AnotherOf">The species of a plant you have that is another cultivar of it, like
/// "Monstera deliciosa". Only set when there is no same one.</param>
public sealed record WishHave(int Same, string? AnotherOf)
{
    /// <summary>"You have 2" or "You have another Monstera deliciosa". Null when you have nothing like it.</summary>
    public string? Describe() =>
        Same > 0 ? $"You have {Same}"
        : AnotherOf is not null ? $"You have another {AnotherOf}"
        : null;
}

/// <summary>
/// Compares wishes with plants and propagations. Names are compared without case, and an old name
/// from the dictionary counts as the one it goes by now. A wish with no genus is compared on its
/// own name and a plant's nickname, since a lone species or cultivar says too little.
/// </summary>
public static class WishMatches
{
    /// <summary>What the plants in the collection say about a wish. Plants that died or left don't count.</summary>
    public static WishHave Have(Wish wish, IEnumerable<Plant> plants, PlantDictionary? dictionary = null)
    {
        var mine = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active).ToList();
        var same = mine.Count(p => SameName(wish, p.Genus, p.KnownSpecies, p.Cultivar, p.Nickname, dictionary));
        if (same > 0)
            return new WishHave(same, null);

        if (Key(wish.Genus, wish.KnownSpecies, null, dictionary) is { Species.Length: > 0 } key
            && mine.Any(p => Key(p.Genus, p.KnownSpecies, null, dictionary) == key))
            return new WishHave(0, PlantNames.Botanical(wish.Genus, wish.KnownSpecies, null));
        return new WishHave(0, null);
    }

    /// <summary>The open wishes that name the same plant as this one, e.g. the names typed on the add form.</summary>
    public static IReadOnlyList<Wish> OpenFor(string? genus, string? species, string? cultivar, string? nickname,
        IEnumerable<Wish> wishes, PlantDictionary? dictionary = null) =>
        wishes
            .Where(w => !w.IsDeleted && !w.IsGot)
            .Where(w => SameName(w, genus, species, cultivar, nickname, dictionary))
            .OrderBy(w => w.CreatedAt)
            .ToList();

    /// <summary>True when a plant, propagation or add form with these names is the same plant as the wish.</summary>
    public static bool SameName(Wish wish, string? genus, string? species, string? cultivar, string? nickname, PlantDictionary? dictionary)
    {
        var wanted = Key(wish.Genus, wish.KnownSpecies, wish.Cultivar, dictionary);
        if (wanted.Genus.Length > 0)
            return wanted == Key(genus, species, cultivar, dictionary);

        return Clean(wish.Name) is { } name && name == Clean(nickname);
    }

    private static (string Genus, string Species, string Cultivar) Key(string? genus, string? species, string? cultivar, PlantDictionary? dictionary)
    {
        var (g, s) = dictionary?.Current(genus, species) ?? (genus, species);
        return (Clean(g) ?? "", Clean(s) ?? "", Clean(PlantNames.Cultivar(cultivar)) ?? "");
    }

    private static string? Clean(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLowerInvariant();
}
