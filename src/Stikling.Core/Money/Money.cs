using Stikling.Core.Models;

namespace Stikling.Core.Spending;

/// <summary>The parts of a year's money. Everything but <see cref="BroughtIn"/> is spent.</summary>
public enum MoneyPart
{
    Plants,
    Shipping,
    Supplies,
    Other,
    BroughtIn
}

/// <summary>What a line links to.</summary>
public enum MoneyLink
{
    Plant,
    Propagation,
    Order,
    Supply,
    ShoppingList
}

/// <param name="Name">What it is. Null for a shipping line whose order has no seller name, which the page words itself.</param>
/// <param name="LinkId">The plant, propagation, order or supply the line is from. Null for the shopping list.</param>
public sealed record MoneyLine(DateOnly Date, string? Name, decimal Amount, MoneyPart Part, MoneyLink Link, Guid? LinkId);

/// <summary>One calendar year of money: every line behind the numbers.</summary>
public sealed record MoneyYear(int Year, IReadOnlyList<MoneyLine> Lines)
{
    public IReadOnlyList<MoneyLine> Of(MoneyPart part) => [.. Lines.Where(l => l.Part == part)];

    public decimal Plants => Sum(MoneyPart.Plants);
    public decimal Shipping => Sum(MoneyPart.Shipping);
    public decimal Supplies => Sum(MoneyPart.Supplies);
    public decimal Other => Sum(MoneyPart.Other);
    public decimal BroughtIn => Sum(MoneyPart.BroughtIn);

    public decimal Spent => Plants + Shipping + Supplies + Other;

    private decimal Sum(MoneyPart part) => Lines.Where(l => l.Part == part).Sum(l => l.Amount);
}

/// <summary>
/// What the hobby costs and brings in, per calendar year, added up from the prices people typed. Amounts are
/// plain numbers with no currency, and only what has a price is counted. Deleted records are left out.
/// </summary>
public static class Money
{
    /// <summary>
    /// Years newest first, only the ones with a line.
    /// </summary>
    /// <param name="dayOf">The day a moment falls on where the device is.</param>
    /// <param name="orderName">The name to show for an order, usually its seller. Null when it has none.</param>
    /// <param name="supplyName">The name of a supply, as the stock shows it.</param>
    /// <param name="potName">The name of a pot, for a shopping line that is a pot.</param>
    public static IReadOnlyList<MoneyYear> ByYear(
        IEnumerable<Plant> plants,
        IEnumerable<Order> orders,
        IEnumerable<OrderItem> orderItems,
        IEnumerable<SupplyEntry> supplyEntries,
        IEnumerable<ShoppingItem> shoppingItems,
        IEnumerable<TimelineEntry> entries,
        IEnumerable<Propagation> propagations,
        Func<DateTimeOffset, DateOnly> dayOf,
        Func<Order, string?>? orderName = null,
        Func<Guid, string?>? supplyName = null,
        Func<Guid, string?>? potName = null)
    {
        var lines = new List<MoneyLine>();
        var livePlants = plants.Where(p => !p.IsDeleted).ToList();

        // Plants: what was paid, dated by when it was got. Plants that live with someone else aren't yours.
        foreach (var plant in livePlants.Where(p => !p.LivesElsewhere && p.PricePaid > 0))
            lines.Add(new(plant.AcquiredOn?.Start ?? dayOf(plant.CreatedAt), plant.DisplayName, plant.PricePaid!.Value,
                MoneyPart.Plants, MoneyLink.Plant, plant.Id));

        var liveOrders = orders.Where(o => !o.IsDeleted).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());

        // Order items that never became a plant or came at all. Once it is a plant its own price counts.
        foreach (var item in orderItems.Where(i => !i.IsDeleted && i.PlantId is null && !i.DidNotCome && i.Price > 0))
        {
            if (!liveOrders.TryGetValue(item.OrderId, out var order))
                continue;
            // One that became a propagation is plants too
            lines.Add(new(order.OrderedOn, item.DisplayName, item.Price!.Value, MoneyPart.Plants, MoneyLink.Order, order.Id));
        }

        foreach (var order in liveOrders.Values.Where(o => o.Shipping > 0))
            lines.Add(new(order.OrderedOn, orderName?.Invoke(order), order.Shipping!.Value, MoneyPart.Shipping, MoneyLink.Order, order.Id));

        foreach (var entry in supplyEntries.Where(e => !e.IsDeleted && e.Kind == SupplyEntryKind.Bought && e.Price > 0))
            lines.Add(new(dayOf(entry.At), supplyName?.Invoke(entry.SupplyId), entry.Price!.Value, MoneyPart.Supplies, MoneyLink.Supply, entry.SupplyId));

        // Supplies have no shopping line of their own, so these are loose lines and pots
        foreach (var item in shoppingItems.Where(i => !i.IsDeleted && i.BoughtAt is not null && i.Price > 0))
            lines.Add(new(dayOf(item.BoughtAt!.Value),
                item.PotId is { } potId && potName?.Invoke(potId) is { } pot ? pot : item.Text,
                item.Price!.Value, MoneyPart.Other, MoneyLink.ShoppingList, null));

        // Brought in: plants sold, on the day they left
        var liveEntries = entries.Where(e => !e.IsDeleted).ToList();
        var soldOn = liveEntries
            .Where(e => e.SubjectType == SubjectType.Plant && e.Text is not null
                && e.Text.Split('\n').Any(line => line.StartsWith("Status: Sold", StringComparison.Ordinal)))
            .GroupBy(e => e.SubjectId)
            .ToDictionary(g => g.Key, g => g.Max(e => e.OccurredAt));
        foreach (var plant in livePlants.Where(p => p.Status == PlantStatus.Sold && p.SoldPrice > 0))
        {
            var day = plant.StatusSince
                ?? (soldOn.TryGetValue(plant.Id, out var at) ? dayOf(at) : dayOf(plant.UpdatedAt));
            lines.Add(new(day, plant.DisplayName, plant.SoldPrice!.Value, MoneyPart.BroughtIn, MoneyLink.Plant, plant.Id));
        }

        // ...and cuttings sold
        var liveCuttings = propagations.Where(p => !p.IsDeleted).GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var entry in liveEntries.Where(e => e.Event == TimelineEvent.GaveAway && e.SubjectType == SubjectType.Propagation && e.Price > 0))
        {
            if (!liveCuttings.TryGetValue(entry.SubjectId, out var cutting))
                continue;
            lines.Add(new(dayOf(entry.OccurredAt), cutting.DisplayName, entry.Price!.Value, MoneyPart.BroughtIn, MoneyLink.Propagation, cutting.Id));
        }

        return [.. lines
            .GroupBy(l => l.Date.Year)
            .OrderByDescending(g => g.Key)
            .Select(g => new MoneyYear(g.Key, [.. g
                .OrderByDescending(l => l.Date)
                .ThenByDescending(l => l.Amount)
                .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.LinkId)]))];
    }
}
