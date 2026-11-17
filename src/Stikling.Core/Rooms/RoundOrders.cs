namespace Stikling.Core.Rooms;

/// <summary>A room's own walking order for going round it, see <see cref="Models.Place.RoundOrder"/>.</summary>
public static class RoundOrders
{
    /// <summary>
    /// The items in the order set for the room. Ids that match no item are skipped. An item that isn't
    /// in the order goes after the last item in the same place, or at the end when none is there.
    /// </summary>
    /// <param name="items">What is in the round, in the usual order.</param>
    public static IReadOnlyList<T> Apply<T>(
        IReadOnlyList<T> items, IReadOnlyList<Guid> order, Func<T, Guid> idOf, Func<T, Guid?> placeOf)
    {
        var position = new Dictionary<Guid, int>();
        for (var i = 0; i < order.Count; i++)
            position.TryAdd(order[i], i);

        var result = items.Where(c => position.ContainsKey(idOf(c))).OrderBy(c => position[idOf(c)]).ToList();
        foreach (var item in items.Where(c => !position.ContainsKey(idOf(c))))
        {
            var at = placeOf(item);
            var last = at is null ? -1 : result.FindLastIndex(c => placeOf(c) == at);
            result.Insert(last < 0 ? result.Count : last + 1, item);
        }
        return result;
    }
}
