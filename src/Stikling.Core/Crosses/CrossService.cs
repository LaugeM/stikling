using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Crosses;

/// <summary>Crosses and seed lots: reading them together with what was sown from them, and saving.</summary>
public sealed class CrossService(
    ICrossRepository crosses,
    ISeedLotRepository lots,
    IPlantRepository plants,
    IPropagationRepository propagations)
{
    // Must never change. The lot a cross's Make a seed lot makes gets its id from this and the cross's id,
    // so pressing it on two devices makes one lot.
    private static readonly Guid LotNamespace = new("5b1e8c7a-3d24-4f69-8a0b-c2e94d716f35");

    /// <summary>Reads everything the book needs. One read, so a page asks once.</summary>
    public async Task<CrossBook> GetBookAsync() =>
        new(await crosses.GetAllAsync(), await lots.GetAllAsync(), await propagations.GetAllAsync(), await plants.GetAllAsync());

    public Task<Cross?> GetAsync(Guid id) => crosses.GetAsync(id);

    public Task<SeedLot?> GetLotAsync(Guid id) => lots.GetAsync(id);

    public Task SaveAsync(Cross cross) => crosses.SaveAsync(cross);

    public Task DeleteAsync(Guid id) => crosses.DeleteAsync(id);

    public Task SaveLotAsync(SeedLot lot) => lots.SaveAsync(lot);

    public Task DeleteLotAsync(Guid id) => lots.DeleteAsync(id);

    /// <summary>
    /// A new cross with the plant as pod parent or as pollen parent, for starting one from a plant's page.
    /// The other side is left for the person to fill in.
    /// </summary>
    public static Cross StartFrom(Plant plant, bool asPollenParent, DateOnly today)
    {
        var cross = new Cross { PollinatedOn = today };
        if (asPollenParent)
        {
            cross.PollenParentPlantId = plant.Id;
            cross.PollenParentName = plant.DisplayName;
        }
        else
        {
            cross.PodParentPlantId = plant.Id;
            cross.PodParentName = plant.DisplayName;
        }

        return cross;
    }

    /// <summary>The id of the lot made from a cross. The same on every device.</summary>
    public static Guid LotIdFor(Guid crossId) => NameBasedId.For(LotNamespace, crossId.ToString("N"));

    /// <summary>
    /// A lot filled in from a cross with ripe seed: named after the pod parent's genus and species,
    /// collected on the ripe day and with the cross's amount. Not saved. When the cross has made a lot
    /// already, that one is returned instead, so the action can't make a second.
    /// </summary>
    public async Task<SeedLot> LotFromCrossAsync(Cross cross)
    {
        var id = LotIdFor(cross.Id);
        var all = await lots.GetAllAsync();
        if (all.FirstOrDefault(l => l.Id == id) is { } existing)
            return existing;

        var pod = cross.PodParentPlantId is { } podId ? await plants.GetAsync(podId) : null;
        return new SeedLot
        {
            Id = id,
            CrossId = cross.Id,
            Genus = pod?.Genus,
            Species = pod?.KnownSpecies,
            CollectedOn = cross.SeedRipeOn,
            Amount = cross.SeedAmount,
            AmountUnit = cross.SeedAmountUnit,
            AmountIsAbout = cross.SeedAmountIsAbout
        };
    }

    /// <summary>
    /// A seed propagation to sow from a lot: the lot's names, the seeds counted when the amount is in
    /// seeds, and the pod parent as the parent plant when it is a plant in the app, so everything that
    /// follows one parent keeps working. Not saved.
    /// </summary>
    public async Task<Propagation> SowingFromAsync(SeedLot lot, DateOnly today)
    {
        var propagation = new Propagation
        {
            Type = PropagationType.Seed,
            Medium = GrowingMedium.Soil,
            StartedOn = today,
            Genus = lot.Genus,
            Species = lot.Species,
            Cultivar = lot.Cultivar,
            SeedLotId = lot.Id,
            Source = lot.Source,
            SellerId = lot.SellerId,
            InitialCount = SeedAmounts.SeedCount(lot.Amount, lot.AmountUnit) ?? 1
        };

        if (lot.CrossId is { } crossId && await crosses.GetAsync(crossId) is { PodParentPlantId: { } podId }
            && await plants.GetAsync(podId) is { } pod)
        {
            propagation.ParentPlantId = pod.Id;
            // The seedlings take the pod parent's names when the lot has none
            propagation.Genus ??= pod.Genus;
            propagation.Species ??= pod.KnownSpecies;
        }

        return propagation;
    }
}
