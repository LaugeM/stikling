using Stikling.Core.Feeds;
using Stikling.Core.Fields;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Pests;
using Stikling.Core.Pots;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.Settings;
using Stikling.Core.SoilMixes;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Plants;

/// <summary>What a <see cref="PlantForm"/> loads once, so the page doesn't read the same repositories again.</summary>
internal sealed record PlantFormData(
    IReadOnlyList<Plant> Plants,
    IReadOnlyList<Propagation> Propagations,
    Places Places,
    IReadOnlyList<Pot> Pots,
    IReadOnlyList<SoilMix> Mixes,
    IReadOnlyList<Feed> Feeds,
    SellerList Sellers,
    IReadOnlyList<OwnField> OwnFields,
    UserSettings Settings,
    IReadOnlyList<Wish> Wishes,
    PlantDictionary? Dictionary);

/// <summary>Makes the form for adding a plant or editing one.</summary>
public sealed class PlantForms(
    IPlantRepository plants,
    IPropagationRepository propagations,
    IPotRepository pots,
    ISoilMixRepository mixes,
    IFeedRepository feeds,
    IPestTreatmentRepository pestTreatments,
    SettingsService settings,
    PlantService plantService,
    WishService wishService,
    PlantNameService names,
    RoomService roomService,
    SellerService sellerService,
    OwnFieldService ownFieldService,
    TimeProvider time)
{
    internal PlantService PlantService => plantService;

    internal WishService WishService => wishService;

    internal RoomService RoomService => roomService;

    internal SellerService SellerService => sellerService;

    internal OwnFieldService OwnFieldBook => ownFieldService;

    internal IPestTreatmentRepository PestTreatments => pestTreatments;

    internal TimeProvider Time => time;

    private async Task<PlantFormData> LoadAsync(bool forNew)
    {
        var all = await plants.GetAllAsync();
        var places = await roomService.GetPlacesAsync();
        return new PlantFormData(
            all,
            await propagations.GetAllAsync(),
            places,
            await pots.GetAllAsync(),
            await mixes.GetAllAsync(),
            [.. (await feeds.GetAllAsync()).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)],
            await sellerService.GetSellersAsync(),
            await ownFieldService.GetAllAsync(),
            await settings.GetAsync(),
            forNew ? await wishService.GetAllAsync() : [],
            forNew ? await names.GetDictionaryAsync() : null);
    }

    /// <summary>
    /// A form for a new plant: empty, or a copy of another plant, or started from a wish, or a
    /// propagated plant of a parent. A copy wins over a wish, and a parent applies on top.
    /// </summary>
    public async Task<PlantForm> NewAsync(Guid? copyOfId, Guid? wishId, Guid? parentId, Func<Enum, string> label)
    {
        var data = await LoadAsync(forNew: true);
        var today = time.Today();

        Plant? plant = null;
        LooseDate? copiedDate = null;
        string? copiedFrom = null;
        if (copyOfId is { } copy && data.Plants.FirstOrDefault(p => p.Id == copy) is { } source)
        {
            plant = source.Duplicate();
            plant.AcquiredOn = LooseDate.Of(today);
            copiedDate = source.AcquiredOn;
            copiedFrom = source.DisplayName;
        }

        var fresh = plant is null;
        plant ??= new Plant { AcquiredOn = LooseDate.Of(today) };
        var wish = wishId is { } id ? data.Wishes.FirstOrDefault(w => w.Id == id && !w.IsGot) : null;
        var fromWish = false;
        if (fresh && copyOfId is null && wish is not null)
        {
            plant.Genus = wish.Genus;
            plant.Species = wish.Species;
            plant.Cultivar = wish.Cultivar;
            plant.NameQualifier = wish.NameQualifier;
            plant.NameNote = wish.NameNote;
            plant.Nickname = string.IsNullOrWhiteSpace(wish.Genus) ? wish.Name : null;
            plant.Source = wish.FoundAt;
            fromWish = true;
        }
        if (parentId is { } parentKey && data.Plants.FirstOrDefault(p => p.Id == parentKey) is { } parent)
        {
            plant.Origin = PlantOrigin.Propagated;
            plant.ParentPlantId = parent.Id;
            plant.Genus = parent.Genus;
            plant.Species = parent.Species;
            plant.Cultivar = parent.Cultivar;
            plant.NameQualifier = parent.NameQualifier;
            plant.NameNote = parent.NameNote;
            plant.PlaceId = parent.PlaceId;
        }

        var form = new PlantForm(this, data, label, plant, null, fromWish, copiedDate, copiedFrom, wish);
        if (fresh)
            form.OriginChanged();
        form.RememberStart();
        return form;
    }

    /// <summary>A form for editing a plant, or null when there is no such plant.</summary>
    public async Task<PlantForm?> EditAsync(Guid id, Func<Enum, string> label)
    {
        var data = await LoadAsync(forNew: false);
        if (data.Plants.FirstOrDefault(p => p.Id == id) is not { } plant)
            return null;

        return new PlantForm(this, data, label, plant, plant.Copy(), false, null, null, null);
    }
}
