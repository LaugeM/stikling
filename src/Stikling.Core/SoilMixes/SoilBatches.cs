using Stikling.Core.Models;
using Stikling.Core.Stock;

namespace Stikling.Core.SoilMixes;

/// <summary>A batch with what it holds, worked out from its mixings.</summary>
public sealed record BatchOption(SoilBatch Batch, decimal Litres, int Mixings)
{
    public Guid Id => Batch.Id;
}

/// <summary>
/// Batches of soil, worked out from the records. Pure, so it is tested without repositories. What a
/// merge of two devices can produce is handled here: mixings of a batch that is deleted or missing are
/// ignored, a batch whose mix is gone still counts, and the mixings can arrive in any order.
/// </summary>
public static class SoilBatches
{
    /// <summary>The litres in a batch: the sum of its mixings that aren't deleted, in any order.</summary>
    public static decimal Volume(Guid batchId, IEnumerable<BatchMixing> mixings) =>
        mixings.Where(m => !m.IsDeleted && m.BatchId == batchId).Sum(m => m.Litres);

    /// <summary>Every batch that isn't deleted, with its volume. Newest first, then by id so devices agree.</summary>
    public static IReadOnlyList<BatchOption> Options(IEnumerable<SoilBatch> batches, IEnumerable<BatchMixing> mixings)
    {
        var live = mixings.Where(m => !m.IsDeleted).ToLookup(m => m.BatchId);
        return [.. batches
            .Where(b => !b.IsDeleted)
            .GroupBy(b => b.Id).Select(g => g.First())
            .Select(b => new BatchOption(b, live[b.Id].Sum(m => m.Litres), live[b.Id].Count()))
            .OrderByDescending(o => o.Batch.MixedOn).ThenByDescending(o => o.Batch.CreatedAt).ThenBy(o => o.Batch.Id)];
    }

    /// <summary>The batches of a mix, newest first.</summary>
    public static IReadOnlyList<BatchOption> Of(Guid mixId, IEnumerable<BatchOption> options) =>
        [.. options.Where(o => o.Batch.SoilMixId == mixId)];

    /// <summary>
    /// The batch to fill in when a mix is picked: the newest one of that mix that isn't used up, or null
    /// when there is none. Only the order of the options matters, so every device picks the same one.
    /// </summary>
    public static BatchOption? Newest(Guid mixId, IEnumerable<BatchOption> options) =>
        Of(mixId, options).FirstOrDefault(o => !o.Batch.IsUsedUp);

    /// <summary>The batches open to pick for a mix: not used up, plus the one already picked.</summary>
    public static IReadOnlyList<BatchOption> Choices(Guid mixId, Guid? current, IEnumerable<BatchOption> options) =>
        [.. Of(mixId, options).Where(o => !o.Batch.IsUsedUp || o.Id == current)];
}

/// <summary>The batches of soil, their top-ups and what they take off the stock.</summary>
public sealed class SoilBatchService(
    ISoilBatchRepository batches,
    IBatchMixingRepository mixings,
    ISoilMixRepository mixes,
    Stock.ISupplyRepository supplies,
    Stock.StockService stock,
    TimeProvider time)
{
    public async Task<IReadOnlyList<BatchOption>> GetOptionsAsync() =>
        SoilBatches.Options(await batches.GetAllAsync(), await mixings.GetAllAsync());

    public async Task<IReadOnlyList<BatchMixing>> GetMixingsAsync(Guid batchId) =>
        [.. (await mixings.GetAllAsync()).Where(m => m.BatchId == batchId).OrderBy(m => m.MixedOn).ThenBy(m => m.CreatedAt).ThenBy(m => m.Id)];

    /// <summary>Starts a batch of a mix with its first mixing. Returns the mixing, for the stock check.</summary>
    public async Task<BatchMixing> MixAsync(SoilMix mix, decimal litres, DateOnly day)
    {
        if (litres <= 0)
            throw new InvalidOperationException(Stikling.Core.Resources.Supplies.BatchNeedsVolume);

        var batch = new SoilBatch { SoilMixId = mix.Id, MixName = mix.Name?.Trim(), MixedOn = day };
        await batches.SaveAsync(batch);
        return await TopUpAsync(batch.Id, litres, day);
    }

    /// <summary>Mixes more into a batch. A top-up of a batch that was used up opens it again.</summary>
    public async Task<BatchMixing> TopUpAsync(Guid batchId, decimal litres, DateOnly day)
    {
        var mixing = new BatchMixing { BatchId = batchId, MixedOn = day, Litres = litres };
        await mixings.SaveAsync(mixing);

        if (await batches.GetAsync(batchId) is { UsedUpOn: not null } batch)
        {
            var saved = batch.Copy();
            saved.UsedUpOn = null;
            await batches.SaveAsync(saved);
        }

        return mixing;
    }

    public async Task SetUsedUpAsync(SoilBatch batch, bool usedUp)
    {
        var saved = batch.Copy();
        saved.UsedUpOn = usedUp ? time.Today() : null;
        await batches.SaveAsync(saved);
    }

    /// <summary>Deletes one mixing, and what it took off the stock, so the levels go back up.</summary>
    public async Task DeleteMixingAsync(Guid mixingId)
    {
        await stock.ReturnAsync(mixingId);
        await mixings.DeleteAsync(mixingId);
    }

    /// <summary>
    /// Deletes a batch with its mixings and what they took off the stock. Plants and cuttings that point
    /// at it keep the link and just don't say which batch.
    /// </summary>
    public async Task DeleteBatchAsync(Guid batchId)
    {
        foreach (var mixing in (await mixings.GetAllAsync()).Where(m => m.BatchId == batchId))
            await DeleteMixingAsync(mixing.Id);
        await batches.DeleteAsync(batchId);
    }

    /// <summary>The lines for the check sheet after a mixing, from the mix as it is now.</summary>
    public async Task<IReadOnlyList<TakeoffLine>> TakeoffAsync(SoilMix mix, decimal litres) =>
        Takeoff.ForMixing(mix, litres, await supplies.GetAllAsync());

    /// <summary>Sets which supply each ingredient comes out of, and nothing else of the mix.</summary>
    public async Task LinkSuppliesAsync(SoilMix mix, IReadOnlyList<Guid?> supplyIds)
    {
        var saved = mix.Copy();
        for (var i = 0; i < saved.Ingredients.Count && i < supplyIds.Count; i++)
            saved.Ingredients[i].SupplyId = supplyIds[i];
        await mixes.SaveAsync(saved);
    }
}
