using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Products;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.SoilMixes;
using Stikling.Core.Stock;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tidy;

/// <summary>
/// Works out what the Tidy up page lists and does the one-at-a-time fixes on it. Nothing here
/// changes a record unless the person chose that one on the page.
/// </summary>
public sealed class TidyService(
    IPlantRepository plants,
    IPropagationRepository propagations,
    IPhotoRepository photos,
    IPlaceRepository places,
    IProductRepository products,
    ICareLogRepository careLogs,
    IPropagationCareLogRepository propagationCareLogs,
    IFeedRepository feeds,
    ISupplyRepository supplies,
    ISoilMixRepository mixes,
    IPutOffRepository putOffs)
{
    public async Task<TidyReport> GetAsync() => TidyChecks.Build(await ReadAsync());

    private async Task<TidyInput> ReadAsync() => new(
        await plants.GetAllAsync(),
        await propagations.GetAllAsync(),
        await photos.GetOfKindAsync(SubjectType.Plant),
        await places.GetPlacesAsync(),
        await products.GetAllAsync(),
        await careLogs.GetAllAsync(),
        await propagationCareLogs.GetAllAsync(),
        await feeds.GetAllAsync(),
        await supplies.GetAllAsync(),
        await mixes.GetAllAsync(),
        await putOffs.GetAllAsync());

    /// <summary>
    /// Says the two plants are not the same one, for good. It is a put-off with a fixed id, so two
    /// devices saying it make one record.
    /// </summary>
    public async Task NotTheSameAsync(Guid first, Guid second)
    {
        var key = TidyChecks.SameKey(first, second);
        var id = PutOff.IdFor(key);
        var putOff = await putOffs.GetAsync(id) ?? new PutOff { Id = id, Key = key };
        putOff.Until = DateOnly.MaxValue;
        await putOffs.SaveAsync(putOff);
    }

    /// <summary>
    /// Takes an unused tag off the plants that are no longer in the collection, which are the only
    /// ones that still carry it. Does nothing when a plant in the collection has the tag after all.
    /// Returns whether it was removed.
    /// </summary>
    public async Task<bool> RemoveTagAsync(string tag)
    {
        var all = await plants.GetAllAsync();
        if (all.Any(p => !p.IsDeleted && p.Status == PlantStatus.Active && !p.LivesElsewhere && PlantTags.Has(p, tag)))
            return false;

        foreach (var plant in all.Where(p => !p.IsDeleted && PlantTags.Has(p, tag)))
        {
            plant.Tags = PlantTags.Remove(plant.Tags, tag);
            await plants.SaveAsync(plant);
        }
        return true;
    }

    /// <summary>Deletes a product nothing points at. Read again first, so one used since the page loaded stays.</summary>
    public async Task<bool> RemoveProductAsync(Guid id)
    {
        var input = await ReadAsync();
        if (TidyChecks.ProductsInUse(input).Contains(id) || input.Products.All(p => p.Id != id))
            return false;
        await products.DeleteAsync(id);
        return true;
    }

    /// <summary>Deletes a soil mix no plant or propagation is in. Read again first, like a product.</summary>
    public async Task<bool> RemoveMixAsync(Guid id)
    {
        var input = await ReadAsync();
        if (!TidyChecks.Build(input).UnusedMixes.Any(m => m.Id == id))
            return false;
        await mixes.DeleteAsync(id);
        return true;
    }
}
