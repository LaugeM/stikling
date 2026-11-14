using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Spending;

namespace Stikling.Core.Tests;

public class MoneyTests
{
    private static DateOnly Day(DateTimeOffset moment) => DateOnly.FromDateTime(moment.DateTime);

    private static IReadOnlyList<MoneyYear> Of(
        IEnumerable<Plant>? plants = null,
        IEnumerable<Order>? orders = null,
        IEnumerable<OrderItem>? items = null,
        IEnumerable<SupplyEntry>? supply = null,
        IEnumerable<ShoppingItem>? shopping = null,
        IEnumerable<TimelineEntry>? entries = null,
        IEnumerable<Propagation>? propagations = null) =>
        Money.ByYear(plants ?? [], orders ?? [], items ?? [], supply ?? [], shopping ?? [], entries ?? [], propagations ?? [], Day);

    private static MoneyYear Year(IReadOnlyList<MoneyYear> years, int year) => years.Single(y => y.Year == year);

    [Fact]
    public void A_plant_is_counted_by_the_day_it_was_got_whatever_its_status_now()
    {
        var bought = new Plant { Nickname = "Monstera", PricePaid = 150, AcquiredOn = LooseDate.Of(2025, 3), Status = PlantStatus.Died };
        var sold = new Plant { Nickname = "Basil", PricePaid = 20, AcquiredOn = LooseDate.Of(2026) };

        var years = Of(plants: [bought, sold]);

        Assert.Equal([2026, 2025], years.Select(y => y.Year));
        Assert.Equal(150, Year(years, 2025).Plants);
        Assert.Equal(20, Year(years, 2026).Spent);
    }

    [Fact]
    public void A_plant_without_a_date_is_dated_by_when_it_was_added()
    {
        var plant = new Plant { Nickname = "Pothos", PricePaid = 30, CreatedAt = new DateTimeOffset(2024, 12, 31, 10, 0, 0, TimeSpan.Zero) };

        var years = Of(plants: [plant]);

        Assert.Equal(30, Year(years, 2024).Plants);
    }

    [Fact]
    public void Deleted_plants_plants_of_others_and_plants_without_a_price_are_left_out()
    {
        var at = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var gone = new Plant { PricePaid = 10, CreatedAt = at, DeletedAt = at };
        var notYours = new Plant { PricePaid = 10, CreatedAt = at, LivesElsewhere = true };
        var free = new Plant { CreatedAt = at };

        Assert.Empty(Of(plants: [gone, notYours, free]));
    }

    [Fact]
    public void An_order_item_that_became_a_plant_is_counted_once_through_the_plant()
    {
        var order = new Order { OrderedOn = new DateOnly(2026, 2, 10), Shipping = 5 };
        var plant = new Plant { Nickname = "Hoya", PricePaid = 40, AcquiredOn = LooseDate.Of(new DateOnly(2026, 2, 14)) };
        var item = new OrderItem { OrderId = order.Id, Name = "Hoya", Price = 40, PlantId = plant.Id };

        var year = Year(Of(plants: [plant], orders: [order], items: [item]), 2026);

        Assert.Equal(40, year.Plants);
        Assert.Equal(5, year.Shipping);
        Assert.Equal(45, year.Spent);
    }

    [Fact]
    public void An_item_still_on_the_way_or_become_a_propagation_counts_under_plants_by_the_order_day()
    {
        var order = new Order { OrderedOn = new DateOnly(2025, 12, 30) };
        var onTheWay = new OrderItem { OrderId = order.Id, Name = "Philodendron", Price = 25 };
        var cutting = new OrderItem { OrderId = order.Id, Name = "Cutting", Price = 15, PropagationId = Guid.NewGuid() };

        var year = Year(Of(orders: [order], items: [onTheWay, cutting]), 2025);

        Assert.Equal(40, year.Plants);
        Assert.All(year.Of(MoneyPart.Plants), l => Assert.Equal(MoneyLink.Order, l.Link));
    }

    [Fact]
    public void An_item_that_did_not_come_and_items_of_deleted_orders_are_left_out()
    {
        var order = new Order { OrderedOn = new DateOnly(2026, 1, 5) };
        var deletedOrder = new Order { OrderedOn = new DateOnly(2026, 1, 5), DeletedAt = DateTimeOffset.UnixEpoch, Shipping = 9 };
        var missing = new OrderItem { OrderId = order.Id, Name = "Missing", Price = 25, DidNotCome = true };
        var orphan = new OrderItem { OrderId = deletedOrder.Id, Name = "Orphan", Price = 12 };

        Assert.Empty(Of(orders: [order, deletedOrder], items: [missing, orphan]));
    }

    [Fact]
    public void Supplies_and_loose_shopping_lines_are_counted_on_their_own_day()
    {
        var supplyId = Guid.NewGuid();
        var bought = new SupplyEntry { SupplyId = supplyId, Kind = SupplyEntryKind.Bought, Price = 60, At = new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.Zero) };
        var counted = new SupplyEntry { SupplyId = supplyId, Kind = SupplyEntryKind.Counted, Price = 5, At = bought.At };
        var deletedEntry = new SupplyEntry { SupplyId = supplyId, Kind = SupplyEntryKind.Bought, Price = 7, At = bought.At, DeletedAt = bought.At };
        var line = new ShoppingItem { Text = "Sticky traps", Price = 30, BoughtAt = new DateTimeOffset(2025, 11, 1, 9, 0, 0, TimeSpan.Zero) };
        var notBought = new ShoppingItem { Text = "Perlite", Price = 99 };

