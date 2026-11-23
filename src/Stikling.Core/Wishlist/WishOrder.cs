using Stikling.Core.Models;

namespace Stikling.Core.Wishlist;

/// <summary>How open wishes are put in order by how much they are wanted.</summary>
public static class WishOrder
{
    /// <summary>0 for Must have, 1 for Want and for a wish with no level, 2 for Maybe.</summary>
    public static int Rank(Wish wish) => wish.Priority switch
    {
        WishPriority.MustHave => 0,
        WishPriority.Maybe => 2,
        _ => 1
    };

    /// <summary>The level a wish counts as: no level counts as Want.</summary>
    public static WishPriority Level(Wish wish) => wish.Priority ?? WishPriority.Want;

    /// <summary>Must have first, then Want and wishes with no level, then Maybe. The order within a level is kept.</summary>
    public static IOrderedEnumerable<Wish> ByRank(IEnumerable<Wish> wishes) => wishes.OrderBy(Rank);
}
