using Stikling.Core.Models;
using Stikling.Core.Products;
using Stikling.Core.Stock;

namespace Stikling.Core.Feeds;

/// <summary>
/// "Mixed a can": a can of a feed was made up with some water. Each is a record of its own with the doses
/// as they were, and it is what the products come off the stock for. Logging a feed on a plant changes
/// nothing on stock, so watering ten plants from one can counts once.
/// </summary>
public sealed class FeedMixingService(
    IFeedMixingRepository mixings,
    IProductRepository products,
    ISupplyRepository supplies,
    StockService stock)
{
    /// <summary>The day each feed was last mixed, for the feeds that have been.</summary>
    public static IReadOnlyDictionary<Guid, DateOnly> LastMixed(IEnumerable<FeedMixing> all) => all
        .Where(m => !m.IsDeleted)
        .GroupBy(m => m.FeedId)
        .ToDictionary(g => g.Key, g => g.Max(m => m.MixedOn));

    public async Task<IReadOnlyDictionary<Guid, DateOnly>> GetLastMixedAsync() => LastMixed(await mixings.GetAllAsync());

    /// <summary>The cans mixed of one feed, newest first.</summary>
    public async Task<IReadOnlyList<FeedMixing>> GetForFeedAsync(Guid feedId) =>
        [.. (await mixings.GetAllAsync()).Where(m => m.FeedId == feedId).OrderByDescending(m => m.MixedOn).ThenByDescending(m => m.CreatedAt).ThenBy(m => m.Id)];

    /// <summary>Saves a can with the feed's doses as they are today.</summary>
    public async Task<FeedMixing> MixAsync(Feed feed, decimal litres, DateOnly day)
    {
        var mixing = new FeedMixing
        {
            FeedId = feed.Id,
            FeedName = feed.Name?.Trim(),
            MixedOn = day,
            Litres = litres,
            Doses = feed.Doses(await products.GetAllAsync())
        };
        await mixings.SaveAsync(mixing);
        return mixing;
    }

    /// <summary>The lines for the check sheet: each product that has a supply, its dose times the water.</summary>
    public async Task<IReadOnlyList<TakeoffLine>> TakeoffAsync(FeedMixing mixing) =>
        Takeoff.ForCan(mixing.Doses, mixing.Litres, await supplies.GetAllAsync());

    /// <summary>Deletes a can and what it took off the stock, so the levels go back up.</summary>
    public async Task DeleteAsync(Guid mixingId)
    {
        await stock.ReturnAsync(mixingId);
        await mixings.DeleteAsync(mixingId);
    }
}
