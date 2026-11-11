using Stikling.Core.Models;
using Stikling.Core.Pots;
using Stikling.Core.Products;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Stock;

/// <summary>What the bought sheet asks for. Everything but the day is optional.</summary>
public sealed record BoughtDetails(DateOnly Day, decimal? Amount, decimal? Price, Guid? SellerId, bool RaisePot);

/// <summary>
/// The stock of loose supplies and the shopping list. Stock is never a number that gets changed: every
/// change is an entry of its own, so two devices can't overwrite each other, and the level is worked
/// out from the entries by <see cref="StockView"/>.
/// </summary>
public sealed class StockService(
    ISupplyRepository supplies,
    ISupplyEntryRepository entries,
    IShoppingItemRepository items,
    IProductRepository products,
    IPotRepository pots,
    TimeProvider time)
{
    public async Task<StockView> GetAsync() => StockView.Build(
        await supplies.GetAllAsync(),
        await entries.GetAllAsync(),
        await items.GetAllAsync(),
        await products.GetAllAsync(),
        await pots.GetAllAsync(),
        time.LocalDay,
        time.Today());

    /// <summary>
    /// Saves a supply. One for a product always gets the product's fixed id, so a second device making
    /// one for the same product makes the same record, and one that was deleted is brought back. Its
    /// kind is Water and its name is copied from the product as a fallback.
    /// </summary>
    public async Task SaveSupplyAsync(Supply supply)
    {
        if (supply.ProductId is { } productId)
        {
            supply.Id = Supply.IdForProduct(productId);
            supply.Kind = SupplyKind.Water;
            if (await products.GetAsync(productId) is { } product)
                supply.Name = Clean(product.Name);
        }

        supply.Name = Clean(supply.Name);
        supply.Notes = Clean(supply.Notes);
        await supplies.SaveAsync(supply);
    }

    /// <summary>
    /// Deletes a supply and its entries. A product's supply keeps its fixed id, so without this a supply
    /// made again for the same product would pick up the old counts and purchases.
    /// </summary>
    public async Task DeleteSupplyAsync(Guid supplyId)
    {
        foreach (var entry in (await entries.GetAllAsync()).Where(e => e.SupplyId == supplyId && !e.IsDeleted))
            await entries.DeleteAsync(entry.Id);
        await supplies.DeleteAsync(supplyId);
    }

    /// <summary>Adds an entry for a supply: it was bought, is running low or out, or was counted.</summary>
    public async Task<SupplyEntry> MarkAsync(
        Guid supplyId, SupplyEntryKind kind, decimal? amount, decimal? price, Guid? sellerId, DateTimeOffset at)
    {
        var bought = kind == SupplyEntryKind.Bought;
        var entry = new SupplyEntry
        {
            SupplyId = supplyId,
            At = at,
            Kind = kind,
            Amount = kind is SupplyEntryKind.Bought or SupplyEntryKind.Counted ? amount : null,
            Price = bought ? price : null,
            SellerId = bought ? sellerId : null
        };
        await entries.SaveAsync(entry);
        return entry;
    }

    /// <summary>Takes an entry back, e.g. a tick on the shopping list. The entry is soft deleted.</summary>
    public Task UndoAsync(Guid entryId) => entries.DeleteAsync(entryId);

    /// <summary>
    /// Adds the amount, price and seller to a Bought entry that was ticked without them, and the day it
    /// was bought on.
    /// </summary>
    public async Task UpdateBoughtAsync(Guid entryId, BoughtDetails details)
    {
        if (await entries.GetAsync(entryId) is not { } entry)
            return;
        entry.Amount = details.Amount;
        entry.Price = details.Price;
        entry.SellerId = details.SellerId;
        entry.At = MomentOn(details.Day, entry.At);
        await entries.SaveAsync(entry);
    }

    /// <summary>
    /// Marks a product as running low. Its supply is made if there is none, with a fixed id, and
    /// brought back if it was deleted.
    /// </summary>
    public async Task<SupplyEntry> RunningLowForProductAsync(Product product)
    {
        var id = Supply.IdForProduct(product.Id);
        if (await supplies.GetAsync(id) is null)
            await SaveSupplyAsync(new Supply { Id = id, ProductId = product.Id, Kind = SupplyKind.Water, Unit = Supply.DefaultUnit(SupplyKind.Water) });
        return await MarkAsync(id, SupplyEntryKind.RunningLow, null, null, null, time.GetLocalNow());
    }

    /// <summary>Puts something typed on the shopping list.</summary>
    public async Task<ShoppingItem?> AddLineAsync(string? text)
    {
        if (Clean(text) is not { } clean)
            return null;
        var item = new ShoppingItem { Text = clean };
        await items.SaveAsync(item);
        return item;
    }

    /// <summary>
    /// Puts a pot on the shopping list, unless it is already there waiting to be bought. Returns the
    /// line that was made, or null when there already was one.
    /// </summary>
    public async Task<ShoppingItem?> AddPotAsync(Pot pot)
    {
        if ((await items.GetAllAsync()).Any(i => i.PotId == pot.Id && i.BoughtAt is null))
            return null;
        var item = new ShoppingItem { Text = pot.StoredName, PotId = pot.Id };
        await items.SaveAsync(item);
        return item;
    }

    /// <summary>Ticks off a loose line as bought, now.</summary>
    public async Task TickAsync(ShoppingItem item)
    {
        var saved = item.Copy();
        saved.BoughtAt = time.GetLocalNow();
        await items.SaveAsync(saved);
    }

    /// <summary>Takes a tick off a loose line.</summary>
    public async Task UntickAsync(ShoppingItem item)
    {
        var saved = item.Copy();
        saved.BoughtAt = null;
        await items.SaveAsync(saved);
    }

    /// <summary>
    /// Adds what was paid and bought to a line that was ticked off. For a pot line with an amount, the
    /// pot's Owned can go up by it, once: only the first time an amount is given, so editing the line
    /// again can't count the same pots twice.
    /// </summary>
    public async Task UpdateBoughtAsync(ShoppingItem item, BoughtDetails details)
    {
        var firstAmount = item.Amount is null;
        var saved = item.Copy();
        saved.Amount = details.Amount;
        saved.Price = details.Price;
        saved.SellerId = details.SellerId;
        saved.BoughtAt = MomentOn(details.Day, item.BoughtAt ?? time.GetLocalNow());
        await items.SaveAsync(saved);

        if (details.RaisePot && firstAmount && details.Amount is > 0 && item.PotId is { } potId
            && await pots.GetAsync(potId) is { } pot)
        {
            pot.Owned += (int)Math.Floor(details.Amount.Value);
            await pots.SaveAsync(pot);
        }
    }

    public Task DeleteLineAsync(Guid itemId) => items.DeleteAsync(itemId);

    /// <summary>The day picked, at the time of day the entry already had, or now when it is today.</summary>
    private DateTimeOffset MomentOn(DateOnly day, DateTimeOffset current) =>
        time.LocalDay(current) == day ? current : time.MomentOn(day);
}
