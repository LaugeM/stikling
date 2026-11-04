using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Names;

/// <summary>What the person has of a genus, species or cultivar.</summary>
public sealed record Yours(IReadOnlyList<Plant> Plants, IReadOnlyList<Propagation> Propagations, IReadOnlyList<Wish> Wishes);

/// <summary>How many plants and propagations still going the person has, per genus and per species.</summary>
public sealed class OwnedCounts
{
    private readonly Dictionary<string, int> genera = [];
    private readonly Dictionary<(string, string), int> species = [];

    internal void Add(string genus, string speciesName)
    {
        var g = NameKey.Of(genus);
        genera[g] = genera.GetValueOrDefault(g) + 1;
        var s = NameKey.Of(speciesName);
        if (s.Length == 0)
            return;
        species[(g, s)] = species.GetValueOrDefault((g, s)) + 1;
        var basic = DictionaryService.BaseSpecies(s);
        if (basic != s)
            species[(g, basic)] = species.GetValueOrDefault((g, basic)) + 1;
    }

    public int Genus(string genus) => genera.GetValueOrDefault(NameKey.Of(genus));

    public int Species(string genus, string speciesName) => species.GetValueOrDefault((NameKey.Of(genus), NameKey.Of(speciesName)));
}

/// <summary>Browsing the built-in dictionary, and what the person has of what they find there.</summary>
public sealed class DictionaryService(
    PlantNameService names,
    IPlantRepository plants,
    IPropagationRepository propagations,
    IWishRepository wishes)
{
    private DictionaryBrowser? browser;
    private string? language;

    public async Task<DictionaryBrowser> GetBrowserAsync()
    {
        var data = await names.GetDataAsync();
        var book = await names.GetEverydayNamesAsync();
        // Empty data, as when the file couldn't be read, is built again next time
        if (browser is null || ReferenceEquals(browser, DictionaryBrowser.Empty) || language != book.Language)
        {
            browser = data.Genera.Count == 0 ? DictionaryBrowser.Empty : new DictionaryBrowser(data, book);
            language = book.Language;
        }
        return browser;
    }

    public async Task<Yours> GetYoursAsync(string genus, string? species = null, string? cultivar = null)
    {
        var dictionary = await names.GetDictionaryAsync();
        var g = NameKey.Of(genus);
        var s = NameKey.Of(species);
        var c = NameKey.Of(cultivar);

        bool Matches(string? recordGenus, string? recordSpecies, string? recordCultivar)
        {
            if (string.IsNullOrWhiteSpace(recordGenus))
                return false;
            var (now, nowSpecies) = dictionary.Current(recordGenus, recordSpecies);
            if (NameKey.Of(now) != g)
                return false;
            if (s.Length > 0 && !SpeciesCovers(s, NameKey.Of(nowSpecies)))
                return false;
            return c.Length == 0 || NameKey.Of(recordCultivar) == c;
        }

        var mine = (await plants.GetAllAsync())
            .Where(p => p.Status == PlantStatus.Active && Matches(p.Genus, p.KnownSpecies, p.Cultivar))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        // Still going, as in the Active progress filter of the Propagations list
        var going = (await propagations.GetAllAsync())
            .Where(p => p.IsActive && Matches(p.Genus, p.KnownSpecies, p.Cultivar))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        // Still wished for, as the open list on the Wishlist page
        var wished = (await wishes.GetAllAsync())
            .Where(w => !w.IsGot && Matches(w.Genus, w.KnownSpecies, w.Cultivar))
            .OrderBy(w => w.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new Yours(mine, going, wished);
    }

    public async Task<OwnedCounts> GetOwnedCountsAsync()
    {
        var dictionary = await names.GetDictionaryAsync();
        var counts = new OwnedCounts();

        void Count(string? genus, string? species)
        {
            if (string.IsNullOrWhiteSpace(genus))
                return;
            var (now, nowSpecies) = dictionary.Current(genus, species);
            if (!string.IsNullOrWhiteSpace(now))
                counts.Add(now, nowSpecies ?? "");
        }

        foreach (var plant in (await plants.GetAllAsync()).Where(p => p.Status == PlantStatus.Active))
            Count(plant.Genus, plant.KnownSpecies);
        foreach (var propagation in (await propagations.GetAllAsync()).Where(p => p.IsActive))
            Count(propagation.Genus, propagation.KnownSpecies);
        return counts;
    }

    // A species also covers its varieties and subspecies: "hederaceum" covers "hederaceum var. oxycardium"
    private static bool SpeciesCovers(string asked, string recordSpecies) =>
        recordSpecies == asked || BaseSpecies(recordSpecies) == asked;

    internal static string BaseSpecies(string key)
    {
        foreach (var rank in new[] { " var. ", " subsp. ", " ssp. ", " f. " })
        {
            var at = key.IndexOf(rank, StringComparison.Ordinal);
            if (at > 0)
                return key[..at];
        }
        return key;
    }
}
