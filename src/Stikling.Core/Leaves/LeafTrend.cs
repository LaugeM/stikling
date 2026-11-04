using Stikling.Core.Models;

namespace Stikling.Core.Leaves;

/// <summary>What the leaves so far say about a plant losing its variegation.</summary>
public enum LeafTrend
{
    None,

    /// <summary>The newest leaf is plain green.</summary>
    NewestReverted,

    /// <summary>Each of the last two leaves was less variegated than the one before.</summary>
    LessTwice
}

public static class LeafTrends
{
    /// <summary>
    /// The trend of the leaves, newest by day and then by when it was logged. Deleted leaves and
    /// ones with a rating this version doesn't know are ignored. A step down from an all white
    /// leaf doesn't count, since an all white leaf is rare and the next one isn't reverting.
    /// </summary>
    public static LeafTrend Of(IEnumerable<Leaf> leaves)
    {
        var ordered = leaves.Where(l => !l.IsDeleted && l.Rating is not null)
            .OrderBy(l => l.On).ThenBy(l => l.CreatedAt).Select(l => l.Rating!.Value).ToList();
        if (ordered.Count == 0)
            return LeafTrend.None;
        if (ordered[^1] == Variegation.Reverted)
            return LeafTrend.NewestReverted;
        if (ordered.Count >= 3 && Dropped(ordered[^3], ordered[^2]) && Dropped(ordered[^2], ordered[^1]))
            return LeafTrend.LessTwice;
        return LeafTrend.None;
    }

    private static bool Dropped(Variegation before, Variegation after) =>
        before != Variegation.AllWhite && after < before;
}
