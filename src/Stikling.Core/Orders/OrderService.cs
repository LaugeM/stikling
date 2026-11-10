using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Sellers;
using Stikling.Core.Wishlist;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Orders;

/// <summary>
/// Orders: what a page does with them, and unpacking a box into plants and cuttings. Everything goes
/// through the repositories, so dates are set. Orders and items are records of their own, so two
/// devices adding items to one order don't overwrite each other.
/// </summary>
public sealed class OrderService(
    IOrderRepository orders,
    IOrderItemRepository items,
    ISellerRepository sellers,
    IPlantRepository plants,
    IPropagationRepository propagations,
    PlantService plantService,
    PropagationService propagationService,
    IWishRepository wishes,
    WishService wishService)
{
    /// <summary>
    /// Every order with its items, newest first. An item whose order is deleted or missing is left out,
    /// and so is a deleted item.
    /// </summary>
    public async Task<IReadOnlyList<OrderView>> GetAllAsync()
    {
        var allOrders = await orders.GetAllAsync();
        if (allOrders.Count == 0)
            return [];

        var byOrder = (await items.GetAllAsync()).ToLookup(i => i.OrderId);
        var sellerList = await sellers.GetSellersAsync();
        var plantPrices = (await plants.GetAllAsync()).ToDictionary(p => p.Id, p => p.PricePaid);

        return allOrders
            .OrderByDescending(o => o.OrderedOn).ThenByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Select(o => ViewOf(o, byOrder[o.Id], sellerList, plantPrices))
            .ToList();
    }

    /// <summary>One order with its items, or null when it is gone.</summary>
    public async Task<OrderView?> GetAsync(Guid id) =>
        (await GetAllAsync()).FirstOrDefault(v => v.Order.Id == id);

    private static OrderView ViewOf(Order order, IEnumerable<OrderItem> orderItems, SellerList sellerList, Dictionary<Guid, decimal?> plantPrices)
    {
        var list = orderItems.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList();
        var seller = sellerList.For(order.SellerId, order.SellerName);
        var prices = new Dictionary<Guid, decimal>();
        foreach (var item in list)
        {
            // Once an item is a plant, the plant's price is the one that counts. A plant that is gone leaves the item's own.
            var price = item.PlantId is { } plantId && plantPrices.TryGetValue(plantId, out var paid) ? paid : item.Price;
            if (price is { } value)
                prices[item.Id] = value;
        }

        return new OrderView
        {
            Order = order,
            Items = list,
            SellerId = seller?.Id,
            SellerName = seller?.Name ?? SellerName.Clean(order.SellerName),
            Prices = prices
        };
    }

    /// <summary>
    /// Saves an order with its items. Items that were on the order and aren't in the list are soft
    /// deleted. When the price of an item that is a plant changed here, the plant's price is set to it,
    /// since the plant's price is the one truth once it is a plant. The plant is only touched when the
    /// price on the item changed, so saving an order never overwrites a price changed on the plant.
    /// </summary>
    /// <param name="removedItemIds">The items the form removed. Only these are deleted: an item the form never
    /// loaded, like one added on another device meanwhile, stays.</param>
    public async Task SaveAsync(Order order, IReadOnlyList<OrderItem> orderItems, IReadOnlyCollection<Guid>? removedItemIds = null)
    {
        // Validate everything first, so a bad item doesn't leave the order half saved
        foreach (var item in orderItems)
        {
            if (item.Validate().FirstOrDefault() is { } error)
                throw new InvalidOperationException(error);
        }

        await orders.SaveAsync(order);

        var stored = (await items.GetAllAsync()).Where(i => i.OrderId == order.Id).ToDictionary(i => i.Id);
        foreach (var gone in stored.Values.Where(s => removedItemIds?.Contains(s.Id) == true && orderItems.All(i => i.Id != s.Id)))
            await items.DeleteAsync(gone.Id);

        foreach (var item in orderItems)
        {
            item.OrderId = order.Id;
            // A plant added to the order here gets the order's seller if it has none, like AddPlantAsync
            if (item.PlantId is { } linked && !stored.ContainsKey(item.Id) && await plants.GetAsync(linked) is { } added)
                await GiveSellerAsync(added, order.SellerId, SellerName.Clean(order.SellerName));

            var priceChanged = stored.TryGetValue(item.Id, out var before) ? before.Price != item.Price : item.Price is not null;
            if (priceChanged && item.Price is { } price && item.PlantId is { } plantId
                && await plants.GetAsync(plantId) is { } plant && plant.PricePaid != price)
            {
                plant.PricePaid = price;
                await plants.SaveAsync(plant);
            }
            await items.SaveAsync(item);
        }
    }

    /// <summary>
    /// Adds a plant that is already in the app to an order, for orders entered after the fact. The item
    /// takes the plant's names and shows the plant's price. A plant with no seller and no Source gets the
    /// order's seller. A plant that is already on the order isn't added twice.
    /// </summary>
    public async Task<OrderItem?> AddPlantAsync(Guid orderId, Guid plantId)
    {
        if (await GetAsync(orderId) is not { } view || await plants.GetAsync(plantId) is not { } plant)
            return null;
        if (view.Items.FirstOrDefault(i => i.PlantId == plantId) is { } already)
            return already;

        await GiveSellerAsync(plant, view.SellerId, view.SellerName);

        var item = new OrderItem
        {
            OrderId = orderId,
            Name = plant.Nickname,
            Genus = plant.Genus,
            Species = plant.Species,
            Cultivar = plant.Cultivar,
            PlantId = plantId
        };
        await items.SaveAsync(item);
        return item;
    }

    // A plant with no seller and no Source is taken to come from the order's seller. One that has either is left alone.
    private async Task GiveSellerAsync(Plant plant, Guid? sellerId, string? sellerName)
    {
        if (plant.SellerId is not null || !string.IsNullOrWhiteSpace(plant.Source) || sellerName is null)
            return;

        plant.SellerId = sellerId;
        plant.Source = sellerName;
        await plants.SaveAsync(plant);
    }

    /// <summary>Adds a wish to an order, with its names and no price. A wish already on the order isn't added twice.</summary>
    public async Task<OrderItem?> AddWishAsync(Guid orderId, Guid wishId)
    {
        if (await GetAsync(orderId) is not { } view || await wishes.GetAsync(wishId) is not { } wish)
            return null;
        if (view.Items.FirstOrDefault(i => i.WishId == wishId) is { } already)
            return already;

        var item = new OrderItem
        {
            OrderId = orderId,
            Name = wish.Name,
            Genus = wish.Genus,
            Species = wish.Species,
            Cultivar = wish.Cultivar,
            WishId = wishId
        };
        await items.SaveAsync(item);
        return item;
    }

    /// <summary>
    /// Unpacks the chosen items of an order. Each becomes a plant or a cutting, filled in from the
    /// order, and points at it. Items left out of <paramref name="choices"/> stay on the way, and so
    /// do ones that already became something. The new records get ids made from the item, so a run
    /// that stopped halfway can be done again without making a second plant.
    /// </summary>
    /// <param name="label">Names enums for the history of a new cutting.</param>
    public async Task<UnpackResult> UnpackAsync(
        Guid orderId, DateOnly arrivedOn, Guid? placeId, IReadOnlyList<UnpackChoice> choices, Func<Enum, string> label)
    {
        var plantIds = new List<Guid>();
        var propagationIds = new List<Guid>();
        if (await GetAsync(orderId) is not { } view)
            return new UnpackResult(plantIds, propagationIds);

        var sellerName = view.SellerName;
        foreach (var choice in choices)
        {
            if (view.Items.FirstOrDefault(i => i.Id == choice.ItemId) is not { IsOnTheWay: true } item)
                continue;

            if (new[] { choice.Genus, choice.Species, choice.Cultivar, choice.Name }.Any(t => !string.IsNullOrWhiteSpace(t)))
            {
                item.Genus = Clean(choice.Genus);
                item.Species = Clean(choice.Species);
                item.Cultivar = Clean(choice.Cultivar);
                item.Name = Clean(choice.Name);
            }

            // With no genus the name typed is all there is, so it becomes the nickname
            var nickname = string.IsNullOrWhiteSpace(item.Genus) ? item.Name?.Trim() : null;

            if (choice.UnpackAs == UnpackAs.Plant)
            {
                var plantId = NameBasedId.For("order-item-plant:" + item.Id);
                // Already made by an earlier run, here or on another device: it is linked, not made or overwritten again
                if (await plants.GetAsync(plantId) is not null)
                {
                    item.PlantId = plantId;
                    item.ArrivedOn = arrivedOn;
                    await items.SaveAsync(item);
                    if (item.WishId is { } linkedWish)
                        await wishService.MarkGotAsync(linkedWish, item.PlantId, item.PropagationId);
                    continue;
                }

                var plant = new Plant
                {
                    Id = plantId,
                    Nickname = nickname,
                    Genus = item.Genus,
                    Species = item.Species,
                    Cultivar = item.Cultivar,
                    Origin = PlantOrigin.Purchased,
                    Source = sellerName,
                    SellerId = view.SellerId,
                    PricePaid = item.Price,
                    AcquiredOn = LooseDate.Of(arrivedOn),
                    ArrivedIn = choice.Rating,
                    ArrivalProblems = [.. choice.Problems],
                    ArrivalNote = Clean(choice.ArrivalNote),
                    PlaceId = placeId
                };
                await plantService.CreateAsync(plant);
                item.PlantId = plant.Id;
                plantIds.Add(plant.Id);
            }
            else
            {
                var cuttingId = NameBasedId.For("order-item-cutting:" + item.Id);
                if (await propagations.GetAsync(cuttingId) is not null)
                {
                    item.PropagationId = cuttingId;
                    item.ArrivedOn = arrivedOn;
                    await items.SaveAsync(item);
                    if (item.WishId is { } linkedWish)
                        await wishService.MarkGotAsync(linkedWish, item.PlantId, item.PropagationId);
                    continue;
                }

                var cutting = new Propagation
                {
                    Id = cuttingId,
                    Nickname = nickname,
                    Genus = item.Genus,
                    Species = item.Species,
                    Cultivar = item.Cultivar,
                    Source = sellerName,
                    SellerId = view.SellerId,
                    ArrivedIn = choice.Rating,
                    ArrivalProblems = [.. choice.Problems],
                    ArrivalNote = Clean(choice.ArrivalNote),
                    PlaceId = placeId,
                    Type = PropagationType.Cutting,
                    StartedOn = arrivedOn
                };
                await propagationService.CreateAsync(cutting, label);
                item.PropagationId = cutting.Id;
                propagationIds.Add(cutting.Id);
            }

            item.ArrivedOn = arrivedOn;
            await items.SaveAsync(item);

            if (item.WishId is { } wishId)
                await wishService.MarkGotAsync(wishId, item.PlantId, item.PropagationId);
        }

        return new UnpackResult(plantIds, propagationIds);
    }

    /// <summary>Marks an item as not in the box, or takes the mark off. An item that became a plant can't be marked.</summary>
    public async Task SetDidNotComeAsync(Guid itemId, bool didNotCome)
    {
        if (await items.GetAsync(itemId) is not { } item || item.DidNotCome == didNotCome)
            return;
        if (didNotCome && (item.PlantId is not null || item.PropagationId is not null))
            return;

        item.DidNotCome = didNotCome;
        await items.SaveAsync(item);
    }

    /// <summary>
    /// Deletes an order and its items. The plants and propagations made from it stay, and a wish on an
    /// item that was still on the way is simply open again.
    /// </summary>
    public async Task DeleteAsync(Guid orderId)
    {
        foreach (var item in (await items.GetAllAsync()).Where(i => i.OrderId == orderId))
            await items.DeleteAsync(item.Id);
        await orders.DeleteAsync(orderId);
    }

    /// <summary>
    /// The open wishes that are on an order on the way, by wish id, with that order and its seller. A
    /// wish whose item didn't come or was deleted, or whose order is gone, is a plain open wish.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, WishOnOrder>> WishesOnTheWayAsync()
    {
        var result = new Dictionary<Guid, WishOnOrder>();
        var open = (await wishes.GetAllAsync()).Where(w => !w.IsGot).Select(w => w.Id).ToHashSet();
        // Newest order last, so it is the one a wish on two orders shows
        foreach (var view in (await GetAllAsync()).Reverse())
        {
            foreach (var item in view.Items.Where(i => i.IsOnTheWay && i.WishId is { } id && open.Contains(id)))
                result[item.WishId!.Value] = new WishOnOrder(view.Order.Id, view.SellerName);
        }
        return result;
    }

    /// <summary>How many orders are on the way, for the Supplies row.</summary>
    public async Task<int> OnTheWayCountAsync() => (await GetAllAsync()).Count(v => v.IsOnTheWay);

    /// <summary>The orders from one seller, newest first, following merges.</summary>
    public async Task<IReadOnlyList<OrderView>> ForSellerAsync(Guid sellerId) =>
        (await GetAllAsync()).Where(v => v.SellerId == sellerId).ToList();
}
