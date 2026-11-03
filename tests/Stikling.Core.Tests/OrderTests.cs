using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Orders;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Today;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly FixedTime Time = new(Now);

    private readonly FakeOrderRepository orders = new();
    private readonly FakeOrderItemRepository items = new();
    private readonly FakeSellerRepository sellers = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakeWishRepository wishes = new();
    private readonly OrderService service;

    public OrderTests()
    {
        var plantService = new PlantService(plants, timeline, Time);
        var propagationService = new PropagationService(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), Time);
        service = new OrderService(orders, items, sellers, plants, propagations, plantService, propagationService, wishes, new WishService(wishes, new FakePhotoRepository(), Time));
    }

    private static string Label(Enum value) => value.ToString();

    private async Task<Seller> SellerAsync(string name = "Plantorama")
    {
        var seller = new Seller { Name = name };
        await sellers.SaveAsync(seller);
        return seller;
    }

    // An order with the given items, saved
    private async Task<(Order Order, List<OrderItem> Items)> OrderAsync(Seller? seller, DateOnly? expected, decimal? shipping, params OrderItem[] list)
    {
        var order = new Order
        {
            SellerId = seller?.Id,
            SellerName = seller?.Name ?? "Plantorama",
            OrderedOn = Day.AddDays(-3),
            ExpectedOn = expected,
            Shipping = shipping
        };
        await service.SaveAsync(order, list);
        return (order, [.. list]);
    }

    private static OrderItem Item(string name, decimal? price = null, string? genus = null) =>
        new() { Name = name, Genus = genus, Price = price };

    private async Task<OrderView> ViewAsync(Order order) => (await service.GetAsync(order.Id))!;

    [Fact]
    public void An_order_needs_a_seller_and_sane_numbers()
    {
        Assert.NotEmpty(new Order { OrderedOn = Day }.Validate(Day));
        Assert.Empty(new Order { SellerName = "Plantorama", OrderedOn = Day }.Validate(Day));
        Assert.Empty(new Order { SellerId = Guid.NewGuid(), OrderedOn = Day }.Validate(Day));
        Assert.NotEmpty(new Order { SellerName = "x", OrderedOn = Day, Shipping = -1 }.Validate(Day));
        Assert.NotEmpty(new Order { SellerName = "x", OrderedOn = Day, ExpectedOn = Day.AddDays(-1) }.Validate(Day));
        Assert.Empty(new Order { SellerName = "x", OrderedOn = Day, ExpectedOn = Day }.Validate(Day));
    }

    [Fact]
    public void An_item_needs_a_name_or_a_genus_and_no_negative_price()
    {
        Assert.NotEmpty(new OrderItem().Validate());
        Assert.Empty(new OrderItem { Genus = "Monstera" }.Validate());
        Assert.Empty(new OrderItem { Name = "pink one" }.Validate());
        Assert.NotEmpty(new OrderItem { Name = "x", Price = -1 }.Validate());
        Assert.Equal("Monstera deliciosa", new OrderItem { Name = "x", Genus = "monstera", Species = "deliciosa" }.DisplayName);
        Assert.Equal("pink one", new OrderItem { Name = " pink one " }.DisplayName);
    }

    [Fact]
    public async Task Unpacking_as_a_plant_fills_it_in_from_the_order()
    {
        var seller = await SellerAsync();
        var (order, list) = await OrderAsync(seller, Day, 10m, Item("Thai", 25m, genus: "Monstera"));
        list[0].Species = "deliciosa";
        list[0].Cultivar = "Thai Constellation";
        await service.SaveAsync(order, list);
        var place = Guid.NewGuid();

        var result = await service.UnpackAsync(order.Id, Day, place,
            [new UnpackChoice { ItemId = list[0].Id, Rating = ArrivalRating.Ok, Problems = [ArrivalProblem.Pests], ArrivalNote = " one leaf torn " }], Label);

        var plant = plants.Plants[Assert.Single(result.PlantIds)];
        Assert.Equal(("Monstera", "deliciosa", "Thai Constellation"), (plant.Genus, plant.Species, plant.Cultivar));
        Assert.Null(plant.Nickname);
        Assert.Equal(PlantOrigin.Purchased, plant.Origin);
        Assert.Equal(("Plantorama", seller.Id, 25m), (plant.Source, plant.SellerId, plant.PricePaid));
        Assert.Equal(LooseDate.Of(Day), plant.AcquiredOn);
        Assert.Equal((ArrivalRating.Ok, "one leaf torn", place), (plant.ArrivedIn, plant.ArrivalNote, plant.PlaceId));
        Assert.Equal([ArrivalProblem.Pests], plant.ArrivalProblems);
        Assert.Equal(PlantStatus.Active, plant.Status);
        Assert.Contains(timeline.Entries, e => e.SubjectId == plant.Id && e.Kind == TimelineKind.Created);

        var item = items.Items[list[0].Id];
        Assert.Equal((plant.Id, Day), (item.PlantId, item.ArrivedOn));
        Assert.Null(item.PropagationId);
        Assert.False(item.IsOnTheWay);
    }

    [Fact]
    public async Task Unpacking_as_a_cutting_makes_a_propagation_and_keeps_the_price_on_the_item()
    {
        var seller = await SellerAsync();
        var (order, list) = await OrderAsync(seller, null, null, Item("Variegated cutting", 12m));
        var place = Guid.NewGuid();

        var result = await service.UnpackAsync(order.Id, Day, place,
            [new UnpackChoice { ItemId = list[0].Id, UnpackAs = UnpackAs.Cutting, Rating = ArrivalRating.Poor, ArrivalNote = "Dry" }], Label);

        var cutting = propagations.Propagations[Assert.Single(result.PropagationIds)];
        Assert.Empty(result.PlantIds);
        Assert.Equal("Variegated cutting", cutting.Nickname);
        Assert.Equal(("Plantorama", seller.Id), (cutting.Source, cutting.SellerId));
        Assert.Equal((ArrivalRating.Poor, "Dry", place), (cutting.ArrivedIn, cutting.ArrivalNote, cutting.PlaceId));
        Assert.Equal((PropagationType.Cutting, Day), (cutting.Type, cutting.StartedOn));
        Assert.Empty(plants.Plants);

        var item = items.Items[list[0].Id];
        Assert.Equal((cutting.Id, 12m, Day), (item.PropagationId, item.Price, item.ArrivedOn));
        Assert.Null(item.PlantId);
    }

    [Fact]
    public async Task A_free_text_name_becomes_the_nickname_when_there_is_no_genus()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("that pink one"));

        var result = await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);

        var plant = plants.Plants[Assert.Single(result.PlantIds)];
        Assert.Equal("that pink one", plant.Nickname);
        Assert.Null(plant.Genus);
        Assert.Null(plant.SellerId);
        Assert.Equal("Plantorama", plant.Source);
    }

    [Fact]
    public async Task A_tidied_name_wins_over_the_one_on_the_item_and_is_kept_on_it()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("monstera thai"));

        var result = await service.UnpackAsync(order.Id, Day, null,
            [new UnpackChoice { ItemId = list[0].Id, Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation" }], Label);

        var plant = plants.Plants[Assert.Single(result.PlantIds)];
        Assert.Equal(("Monstera", "deliciosa", "Thai Constellation", null), (plant.Genus, plant.Species, plant.Cultivar, plant.Nickname));
        var item = items.Items[list[0].Id];
        Assert.Equal(("Monstera", null), (item.Genus, item.Name));
    }

    [Fact]
    public async Task Items_not_chosen_stay_on_the_way_and_an_unpacked_one_is_not_made_again()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"), Item("B"), Item("C"));

        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[1].Id }], Label);

        var view = await ViewAsync(order);
        Assert.True(view.IsOnTheWay);
        Assert.Null(view.ArrivedOn);
        Assert.Equal([true, false, true], view.Items.Select(i => i.IsOnTheWay));
        Assert.Equal(1, view.CameCount);

        var again = await service.UnpackAsync(order.Id, Day.AddDays(1), null, [new UnpackChoice { ItemId = list[1].Id }], Label);
        Assert.Empty(again.PlantIds);
        Assert.Single(plants.Plants);
    }

    [Fact]
    public async Task Unpacking_the_same_item_after_a_failed_run_does_not_make_a_second_plant()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"));
        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);
        var item = items.Items[list[0].Id];
        item.PlantId = null;
        item.ArrivedOn = null;

        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);

        Assert.Single(plants.Plants);
        Assert.Equal(plants.Plants.Keys.Single(), items.Items[list[0].Id].PlantId);
    }

    [Fact]
    public async Task A_wish_item_marks_the_wish_got_with_the_new_plant()
    {
        var wish = new Wish { Genus = "Monstera", Species = "deliciosa" };
        await wishes.SaveAsync(wish);
        var (order, _) = await OrderAsync(null, null, null);
        var item = await service.AddWishAsync(order.Id, wish.Id);

        var result = await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = item!.Id }], Label);

        Assert.True(wishes.Wishes[wish.Id].IsGot);
        Assert.Equal(Assert.Single(result.PlantIds), wishes.Wishes[wish.Id].PlantId);
        Assert.Equal(("Monstera", "deliciosa", null), (item.Genus, item.Species, item.Price));
    }

    [Fact]
    public async Task A_wish_on_an_order_on_the_way_says_so_until_the_item_did_not_come_or_the_order_is_deleted()
    {
        var seller = await SellerAsync();
        var wish = new Wish { Name = "pink one" };
        var other = new Wish { Name = "blue one" };
        await wishes.SaveAsync(wish);
        await wishes.SaveAsync(other);
        var (order, _) = await OrderAsync(seller, null, null);
        var item = await service.AddWishAsync(order.Id, wish.Id);

        var found = await service.WishesOnTheWayAsync();
        Assert.Equal(new WishOnOrder(order.Id, "Plantorama"), found[wish.Id]);
        Assert.False(found.ContainsKey(other.Id));

        await service.SetDidNotComeAsync(item!.Id, true);
        Assert.Empty(await service.WishesOnTheWayAsync());

        await service.SetDidNotComeAsync(item.Id, false);
        Assert.Single(await service.WishesOnTheWayAsync());

        await service.DeleteAsync(order.Id);
        Assert.Empty(await service.WishesOnTheWayAsync());
        Assert.False(wishes.Wishes[wish.Id].IsGot);
    }

    [Fact]
    public async Task Items_that_did_not_come_are_left_out_of_the_totals_and_the_shipping_split()
    {
        var (order, list) = await OrderAsync(null, null, 9m, Item("A", 10m), Item("B", 20m), Item("C", 30m));
        await service.SetDidNotComeAsync(list[2].Id, true);

        var view = await ViewAsync(order);

        Assert.Equal(3, view.ItemCount);
        Assert.Equal(30m, view.ItemsTotal);
        Assert.Equal(39m, view.Total);
        Assert.Equal(4.5m, view.ShippingEach);

        await service.SetDidNotComeAsync(list[2].Id, false);
        Assert.Equal(3m, (await ViewAsync(order)).ShippingEach);
    }

    [Fact]
    public async Task Shipping_each_is_null_without_shipping_or_without_items_that_came()
    {
        var (none, _) = await OrderAsync(null, null, null, Item("A", 10m));
        Assert.Null((await ViewAsync(none)).ShippingEach);

        var (all, list) = await OrderAsync(null, null, 5m, Item("A", 10m));
        await service.SetDidNotComeAsync(list[0].Id, true);
        var view = await ViewAsync(all);
        Assert.Null(view.ShippingEach);
        Assert.Equal(5m, view.Total);
    }

    [Fact]
    public async Task A_plant_item_counts_the_price_of_its_plant_and_changing_it_on_the_order_changes_the_plant()
    {
        var (order, list) = await OrderAsync(null, null, 4m, Item("A", 10m));
        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);
        var plant = plants.Plants.Values.Single();
        Assert.Equal(10m, plant.PricePaid);

        // The plant's price is the truth once it is a plant
        plant.PricePaid = 12m;
        var view = await ViewAsync(order);
        Assert.Equal(12m, view.ItemsTotal);
        Assert.Equal(16m, view.Total);
        Assert.Equal(12m, view.PriceOf(view.Items[0]));

        // Saving the order without touching the price leaves the plant's price alone
        await service.SaveAsync(order, view.Items);
        Assert.Equal(12m, plant.PricePaid);

        // The page edits its own copy, the way a stored record comes back from the database
        var edited = JsonSerializer.Deserialize<OrderItem>(JsonSerializer.Serialize(view.Items[0]))!;
        edited.Price = 15m;
        await service.SaveAsync(order, [edited]);
        Assert.Equal(15m, plant.PricePaid);
        Assert.Equal(15m, (await ViewAsync(order)).ItemsTotal);
    }

    [Fact]
    public async Task A_cutting_item_counts_its_own_price()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A", 7m));
        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id, UnpackAs = UnpackAs.Cutting }], Label);

        Assert.Equal(7m, (await ViewAsync(order)).ItemsTotal);
    }

    [Fact]
    public async Task Adding_an_existing_plant_gives_it_the_seller_only_when_it_has_none()
    {
        var seller = await SellerAsync();
        var (order, _) = await OrderAsync(seller, null, null);
        var bare = new Plant { Genus = "Hoya", PricePaid = 18m };
        var withSource = new Plant { Genus = "Ficus", Source = "A friend" };
        var withSeller = new Plant { Genus = "Alocasia", SellerId = Guid.NewGuid() };
        foreach (var p in new[] { bare, withSource, withSeller })
            plants.Plants[p.Id] = p;

        var item = await service.AddPlantAsync(order.Id, bare.Id);
        await service.AddPlantAsync(order.Id, withSource.Id);
        await service.AddPlantAsync(order.Id, withSeller.Id);
        await service.AddPlantAsync(order.Id, bare.Id);

        Assert.Equal((seller.Id, "Plantorama"), (bare.SellerId, bare.Source));
        Assert.Equal((null, "A friend"), (withSource.SellerId, withSource.Source));
        Assert.Null(withSeller.Source);
        Assert.Equal(3, (await ViewAsync(order)).ItemCount);
        Assert.Equal((bare.Id, "Hoya", null), (item!.PlantId, item.Genus, item.Price));
        Assert.Equal(18m, (await ViewAsync(order)).PriceOf(item));
        Assert.False(item.IsOnTheWay);
    }

    [Fact]
    public async Task A_plant_added_while_saving_an_order_gets_the_seller_only_when_it_has_none()
    {
        var seller = await SellerAsync();
        var bare = new Plant { Genus = "Hoya" };
        var withSource = new Plant { Genus = "Ficus", Source = "A friend" };
        plants.Plants[bare.Id] = bare;
        plants.Plants[withSource.Id] = withSource;
        var order = new Order { SellerId = seller.Id, SellerName = seller.Name, OrderedOn = Day };

        await service.SaveAsync(order, [new OrderItem { Genus = "Hoya", PlantId = bare.Id }, new OrderItem { Genus = "Ficus", PlantId = withSource.Id }]);

        Assert.Equal((seller.Id, "Plantorama"), (bare.SellerId, bare.Source));
        Assert.Equal((null, "A friend"), (withSource.SellerId, withSource.Source));
    }

    [Fact]
    public async Task An_order_is_on_the_way_until_every_item_has_arrived_or_not_come()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"), Item("B"), Item("C"));
        Assert.True((await ViewAsync(order)).IsOnTheWay);

        await service.UnpackAsync(order.Id, Day.AddDays(-1), null, [new UnpackChoice { ItemId = list[0].Id }], Label);
        await service.SetDidNotComeAsync(list[1].Id, true);
        Assert.True((await ViewAsync(order)).IsOnTheWay);

        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[2].Id }], Label);
        var view = await ViewAsync(order);
        Assert.False(view.IsOnTheWay);
        Assert.Equal(Day, view.ArrivedOn);
        Assert.Equal(2, view.CameCount);
        Assert.Equal(0, await service.OnTheWayCountAsync());
    }

    [Fact]
    public async Task An_item_that_became_a_plant_cannot_be_marked_as_not_come()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"));
        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);

        await service.SetDidNotComeAsync(list[0].Id, true);

        Assert.False(items.Items[list[0].Id].DidNotCome);
    }

    [Fact]
    public async Task Items_show_in_the_order_they_were_made()
    {
        var (order, _) = await OrderAsync(null, null, null, Item("Third"), Item("First"), Item("Second"));

        Assert.Equal(["Third", "First", "Second"], (await ViewAsync(order)).Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Removing_an_item_from_the_order_soft_deletes_it()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"), Item("B"));

        await service.SaveAsync(order, [list[0]], [list[1].Id]);

        Assert.Equal(["A"], (await ViewAsync(order)).Items.Select(i => i.Name));
        Assert.True(items.Items[list[1].Id].IsDeleted);
    }

    [Fact]
    public async Task Saving_an_order_keeps_an_item_the_form_never_loaded()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"));
        // Added on another device after the form was opened
        var elsewhere = Item("Elsewhere");
        elsewhere.OrderId = order.Id;
        await items.SaveAsync(elsewhere);

        await service.SaveAsync(order, list);

        Assert.False(items.Items[elsewhere.Id].IsDeleted);
        Assert.Equal(2, (await ViewAsync(order)).ItemCount);
    }

    [Fact]
    public void A_plant_item_needs_no_name_of_its_own()
    {
        Assert.Empty(new OrderItem { PlantId = Guid.NewGuid() }.Validate());
        Assert.Empty(new OrderItem { PropagationId = Guid.NewGuid() }.Validate());
    }

    [Fact]
    public async Task Unpacking_links_a_plant_that_already_exists_instead_of_making_it_again()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A", 10m));
        var existing = new Plant { Nickname = "Already here", PricePaid = 99m, Id = NameBasedId.For("order-item-plant:" + list[0].Id) };
        plants.Plants[existing.Id] = existing;

        var result = await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);

        Assert.Empty(result.PlantIds);
        Assert.Equal(("Already here", 99m), (existing.Nickname, existing.PricePaid));
        Assert.Equal((existing.Id, Day), (items.Items[list[0].Id].PlantId, items.Items[list[0].Id].ArrivedOn));
        Assert.Single(plants.Plants);
    }

    [Fact]
    public async Task Unpacking_links_a_cutting_that_already_exists_instead_of_making_it_again()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"));
        var existing = new Propagation { Nickname = "Already here", Id = NameBasedId.For("order-item-cutting:" + list[0].Id) };
        propagations.Propagations[existing.Id] = existing;

        var result = await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id, UnpackAs = UnpackAs.Cutting }], Label);

        Assert.Empty(result.PropagationIds);
        Assert.Equal(existing.Id, items.Items[list[0].Id].PropagationId);
        Assert.Single(propagations.Propagations);
    }

    [Fact]
    public async Task Deleting_an_order_keeps_the_plants_made_from_it()
    {
        var (order, list) = await OrderAsync(null, null, null, Item("A"), Item("B"));
        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);
        var plant = plants.Plants.Values.Single();

        await service.DeleteAsync(order.Id);

        Assert.Null(await service.GetAsync(order.Id));
        Assert.Empty(await service.GetAllAsync());
        Assert.All(items.Items.Values, i => Assert.True(i.IsDeleted));
        Assert.False(plant.IsDeleted);
    }

    [Fact]
    public async Task An_item_whose_order_is_deleted_or_missing_is_skipped()
    {
        var (order, _) = await OrderAsync(null, null, null, Item("A"));
        var (kept, _) = await OrderAsync(null, null, null, Item("B"));
        await orders.DeleteAsync(order.Id);
        items.Items[Guid.NewGuid()] = new OrderItem { OrderId = Guid.NewGuid(), Name = "Orphan" };

        var all = await service.GetAllAsync();

        var view = Assert.Single(all);
        Assert.Equal(kept.Id, view.Order.Id);
        Assert.Equal(["B"], view.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task The_seller_name_follows_a_merge_and_orders_are_found_by_seller()
    {
        var old = await SellerAsync("Old name");
        var into = await SellerAsync("New name");
        var (order, _) = await OrderAsync(old, null, null, Item("A"));
        old.MergedIntoId = into.Id;
        old.DeletedAt = Now;

        var view = await ViewAsync(order);

        Assert.Equal(("New name", into.Id), (view.SellerName, view.SellerId));
        Assert.Single(await service.ForSellerAsync(into.Id));
        Assert.Empty(await service.ForSellerAsync(old.Id));
    }

    // Today

    private async Task<TodaySections> SectionsAsync(DateOnly day, params AwayStretch[] away) =>
        TodaySections.For(new TodayData { Orders = await service.GetAllAsync(), Stretches = away }, day, Time);

    [Fact]
    public async Task An_order_shows_on_today_from_its_expected_day_while_on_the_way()
    {
        var seller = await SellerAsync();
        var (order, list) = await OrderAsync(seller, Day, null, Item("A"));

        Assert.Empty((await SectionsAsync(Day.AddDays(-1))).OrdersArriving);

        var onTheDay = Assert.Single((await SectionsAsync(Day)).OrdersArriving);
        Assert.Equal(new OrderArriving(order.Id, "Plantorama", Day, true), onTheDay);

        var after = Assert.Single((await SectionsAsync(Day.AddDays(2))).OrdersArriving);
        Assert.False(after.IsToday);
        Assert.Equal(Day, after.ExpectedOn);

        await service.UnpackAsync(order.Id, Day, null, [new UnpackChoice { ItemId = list[0].Id }], Label);
        Assert.Empty((await SectionsAsync(Day)).OrdersArriving);
    }

    [Fact]
    public async Task An_order_with_no_expected_day_is_not_on_today()
    {
        await OrderAsync(null, null, null, Item("A"));

        Assert.Empty((await SectionsAsync(Day.AddDays(10))).OrdersArriving);
    }

    [Fact]
    public async Task An_order_is_not_on_today_while_away_or_quiet()
    {
        await OrderAsync(null, Day, null, Item("A"));

        Assert.Empty((await SectionsAsync(Day, new AwayStretch { From = Day.AddDays(-1), Until = Day.AddDays(3) })).OrdersArriving);
        Assert.Empty((await SectionsAsync(Day, new AwayStretch { From = Day, Until = Day.AddDays(3), Kind = AwayKind.Quiet })).OrdersArriving);
        Assert.Single((await SectionsAsync(Day.AddDays(3), new AwayStretch { From = Day.AddDays(-1), Until = Day.AddDays(3) })).OrdersArriving);
    }
}
