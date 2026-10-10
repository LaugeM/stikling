using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Plants;

/// <summary>Parent/offspring lookups between plants.</summary>
public static class PlantLineage
{
    /// <summary>Plants whose parent is the given plant, sorted by name.</summary>
    public static IReadOnlyList<Plant> Offspring(Plant plant, IEnumerable<Plant> all) =>
        all.Where(p => !p.IsDeleted && p.ParentPlantId == plant.Id)
           .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
           .ToList();

    /// <summary>The plant's parent, then its parent and so on (nearest first). Stops safely on cycles.</summary>
    public static IReadOnlyList<Plant> Ancestors(Plant plant, IEnumerable<Plant> all)
    {
        var byId = all.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);
        var result = new List<Plant>();
        var seen = new HashSet<Guid> { plant.Id };

        var parentId = plant.ParentPlantId;
        while (parentId is { } id && byId.TryGetValue(id, out var parent) && seen.Add(id))
        {
            result.Add(parent);
            parentId = parent.ParentPlantId;
        }

        return result;
    }

    /// <summary>
    /// The family around one plant: up to three generations above it (oldest first), its siblings,
    /// its offspring with how many plants descend from each, and its cuttings still going.
    /// </summary>
    public static FamilyTree Tree(Plant plant, IEnumerable<Plant> all, IEnumerable<Propagation> propagations)
    {
        var list = all.Where(p => !p.IsDeleted).ToList();

        var above = Ancestors(plant, list);
        var shown = above.Take(3).Reverse().ToList();

        var siblings = plant.ParentPlantId is { } parentId && above.Count > 0
            ? list.Where(p => p.ParentPlantId == parentId && p.Id != plant.Id)
                  .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                  .ToList()
            : [];

        var children = Offspring(plant, list)
            .Where(p => p.Id != plant.Id)
            .Select(child => new FamilyChild(child, Descendants(child, plant, list)))
            .ToList();

        var growing = propagations
            .Where(p => !p.IsDeleted && p.IsActive && p.ParentPlantId == plant.Id)
            .OrderBy(p => p.StartedOn)
            .ToList();

        return new FamilyTree(shown, above.Count > 3, siblings, children, growing);
    }

    // Plants descended from the child in any generation, each counted once. The focus plant is left out so a cycle can't count it.
    private static int Descendants(Plant child, Plant focus, List<Plant> list)
    {
        var seen = new HashSet<Guid> { focus.Id, child.Id };
        var count = 0;
        var queue = new Queue<Guid>([child.Id]);
        while (queue.TryDequeue(out var id))
        {
            foreach (var next in list.Where(p => p.ParentPlantId == id))
            {
                if (seen.Add(next.Id))
                {
                    count++;
                    queue.Enqueue(next.Id);
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Plants that can be chosen as parent: everything except the plant itself and its
    /// descendants, which would create a loop.
    /// </summary>
    public static IReadOnlyList<Plant> PossibleParents(Plant plant, IEnumerable<Plant> all)
    {
        var list = all.Where(p => !p.IsDeleted).ToList();
        var excluded = new HashSet<Guid> { plant.Id };

        // Walk down the family tree, collecting every descendant
        var queue = new Queue<Guid>([plant.Id]);
        while (queue.TryDequeue(out var id))
        {
            foreach (var child in list.Where(p => p.ParentPlantId == id))
            {
                if (excluded.Add(child.Id))
                    queue.Enqueue(child.Id);
            }
        }

        return list.Where(p => !excluded.Contains(p.Id))
                   .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                   .ToList();
    }

    /// <summary>
    /// What a plant has given: units taken per propagation type, how many of them rooted, the
    /// direct offspring given away or sold, and the plants descended from those offspring.
    /// </summary>
    public static PlantGiven Given(Plant plant, IEnumerable<Plant> all, IEnumerable<Propagation> propagations)
    {
        var list = all.Where(p => !p.IsDeleted).ToList();
        var taken = propagations.Where(p => !p.IsDeleted && p.ParentPlantId == plant.Id).ToList();

        var offspring = list.Where(p => p.ParentPlantId == plant.Id && p.Id != plant.Id).ToList();
        var seen = new HashSet<Guid> { plant.Id };
        foreach (var child in offspring)
            seen.Add(child.Id);

        // Everything below the offspring, counting each plant once
        var further = 0;
        var queue = new Queue<Guid>(offspring.Select(p => p.Id));
        while (queue.TryDequeue(out var id))
        {
            foreach (var child in list.Where(p => p.ParentPlantId == id))
            {
                if (seen.Add(child.Id))
                {
                    further++;
                    queue.Enqueue(child.Id);
                }
            }
        }

        return new PlantGiven(
            taken.GroupBy(p => p.Type)
                 .Select(g => new KeyValuePair<PropagationType, int>(g.Key, g.Sum(p => p.InitialCount)))
                 .OrderByDescending(g => g.Value).ThenBy(g => g.Key)
                 .ToList(),
            taken.Sum(PropagationResults.Succeeded),
            offspring.Count(p => p.Status == PlantStatus.GivenAway),
            offspring.Count(p => p.Status == PlantStatus.Sold),
            further);
    }
}

/// <param name="TakenByType">Units taken from the plant per propagation type, the most first.</param>
/// <param name="Rooted">Units that rooted, by the same rule as the propagation results.</param>
/// <param name="GivenAway">Direct offspring that were given away.</param>
/// <param name="Sold">Direct offspring that were sold.</param>
/// <param name="FromThoseInTurn">Plants descended from the offspring, not the offspring themselves.</param>
public sealed record PlantGiven(
    IReadOnlyList<KeyValuePair<PropagationType, int>> TakenByType,
    int Rooted,
    int GivenAway,
    int Sold,
    int FromThoseInTurn)
{
    public int Taken => TakenByType.Sum(t => t.Value);

    public bool IsEmpty => TakenByType.Count == 0 && GivenAway == 0 && Sold == 0 && FromThoseInTurn == 0;
}

/// <param name="Ancestors">Up to three, oldest first, ending with the parent.</param>
/// <param name="MoreAbove">There are ancestors beyond the three shown.</param>
public sealed record FamilyTree(
    IReadOnlyList<Plant> Ancestors,
    bool MoreAbove,
    IReadOnlyList<Plant> Siblings,
    IReadOnlyList<FamilyChild> Children,
    IReadOnlyList<Propagation> Growing)
{
    public bool IsEmpty => Ancestors.Count == 0 && Siblings.Count == 0 && Children.Count == 0 && Growing.Count == 0;
}

/// <param name="Below">Plants descended from this one, in every generation.</param>
public sealed record FamilyChild(Plant Plant, int Below);
