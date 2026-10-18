using Stikling.Core.Feeds;
using Stikling.Core.Models;
using Stikling.Core.Products;

namespace Stikling.Core.Care;

/// <summary>
/// Logging what Today lists as due from a care interval. It writes ordinary care entries, so the
/// history, the care summary and the next due day all read them like any other.
/// </summary>
public sealed class CareIntervalService(
    CareService care,
    ICareLogRepository logs,
    IFeedRepository feeds,
    IProductRepository products,
    IOwnCareKindRepository ownKinds,
    TimeProvider time)
{
    /// <summary>
    /// Logs the plants as done today. A feed that still exists puts its products on each entry. Returns
    /// the ids of the entries made, so they can be taken back.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> LogAsync(IntervalGroup group, IEnumerable<Guid> plantIds, Func<Enum, string> label)
    {
        var ids = plantIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        Feed? feed = null;
        IReadOnlyList<ProductDose>? doses = null;
        if (group.FeedId is { } feedId && await feeds.GetAsync(feedId) is { IsDeleted: false } found)
        {
            feed = found;
            doses = found.Doses(await products.GetAllAsync());
        }

        string? ownName = null;
        if (group.OwnKindId is { } ownId)
        {
            var own = CareLabels.Lookup(await ownKinds.GetAllAsync());
            ownName = own.TryGetValue(ownId, out var kind) ? kind.Name : null;
        }

        var before = (await logs.GetAllAsync()).Select(l => l.Id).ToHashSet();
        await care.LogManyAsync(ids, group.Kind, time.Today(), null, label,
            products: doses, feed: feed, ownKindId: group.OwnKindId, ownKindName: ownName);

        return [.. (await logs.GetAllAsync()).Where(l => !before.Contains(l.Id)).Select(l => l.Id)];
    }

    /// <summary>Takes back what <see cref="LogAsync"/> logged.</summary>
    public async Task UndoAsync(IEnumerable<Guid> entryIds)
    {
        foreach (var id in entryIds)
            await care.DeleteAsync(id);
    }
}