        var years = Of(supply: [bought, counted, deletedEntry], shopping: [line, notBought]);

        Assert.Equal(60, Year(years, 2026).Supplies);
        Assert.Equal(30, Year(years, 2025).Other);
        Assert.Equal(MoneyLink.Supply, Year(years, 2026).Lines.Single().Link);
    }

    [Fact]
    public void A_plant_sold_brings_in_its_price_on_the_day_it_left()
    {
        var sold = new Plant { Nickname = "Basil", Status = PlantStatus.Sold, SoldPrice = 80, StatusSince = new DateOnly(2026, 6, 1), LeftFor = "80 kr" };
        var wasSold = new Plant { Nickname = "Back", Status = PlantStatus.Active, SoldPrice = 500 };

        var years = Of(plants: [sold, wasSold]);

        var year = Year(years, 2026);
        Assert.Equal(80, year.BroughtIn);
        Assert.Equal(0, year.Spent);
    }

    [Fact]
    public void A_sold_plant_without_a_day_uses_the_history_entry_of_the_sale()
    {
        var sold = new Plant { Nickname = "Basil", Status = PlantStatus.Sold, SoldPrice = 80, UpdatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) };
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant, SubjectId = sold.Id, Kind = TimelineKind.Change,
            OccurredAt = new DateTimeOffset(2025, 12, 24, 12, 0, 0, TimeSpan.Zero), Text = "Status: Sold (was Active)\nSold for 80 kr"
        };

        Assert.Equal(80, Year(Of(plants: [sold], entries: [entry]), 2025).BroughtIn);
    }

    [Fact]
    public void Sold_cuttings_bring_in_their_price_but_given_ones_and_deleted_ones_do_not()
    {
        var cutting = new Propagation { Nickname = "Pothos" };
        var at = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        TimelineEntry Entry(decimal? price, DateTimeOffset? deleted = null) => new()
        {
            SubjectType = SubjectType.Propagation, SubjectId = cutting.Id, Kind = TimelineKind.Change,
            Event = TimelineEvent.GaveAway, OccurredAt = at, Price = price, DeletedAt = deleted
        };

        var year = Year(Of(entries: [Entry(45), Entry(null), Entry(10, at)], propagations: [cutting]), 2026);

        Assert.Equal(45, year.BroughtIn);
        Assert.Equal(MoneyLink.Propagation, year.Lines.Single().Link);
    }

    [Fact]
    public void Years_are_split_on_the_calendar_year_newest_first()
    {
        var a = new Plant { PricePaid = 1, AcquiredOn = LooseDate.Of(new DateOnly(2025, 12, 31)) };
        var b = new Plant { PricePaid = 2, AcquiredOn = LooseDate.Of(new DateOnly(2026, 1, 1)) };

        var years = Of(plants: [a, b]);

        Assert.Equal([2026, 2025], years.Select(y => y.Year));
        Assert.Equal(2, years[0].Spent);
        Assert.Equal(1, years[1].Spent);
    }

    [Fact]
    public void Old_json_without_the_new_fields_reads_them_as_null()
    {
        var plant = JsonSerializer.Deserialize<Plant>("{\"Status\":\"Sold\",\"LeftFor\":\"150 kr\"}")!;
        var entry = JsonSerializer.Deserialize<TimelineEntry>("{\"Count\":2}")!;

        Assert.Null(plant.SoldPrice);
        Assert.Null(entry.Price);
        Assert.Equal(2, entry.Count);
    }

    [Fact]
    public void A_negative_sold_price_is_not_valid()
    {
        var plant = new Plant { Nickname = "Basil", SoldPrice = -1 };

        Assert.Contains(plant.Validate(new DateOnly(2026, 1, 1)), e => e.Contains("sold for"));
    }

    private static readonly Plant Mother = new() { Nickname = "Mother", PricePaid = 150 };

    [Fact]
    public void A_plant_given_has_a_cost_and_what_its_offspring_and_sold_cuttings_brought_in()
    {
        var child = new Plant { Nickname = "Child", ParentPlantId = Mother.Id, Status = PlantStatus.Sold, SoldPrice = 100 };
        var givenAway = new Plant { Nickname = "Gift", ParentPlantId = Mother.Id, Status = PlantStatus.GivenAway, SoldPrice = 999 };
        var cutting = new Propagation { ParentPlantId = Mother.Id, InitialCount = 5 };
        var other = new Propagation { ParentPlantId = Guid.NewGuid(), InitialCount = 5 };
        TimelineEntry Entry(Propagation of, decimal? price) => new()
        {
            SubjectType = SubjectType.Propagation, SubjectId = of.Id, Event = TimelineEvent.GaveAway, Price = price
        };

        var given = PlantLineage.Given(Mother, [Mother, child, givenAway], [cutting, other],
            [Entry(cutting, 220), Entry(cutting, null), Entry(other, 50)]);

        Assert.Equal(150, given.Cost);
        Assert.Equal(320, given.BroughtIn);
        Assert.True(given.HasMoney);
    }

    [Fact]
    public void A_plant_given_without_prices_has_no_money_and_money_alone_does_not_fill_the_story()
    {
        var free = new Plant { Nickname = "Free" };
        Assert.False(PlantLineage.Given(free, [free], []).HasMoney);

        var costly = PlantLineage.Given(Mother, [Mother], []);
        Assert.True(costly.HasMoney);
        Assert.True(costly.IsEmpty);
        Assert.Null(costly.BroughtIn);
    }
}
