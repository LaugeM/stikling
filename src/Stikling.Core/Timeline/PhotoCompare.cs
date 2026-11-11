using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>Which photos can be laid next to each other to see how a plant grew, and which two it starts with.</summary>
public static class PhotoCompare
{
    /// <summary>
    /// The photos to compare, oldest first. Reference photos are left out, since they aren't of the plant
    /// as it grew, and so is a photo that comes twice. Photos from the same moment go by id, so the order is stable.
    /// </summary>
    public static IReadOnlyList<Photo> Comparable(IEnumerable<Photo> photos) =>
        photos
            .Where(p => !p.IsReference)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.TakenAt)
            .ThenBy(p => p.Id)
            .ToList();

    /// <summary>
    /// The two photos the compare view starts with, older first: the one that was being viewed and the oldest,
    /// or the newest when the viewed one is the oldest. Null with fewer than two photos or when the viewed one isn't in the list.
    /// </summary>
    public static (Guid First, Guid Second)? StartPair(IReadOnlyList<Photo> comparable, Guid viewedId)
    {
        if (comparable.Count < 2)
            return null;
        var index = -1;
        for (var i = 0; i < comparable.Count; i++)
        {
            if (comparable[i].Id == viewedId)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return null;
        return index == 0
            ? (comparable[0].Id, comparable[^1].Id)
            : (comparable[0].Id, viewedId);
    }
}
