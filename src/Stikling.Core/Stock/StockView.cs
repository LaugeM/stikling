using Stikling.Core.Models;

namespace Stikling.Core.Stock;

/// <summary>How much of a supply there is, as far as the entries say.</summary>
public enum SupplyLevel
{
    Have,
    Low,
    Out
}

/// <summary>The latest time a supply was bought.</summary>
public sealed record LastBought(Guid EntryId, DateOnly Day, decimal? Amount, decimal? Price, Guid? SellerId);

/// <summary>A supply with its level and amount worked out from its entries.</summary>
/// <param name="Name">The product's name while the product exists, otherwise the supply's own.</param>
/// <param name="Amount">Only known after a count: the counted amount plus what was bought since. Otherwise null.</param>
/// <param name="CountedOn">The day of the count the amount is based on.</param>
public sealed record SupplyStatus(
    Supply Supply,
    string Name,
    SupplyLevel Level,
    decimal? Amount,
    DateOnly? CountedOn,
    LastBought? LastBought)
{
    public SupplyKind Kind => Supply.EffectiveKind;
}

/// <summary>
/// A line on the shopping list. A supply that is low or out has <see cref="Supply"/> set, a loose line
/// has <see cref="Item"/>. In the bought part, <see cref="Entry"/> is the Bought entry that ticking added.
/// </summary>
/// <param name="LastSellerId">Where the supply was last bought, when the last Bought entry says.</param>
public sealed record ShoppingRow(
    string Name,
    SupplyStatus? Supply,
    ShoppingItem? Item,
    SupplyEntry? Entry,
    Guid? LastSellerId)
{
    public SupplyLevel? Level => Supply?.Level;
}

/// <summary>
/// The stock and the shopping list, worked out from the records. Pure, so it is tested without
/// repositories. Whatever the merge of two devices can produce is handled here: entries of a supply
/// that is deleted or missing are ignored, a pot line whose pot is gone shows its own text, and the
/// entries can arrive in any order.
/// </summary>
public sealed class StockView
{
    public IReadOnlyList<SupplyStatus> Supplies { get; private init; } = [];

    /// <summary>What there is to buy: supplies that are out, then low, then loose lines, newest first.</summary>
    public IReadOnlyList<ShoppingRow> ToBuy { get; private init; } = [];

    /// <summary>What was ticked off today, newest first.</summary>
    public IReadOnlyList<ShoppingRow> BoughtToday { get; private init; } = [];

    /// <summary>Supplies that are low or out.</summary>
    public int RunningLow => Supplies.Count(s => s.Level != SupplyLevel.Have);

    public SupplyStatus? ForProduct(Guid productId) => Supplies.FirstOrDefault(s => s.Supply.ProductId == productId);

    public SupplyStatus? Find(Guid supplyId) => Supplies.FirstOrDefault(s => s.Supply.Id == supplyId);

    /// <summary>The pot line that still has to be bought, if the pot has one.</summary>
    public ShoppingItem? OpenLineForPot(Guid potId) => ToBuy.Select(r => r.Item).FirstOrDefault(i => i?.PotId == potId);

    /// <param name="dayOf">The day a moment falls on where the device is.</param>
    public static StockView Build(
        IEnumerable<Supply> supplies,
        IEnumerable<SupplyEntry> entries,
        IEnumerable<ShoppingItem> items,
        IEnumerable<Product> products,
        IEnumerable<Pot> pots,
        Func<DateTimeOffset, DateOnly> dayOf,
        DateOnly today)
    {
        var liveProducts = products.Where(p => !p.IsDeleted).GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        var livePots = pots.Where(p => !p.IsDeleted).GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        var liveSupplies = supplies.Where(s => !s.IsDeleted).GroupBy(s => s.Id).Select(g => g.First()).ToList();
        var entriesBySupply = entries.Where(e => !e.IsDeleted).ToLookup(e => e.SupplyId);

        var statuses = liveSupplies
            .Select(s => StatusOf(s, entriesBySupply[s.Id], liveProducts, dayOf))
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(s => s.Supply.Id)
            .ToList();
        var byId = statuses.ToDictionary(s => s.Supply.Id);

        var toBuy = statuses
            .Where(s => s.Level != SupplyLevel.Have)
            .OrderBy(s => s.Level == SupplyLevel.Out ? 0 : 1)
            .Select(s => new ShoppingRow(s.Name, s, null, null, s.LastBought?.SellerId))
            .ToList();

        var liveItems = items.Where(i => !i.IsDeleted).ToList();
        toBuy.AddRange(liveItems
            .Where(i => i.BoughtAt is null)
            .OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id)
            .Select(i => new ShoppingRow(ItemName(i, livePots), null, i, null, null)));

