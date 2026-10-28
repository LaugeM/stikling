using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Pots;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.SoilMixes;
using Stikling.Core.Plants;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Propagations;

/// <summary>What a <see cref="PropagationForm"/> loads once, so the page doesn't read the same repositories again.</summary>
internal sealed record PropagationFormData(
    IReadOnlyList<Plant> Plants,
    IReadOnlyList<Propagation> Propagations,
    Places Places,
    IReadOnlyList<SoilMix> Mixes,
    IReadOnlyList<PotUse> PotUses,
    ContainerList ContainerList,
    SellerList Sellers,
    IReadOnlyList<Wish> Wishes,
    PlantDictionary? Dictionary);

/// <summary>Makes the form for starting a propagation or editing one.</summary>
public sealed class PropagationForms(
    IPlantRepository plants,
    IPropagationRepository propagations,
    ISoilMixRepository mixes,
    PropagationService propagationService,
    WishService wishService,
    PlantNameService names,
    RoomService roomService,
    SellerService sellerService,
    PotService potService,
    TimeProvider time)
{
    internal PropagationService PropagationService => propagationService;

    internal WishService WishService => wishService;

    internal RoomService RoomService => roomService;

    internal SellerService SellerService => sellerService;

    internal PotService PotService => potService;

    internal TimeProvider Time => time;

    /// <summary>Whether a propagation is stored. When that can't be read either, it counts as stored, so its photos are kept.</summary>
    internal async Task<bool> IsStoredAsync(Guid id)
    {
        try
        {
            return await propagations.GetAsync(id) is not null;
        }
        catch
        {
            return true;
        }
    }

    private async Task<PropagationFormData> LoadAsync(bool forNew)
    {
        var allPlants = await plants.GetAllAsync();
        var allPropagations = await propagations.GetAllAsync();
        var potUses = await potService.GetAllAsync();
        return new PropagationFormData(
            [.. allPlants.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)],
            allPropagations,
            await roomService.GetPlacesAsync(),
            await mixes.GetAllAsync(),
            potUses,
            new ContainerList(potUses.Select(u => u.Pot)),
            await sellerService.GetSellersAsync(),
            forNew ? await wishService.GetAllAsync() : [],
            forNew ? await names.GetDictionaryAsync() : null);
    }

    /// <summary>
    /// A form for a new propagation: taken from a plant, or started from a wish, or empty. A parent
    /// wins over a wish.
    /// </summary>
    /// <param name="label">Gives the labels for a propagation. It reads the type when called, so the form builds it once.</param>
    public async Task<PropagationForm> NewAsync(Guid? parentId, Guid? wishId, Func<Propagation, Func<Enum, string>> label)
    {
        var data = await LoadAsync(forNew: true);
        var today = time.Today();

        var wish = wishId is { } id ? data.Wishes.FirstOrDefault(w => w.Id == id && !w.IsGot) : null;
        Propagation propagation;
        if (parentId is { } parentKey && data.Plants.FirstOrDefault(p => p.Id == parentKey) is { } parent)
        {
            propagation = propagationService.StartFrom(parent);
        }
        else if (wish is not null)
        {
            // A cutting of a wished-for plant starts with the wish's names and where to find it
            propagation = new Propagation
            {
                StartedOn = today,
                Genus = wish.Genus,
                Species = wish.Species,
                Cultivar = wish.Cultivar,
                NameQualifier = wish.NameQualifier,
                NameNote = wish.NameNote,
                Nickname = string.IsNullOrWhiteSpace(wish.Genus) ? wish.Name : null,
                Source = wish.FoundAt
            };
        }
        else
        {
            propagation = new Propagation { StartedOn = today };
        }

        var form = new PropagationForm(this, data, label, propagation, null, wish);
        form.SuggestNumber();
        return form;
    }

    /// <summary>A form for editing a propagation, or null when there is no such propagation.</summary>
    public async Task<PropagationForm?> EditAsync(Guid id, Func<Propagation, Func<Enum, string>> label)
    {
        var data = await LoadAsync(forNew: false);
        if (data.Propagations.FirstOrDefault(p => p.Id == id) is not { } propagation)
            return null;

        return new PropagationForm(this, data, label, propagation, propagation.Copy(), null);
    }
}
