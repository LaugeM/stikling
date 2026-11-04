using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Search;

namespace Stikling.Core.Timeline;

/// <summary>
/// The plants and propagations that waiting photos can be sent to on the Add photos page: plants in
/// the collection and propagations still going, in one list sorted by the name each is listed by.
/// </summary>
public static class PhotoDestinations
{
    /// <summary>
    /// A <see cref="Plant"/> or <see cref="Propagation"/> for each destination. Every word of
    /// <paramref name="search"/> has to appear in one of its names or its number, ignoring capitals and accents.
    /// </summary>
    public static IReadOnlyList<Entity> Pick(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, string? search)
    {
        var words = SearchText.Words(search);

        var found = new List<(Entity Subject, string Name)>();
        foreach (var plant in plants.Where(p => !p.IsDeleted && p.InCollection))
        {
            if (Matches(words, plant.CollectionNumber, plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, plant.NameNote, plant.BotanicalName))
                found.Add((plant, plant.DisplayName));
        }
        foreach (var propagation in propagations.Where(p => !p.IsDeleted && p.IsActive))
        {
            if (Matches(words, propagation.CollectionNumber, propagation.Nickname, propagation.Genus, propagation.Species, propagation.Cultivar, propagation.NameNote, propagation.BotanicalName))
                found.Add((propagation, propagation.ListName));
        }

        return [.. found
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(f => f.Subject.Id)
            .Select(f => f.Subject)];
    }

    private static bool Matches(IReadOnlyList<string> words, string? number, params string?[] names)
    {
        if (words.Count == 0)
            return true;

        var haystack = SearchText.Fold(string.Join(' ', names.Where(n => !string.IsNullOrWhiteSpace(n))));
        return words.All(word => haystack.Contains(word, StringComparison.Ordinal)
            || CollectionNumbers.MatchesSearch(number, word));
    }
}