        var bought = new List<(DateTimeOffset At, ShoppingRow Row)>();
        foreach (var entry in entriesBySupply.SelectMany(g => g))
        {
            if (entry.Kind == SupplyEntryKind.Bought && byId.TryGetValue(entry.SupplyId, out var status) && dayOf(entry.At) == today)
                bought.Add((entry.At, new ShoppingRow(status.Name, status, null, entry, null)));
        }
        foreach (var item in liveItems)
        {
            if (item.BoughtAt is { } at && dayOf(at) == today)
                bought.Add((at, new ShoppingRow(ItemName(item, livePots), null, item, null, null)));
        }

        return new StockView
        {
            Supplies = statuses,
            ToBuy = toBuy,
            BoughtToday = [.. bought.OrderByDescending(b => b.At).Select(b => b.Row)]
        };
    }

    private static string ItemName(ShoppingItem item, Dictionary<Guid, Pot> pots) =>
        item.PotId is { } potId && pots.TryGetValue(potId, out var pot) ? pot.DisplayName : item.Text;

    private static SupplyStatus StatusOf(
        Supply supply, IEnumerable<SupplyEntry> entries, Dictionary<Guid, Product> products, Func<DateTimeOffset, DateOnly> dayOf)
    {
        var name = supply.ProductId is { } productId && products.TryGetValue(productId, out var product)
                   && !string.IsNullOrWhiteSpace(product.Name)
            ? product.Name.Trim()
            : supply.Name?.Trim() ?? "";

        // The order things happened in: by moment, then by when they were saved, then by id so every device agrees.
        // Entries with a kind this version doesn't know say nothing about the level.
        var known = entries
            .Where(e => e.Kind is not null)
            .OrderBy(e => e.At).ThenBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .ToList();

        // The level is what was said last, not what happened last: a purchase entered today but dated
        // yesterday still means it's back after this morning's Out.
        var level = SupplyLevel.Have;
        if (known.Count > 0)
        {
            var latest = known.MaxBy(e => (e.CreatedAt, e.At, e.Id))!;
            level = latest.Kind switch
            {
                SupplyEntryKind.RunningLow => SupplyLevel.Low,
                SupplyEntryKind.Out => SupplyLevel.Out,
                SupplyEntryKind.Counted when latest.Amount == 0 => SupplyLevel.Out,
                _ => SupplyLevel.Have
            };
        }

        decimal? amount = null;
        DateOnly? countedOn = null;
        // Only the latest count or Out says what is there. "Have it" is a count without an amount and Out
        // empties the shelf, so after either one an older counted amount no longer holds.
        var countIndex = known.FindLastIndex(e => e.Kind is SupplyEntryKind.Counted or SupplyEntryKind.Out);
        if (countIndex >= 0 && known[countIndex] is { Kind: SupplyEntryKind.Counted, Amount: not null } count)
        {
            amount = count.Amount + known.Skip(countIndex + 1).Where(e => e.Kind == SupplyEntryKind.Bought).Sum(e => e.Amount ?? 0);
            countedOn = dayOf(count.At);
        }

        LastBought? last = null;
        if (known.LastOrDefault(e => e.Kind == SupplyEntryKind.Bought) is { } purchase)
            last = new LastBought(purchase.Id, dayOf(purchase.At), purchase.Amount, purchase.Price, purchase.SellerId);

        return new SupplyStatus(supply, name, level, amount, countedOn, last);
    }
}
