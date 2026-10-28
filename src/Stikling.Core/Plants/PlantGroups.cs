using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>A run of plants under one heading in the plant list.</summary>
/// <param name="Name">The genus or room. Null for the plants that have none.</param>
public sealed record PlantGroup(string? Name, IReadOnlyList<Plant> Plants)
{
    /// <summary>How many plants, counting a pot with several as several.</summary>
    public int Count => Plants.Sum(p => p.HowMany);
}

/// <summary>Cuts the plant list into headed groups, and tells which letter of the strip a name belongs to.</summary>
public static class PlantGroups
{
    /// <summary>
    /// Plants that are next to each other and share a key (ignoring case) form a group. The list is
    /// not reordered, so it has to be sorted by the key already. A null key is the group of plants
    /// with none.
    /// </summary>
    public static IReadOnlyList<PlantGroup> Split(IEnumerable<Plant> sorted, Func<Plant, string?> key)
    {
        var groups = new List<PlantGroup>();
        string? current = null;
        List<Plant>? run = null;
        foreach (var plant in sorted)
        {
            var name = key(plant);
            if (run is null || !string.Equals(name, current, StringComparison.CurrentCultureIgnoreCase))
            {
                if (run is not null)
                    groups.Add(new PlantGroup(current, run));
                current = name;
                run = [];
            }
            run.Add(plant);
        }
        if (run is not null)
            groups.Add(new PlantGroup(current, run));
        return groups;
    }

    /// <summary>
    /// The letter a name goes under on the letter strip: A to Z with accents removed, or '#' for a
    /// digit or symbol. Null for no name.
    /// </summary>
    public static char? LetterOf(string? name)
    {
        var first = name?.TrimStart().FirstOrDefault() ?? default;
        if (first == default)
            return null;

        var letter = char.ToUpperInvariant(first.ToString().Normalize(System.Text.NormalizationForm.FormD)[0]);
        return letter is >= 'A' and <= 'Z' ? letter : '#';
    }
}
