using Stikling.Core.Models;

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
}
