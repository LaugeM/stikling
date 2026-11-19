using Stikling.Core.Feeds;
using Stikling.Core.Models;
using Stikling.Core.SoilMixes;
using Stikling.Core.Stock;

namespace Stikling.Core.Tests;

public class BatchesAndStockTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 11);

    private static DateOnly DayOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    private static Supply Supply(string name, SupplyUnit unit) => new() { Name = name, Kind = SupplyKind.Mixing, Unit = unit };

    private static SoilMix Mix(MixUnit unit, params (string Name, decimal? Amount, Guid? Supply)[] parts) => new()
    {
        Name = "Aroid mix",
        Unit = unit,
        Ingredients = [.. parts.Select(p => new MixIngredient { Name = p.Name, Amount = p.Amount, SupplyId = p.Supply })]
    };

    private static Supply ProductSupply(Product product, SupplyUnit unit) => new()
    {
        Id = Models.Supply.IdForProduct(product.Id),
        ProductId = product.Id,
        Name = product.Name,
        Kind = SupplyKind.Water,
        Unit = unit
    };

    private sealed record Setup(
        SoilBatchService Batches, FeedMixingService Cans, StockService Stock,
        FakeSoilBatchRepository BatchRepo, FakeBatchMixingRepository MixingRepo, FakeFeedMixingRepository CanRepo,
        FakeSupplyRepository Supplies, FakeSupplyEntryRepository Entries, FakeSoilMixRepository Mixes,
        FakeProductRepository Products);

    private static Setup Create()
    {
        var supplies = new FakeSupplyRepository();
        var entries = new FakeSupplyEntryRepository();
        var products = new FakeProductRepository();
        var mixes = new FakeSoilMixRepository();
        var batches = new FakeSoilBatchRepository();
        var mixings = new FakeBatchMixingRepository();
        var cans = new FakeFeedMixingRepository();
        var time = new FixedTime(Now);
        var stock = new StockService(supplies, entries, new FakeShoppingItemRepository(), products, new FakePotRepository(), time, mixings, cans);
        return new Setup(
            new SoilBatchService(batches, mixings, mixes, supplies, stock, time),
            new FeedMixingService(cans, products, supplies, stock),
            stock, batches, mixings, cans, supplies, entries, mixes, products);
    }

    // What comes off

    [Fact]
    public void Parts_are_shares_of_the_volume_in_the_supplys_own_unit()
    {
        var leca = Supply("LECA", SupplyUnit.Litres);
        var perlite = Supply("Perlite", SupplyUnit.Millilitres);
        var mix = Mix(MixUnit.Parts, ("LECA", 1, leca.Id), ("Perlite", 3, perlite.Id));

        var lines = Takeoff.ForMixing(mix, 4, [leca, perlite]);

        Assert.Equal([1m, 3000m], lines.Select(l => l.Amount));
        Assert.Equal(["LECA", "Perlite"], lines.Select(l => l.What));
    }

    [Fact]
    public void Percent_works_the_same_as_parts()
    {
        var bark = Supply("Bark", SupplyUnit.Litres);
        var mix = Mix(MixUnit.Percent, ("Bark", 25, bark.Id), ("Soil", 75, null));

        var line = Assert.Single(Takeoff.ForMixing(mix, 8, [bark]));

        Assert.Equal(2m, line.Amount);
    }

    [Theory]
    [InlineData(SupplyUnit.Kilograms)]
    [InlineData(SupplyUnit.Pieces)]
    [InlineData(SupplyUnit.Packs)]
    [InlineData(SupplyUnit.Grams)]
    public void A_volume_cant_be_turned_into_a_weight_or_a_count(SupplyUnit unit)
    {
        var sand = Supply("Sand", unit);
        var mix = Mix(MixUnit.Parts, ("Sand", 1, sand.Id), ("Soil", 1, null));

        var line = Assert.Single(Takeoff.ForMixing(mix, 4, [sand]));

        Assert.Null(line.Amount);
        Assert.Equal(sand.Id, line.SupplyId);
    }

    [Fact]
    public void A_mix_that_isnt_counted_gives_empty_lines_for_its_linked_ingredients_only()
    {
        var leca = Supply("LECA", SupplyUnit.Litres);
        var mix = Mix(MixUnit.None, ("LECA", null, leca.Id), ("Soil", null, null));

        var line = Assert.Single(Takeoff.ForMixing(mix, 4, [leca]));

        Assert.Null(line.Amount);
    }

    [Fact]
    public void A_deleted_or_missing_supply_is_left_off_the_sheet()
    {
        var gone = Supply("Gone", SupplyUnit.Litres);
        gone.DeletedAt = Now;
        var mix = Mix(MixUnit.Parts, ("Gone", 1, gone.Id), ("Lost", 1, Guid.NewGuid()));

        Assert.Empty(Takeoff.ForMixing(mix, 4, [gone]));
    }

    [Theory]
    [InlineData(DoseUnit.Millilitres, SupplyUnit.Millilitres, 6, 6)]
    [InlineData(DoseUnit.Millilitres, SupplyUnit.Litres, 6, 0.006)]
    [InlineData(DoseUnit.Grams, SupplyUnit.Grams, 6, 6)]
    [InlineData(DoseUnit.Grams, SupplyUnit.Kilograms, 6, 0.006)]
    public void A_dose_is_converted_between_millilitres_and_litres_and_grams_and_kilograms(
        DoseUnit dose, SupplyUnit supply, double amount, double expected) =>
        Assert.Equal((decimal)expected, Takeoff.FromDose((decimal)amount, dose, supply));

    [Theory]
    [InlineData(DoseUnit.Drops, SupplyUnit.Millilitres)]
    [InlineData(DoseUnit.Millilitres, SupplyUnit.Grams)]
    [InlineData(DoseUnit.Grams, SupplyUnit.Millilitres)]
    [InlineData(DoseUnit.Millilitres, SupplyUnit.Pieces)]
    public void A_dose_that_cant_be_converted_is_left_empty(DoseUnit dose, SupplyUnit supply) =>
        Assert.Null(Takeoff.FromDose(6, dose, supply));

    [Fact]
    public void A_can_takes_each_dose_times_the_water_off_the_supply_found_through_the_product()
    {
        var feed = new Product { Name = "Hydro feed" };
        var silica = new Product { Name = "Silica" };
        var feedSupply = ProductSupply(feed, SupplyUnit.Millilitres);
        var doses = new List<ProductDose>
        {
            new() { ProductId = feed.Id, Name = "Hydro feed", Amount = 2, Unit = DoseUnit.Millilitres },
            new() { ProductId = silica.Id, Name = "Silica", Amount = 1, Unit = DoseUnit.Millilitres }
        };

        var line = Assert.Single(Takeoff.ForCan(doses, 3, [feedSupply]));

        Assert.Equal(6m, line.Amount);
        Assert.Equal(feedSupply.Id, line.SupplyId);
    }

    [Fact]
    public void A_dose_for_a_different_amount_of_water_is_scaled()
    {
        var product = new Product { Name = "Feed" };
        var supply = ProductSupply(product, SupplyUnit.Millilitres);
        var dose = new ProductDose { ProductId = product.Id, Name = "Feed", Amount = 5, Per = 4, PerUnit = WaterUnit.Litres };

        var line = Assert.Single(Takeoff.ForCan([dose], 2, [supply]));

        Assert.Equal(2.5m, line.Amount);
    }

    // Picking a batch

    private static BatchOption Option(Guid mix, int day, bool usedUp = false, decimal litres = 4)
    {
        var batch = new SoilBatch { SoilMixId = mix, MixedOn = new DateOnly(2026, 11, day), CreatedAt = Now.AddMinutes(day), UsedUpOn = usedUp ? Today : null };
        return new BatchOption(batch, litres, 1);
    }

    [Fact]
    public void The_newest_batch_that_isnt_used_up_is_picked()
    {
        var mix = Guid.NewGuid();
        var older = Option(mix, 3);
        var newest = Option(mix, 9, usedUp: true);
        var open = Option(mix, 7);
        var other = Option(Guid.NewGuid(), 10);

        var options = SoilBatches.Options(
            [older.Batch, newest.Batch, open.Batch, other.Batch],
            [.. new[] { older, newest, open, other }.Select(o => new BatchMixing { BatchId = o.Id, Litres = 4 })]);

        Assert.Equal(open.Id, SoilBatches.Newest(mix, options)?.Id);
    }

    [Fact]
    public void No_batch_is_picked_when_a_mix_has_none_open()
    {
        var mix = Guid.NewGuid();
        var used = Option(mix, 3, usedUp: true);

        Assert.Null(SoilBatches.Newest(mix, [used]));
        Assert.Null(SoilBatches.Newest(Guid.NewGuid(), [used]));
    }

    [Fact]
    public void The_choices_for_a_plant_already_in_a_used_up_batch_still_include_it()
    {
        var mix = Guid.NewGuid();
        var used = Option(mix, 3, usedUp: true);
        var open = Option(mix, 7);

        Assert.Equal([open.Id], SoilBatches.Choices(mix, null, [open, used]).Select(o => o.Id));
        Assert.Equal([open.Id, used.Id], SoilBatches.Choices(mix, used.Id, [open, used]).Select(o => o.Id));
    }

    [Fact]
    public void Batches_on_the_same_day_are_ordered_the_same_on_every_device()
    {
        var mix = Guid.NewGuid();
        var a = new SoilBatch { SoilMixId = mix, MixedOn = Today, CreatedAt = Now };
        var b = new SoilBatch { SoilMixId = mix, MixedOn = Today, CreatedAt = Now };

        var one = SoilBatches.Options([a, b], []).Select(o => o.Id);
        var two = SoilBatches.Options([b, a], []).Select(o => o.Id);

        Assert.Equal(one, two);
    }

    // Volume

    [Fact]
    public void A_batchs_volume_is_the_sum_of_its_mixings_whatever_order_they_arrive_in()
    {
        var batch = new SoilBatch { SoilMixId = Guid.NewGuid(), MixedOn = Today };
        var first = new BatchMixing { BatchId = batch.Id, Litres = 4, MixedOn = Today };
        var topUpA = new BatchMixing { BatchId = batch.Id, Litres = 2, MixedOn = Today.AddDays(3) };
        var topUpB = new BatchMixing { BatchId = batch.Id, Litres = 1.5m, MixedOn = Today.AddDays(3) };
        var deleted = new BatchMixing { BatchId = batch.Id, Litres = 100, DeletedAt = Now };
        var elsewhere = new BatchMixing { BatchId = Guid.NewGuid(), Litres = 50 };

        var forward = SoilBatches.Volume(batch.Id, [first, topUpA, topUpB, deleted, elsewhere]);
        var backward = SoilBatches.Volume(batch.Id, [elsewhere, deleted, topUpB, topUpA, first]);

        Assert.Equal(7.5m, forward);
        Assert.Equal(forward, backward);
        Assert.Equal(7.5m, Assert.Single(SoilBatches.Options([batch], [first, topUpA, topUpB, deleted, elsewhere])).Litres);
    }

    [Fact]
    public void A_batch_whose_mix_was_deleted_on_another_device_still_works()
    {
        var mix = Mix(MixUnit.None, ("Soil", null, null));
        mix.DeletedAt = Now;
        var batch = new SoilBatch { SoilMixId = mix.Id, MixName = "Aroid mix", MixedOn = Today };

        var option = Assert.Single(SoilBatches.Options([batch], [new BatchMixing { BatchId = batch.Id, Litres = 3 }]));

        Assert.Equal(3m, option.Litres);
        Assert.Equal("Aroid mix", option.Batch.MixName);
        Assert.Equal(option.Id, SoilBatches.Newest(mix.Id, [option])?.Id);
    }

    [Fact]
    public void Mixings_of_a_batch_that_was_never_seen_make_no_batch()
    {
        Assert.Empty(SoilBatches.Options([], [new BatchMixing { BatchId = Guid.NewGuid(), Litres = 3 }]));
    }

    // The service

    [Fact]
    public async Task Mixing_a_batch_makes_the_batch_and_its_first_mixing_and_a_top_up_adds_another()
    {
        var s = Create();
        var mix = Mix(MixUnit.Parts, ("Soil", 1, null));
        s.Mixes.Mixes[mix.Id] = mix;

        var first = await s.Batches.MixAsync(mix, 4, Today);
        await s.Batches.TopUpAsync(first.BatchId, 2, Today.AddDays(5));

        var option = Assert.Single(await s.Batches.GetOptionsAsync());
        Assert.Equal(6m, option.Litres);
        Assert.Equal(2, option.Mixings);
        Assert.Equal(Today, option.Batch.MixedOn);
        Assert.Equal("Aroid mix", option.Batch.MixName);
    }

    [Fact]
    public async Task A_batch_can_be_marked_used_up_and_a_top_up_opens_it_again()
    {
        var s = Create();
        var mix = Mix(MixUnit.None, ("Soil", null, null));
        var first = await s.Batches.MixAsync(mix, 4, Today);
        var batch = s.BatchRepo.Batches[first.BatchId];

        await s.Batches.SetUsedUpAsync(batch, true);
        Assert.True(s.BatchRepo.Batches[batch.Id].IsUsedUp);

        await s.Batches.TopUpAsync(batch.Id, 1, Today);
        Assert.False(s.BatchRepo.Batches[batch.Id].IsUsedUp);
    }

    [Fact]
    public async Task Linking_a_supply_changes_nothing_else_about_the_mix()
    {
        var s = Create();
        var leca = Supply("LECA", SupplyUnit.Litres);
        var mix = Mix(MixUnit.Parts, ("LECA", 1, null), ("Soil", 2, null));
        mix.Notes = "Chunky";
        s.Mixes.Mixes[mix.Id] = mix;

        await s.Batches.LinkSuppliesAsync(mix, [leca.Id, null]);

        var saved = s.Mixes.Mixes[mix.Id];
        Assert.Equal([leca.Id, null], saved.Ingredients.Select(i => i.SupplyId));
        Assert.Equal(["LECA", "Soil"], saved.Ingredients.Select(i => i.Name));
        Assert.Equal([1m, 2m], saved.Ingredients.Select(i => i.Amount));
        Assert.Equal("Chunky", saved.Notes);
        Assert.Equal(MixUnit.Parts, saved.Unit);
    }

    // The stock

    private static SupplyEntry Counted(Supply supply, decimal amount, int daysAgo = 5) => new()
    {
        SupplyId = supply.Id, Kind = SupplyEntryKind.Counted, Amount = amount, At = Now.AddDays(-daysAgo), CreatedAt = Now.AddDays(-daysAgo)
    };

    private static SupplyStatus StatusOf(Supply supply, IEnumerable<SupplyEntry> entries, IReadOnlySet<Guid>? gone = null) =>
        Assert.Single(StockView.Build([supply], entries, [], [], [], DayOf, Today, gone).Supplies);

    [Fact]
    public void What_was_used_after_a_count_comes_off_the_amount()
    {
        var leca = Supply("LECA", SupplyUnit.Litres);
        var used = new SupplyEntry { SupplyId = leca.Id, Kind = SupplyEntryKind.Used, Amount = 1.5m, At = Now, CreatedAt = Now, UsedForId = Guid.NewGuid() };

        var status = StatusOf(leca, [Counted(leca, 10), used]);

        Assert.Equal(8.5m, status.Amount);
        Assert.Equal(SupplyLevel.Have, status.Level);
    }

    [Fact]
    public void What_was_used_before_the_count_doesnt_count()
    {
        var leca = Supply("LECA", SupplyUnit.Litres);
        var used = new SupplyEntry { SupplyId = leca.Id, Kind = SupplyEntryKind.Used, Amount = 3, At = Now.AddDays(-10), CreatedAt = Now.AddDays(-10) };

        Assert.Equal(10m, StatusOf(leca, [used, Counted(leca, 10)]).Amount);
    }

    [Fact]
    public void Using_up_what_was_counted_makes_the_supply_out()
    {
        var leca = Supply("LECA", SupplyUnit.Litres);
        var used = new SupplyEntry { SupplyId = leca.Id, Kind = SupplyEntryKind.Used, Amount = 12, At = Now, CreatedAt = Now };

        var status = StatusOf(leca, [Counted(leca, 10), used]);

        Assert.Equal(0m, status.Amount);
        Assert.Equal(SupplyLevel.Out, status.Level);
    }

    [Fact]
    public void Using_some_doesnt_clear_running_low()
    {
        var leca = Supply("LECA", SupplyUnit.Litres);
        var low = new SupplyEntry { SupplyId = leca.Id, Kind = SupplyEntryKind.RunningLow, At = Now.AddDays(-1), CreatedAt = Now.AddDays(-1) };
        var used = new SupplyEntry { SupplyId = leca.Id, Kind = SupplyEntryKind.Used, Amount = 1, At = Now, CreatedAt = Now };

        var status = StatusOf(leca, [low, used]);

        Assert.Equal(SupplyLevel.Low, status.Level);
        Assert.Contains(StockView.Build([leca], [low, used], [], [], [], DayOf, Today).ToBuy, r => r.Supply?.Supply.Id == leca.Id);
    }

    [Fact]
    public void An_entry_for_a_deleted_mixing_is_ignored()
    {
        var leca = Supply("LECA", SupplyUnit.Litres);
        var mixing = Guid.NewGuid();
        var used = new SupplyEntry { SupplyId = leca.Id, Kind = SupplyEntryKind.Used, Amount = 4, At = Now, CreatedAt = Now, UsedForId = mixing };

        Assert.Equal(10m, StatusOf(leca, [Counted(leca, 10), used], new HashSet<Guid> { mixing }).Amount);
    }

    [Fact]
    public async Task Confirming_the_sheet_adds_one_entry_per_line_with_an_amount()
    {
        var s = Create();
        var leca = Supply("LECA", SupplyUnit.Litres);
        var sand = Supply("Sand", SupplyUnit.Kilograms);
        s.Supplies.Supplies[leca.Id] = leca;
        s.Supplies.Supplies[sand.Id] = sand;
        var mix = Mix(MixUnit.Parts, ("LECA", 1, leca.Id), ("Sand", 1, sand.Id), ("Soil", 2, null));
        var mixing = await s.Batches.MixAsync(mix, 4, Today);

        var lines = await s.Batches.TakeoffAsync(mix, 4);
        var saved = await s.Stock.TakeOffAsync(mixing.Id, lines);

        var entry = Assert.Single(saved);
        Assert.Equal(SupplyEntryKind.Used, entry.Kind);
        Assert.Equal(1m, entry.Amount);
        Assert.Equal(mixing.Id, entry.UsedForId);
        Assert.Single(s.Entries.Entries.Values);
    }

    [Fact]
    public async Task A_line_filled_in_by_hand_is_taken_off_and_one_left_empty_is_not()
    {
        var s = Create();
        var sand = Supply("Sand", SupplyUnit.Kilograms);
        s.Supplies.Supplies[sand.Id] = sand;
        var mix = Mix(MixUnit.Parts, ("Sand", 1, sand.Id), ("Soil", 1, null));
        var mixing = await s.Batches.MixAsync(mix, 4, Today);
        var line = Assert.Single(await s.Batches.TakeoffAsync(mix, 4));

        Assert.Empty(await s.Stock.TakeOffAsync(mixing.Id, [line]));
        var saved = await s.Stock.TakeOffAsync(mixing.Id, [line with { Amount = 1.2m }]);

        Assert.Equal(1.2m, Assert.Single(saved).Amount);
    }

    [Fact]
    public async Task Confirming_twice_for_one_mixing_doesnt_take_it_off_twice()
    {
        var s = Create();
        var leca = Supply("LECA", SupplyUnit.Litres);
        s.Supplies.Supplies[leca.Id] = leca;
        var mix = Mix(MixUnit.Parts, ("LECA", 1, leca.Id));
        var mixing = await s.Batches.MixAsync(mix, 4, Today);
        var lines = await s.Batches.TakeoffAsync(mix, 4);

        await s.Stock.TakeOffAsync(mixing.Id, lines);
        await s.Stock.TakeOffAsync(mixing.Id, lines);

        Assert.Single(s.Entries.Entries.Values, e => !e.IsDeleted);
    }

    [Fact]
    public async Task Deleting_a_mixing_puts_the_level_back()
    {
        var s = Create();
        var leca = Supply("LECA", SupplyUnit.Litres);
        s.Supplies.Supplies[leca.Id] = leca;
        await s.Stock.MarkAsync(leca.Id, SupplyEntryKind.Counted, 10, null, null, Now.AddDays(-1));
        var mix = Mix(MixUnit.Parts, ("LECA", 1, leca.Id));
        var mixing = await s.Batches.MixAsync(mix, 4, Today);
        await s.Stock.TakeOffAsync(mixing.Id, await s.Batches.TakeoffAsync(mix, 4));
        Assert.Equal(6m, (await s.Stock.GetAsync()).Find(leca.Id)!.Amount);

        await s.Batches.DeleteMixingAsync(mixing.Id);

        Assert.Equal(10m, (await s.Stock.GetAsync()).Find(leca.Id)!.Amount);
        Assert.All(s.Entries.Entries.Values.Where(e => e.Kind == SupplyEntryKind.Used), e => Assert.True(e.IsDeleted));
    }

    [Fact]
    public async Task Deleting_a_batch_deletes_its_mixings_and_what_they_took_off()
    {
        var s = Create();
        var leca = Supply("LECA", SupplyUnit.Litres);
        s.Supplies.Supplies[leca.Id] = leca;
        await s.Stock.MarkAsync(leca.Id, SupplyEntryKind.Counted, 10, null, null, Now.AddDays(-1));
        var mix = Mix(MixUnit.Parts, ("LECA", 1, leca.Id));
        var first = await s.Batches.MixAsync(mix, 4, Today);
        var topUp = await s.Batches.TopUpAsync(first.BatchId, 2, Today);
        await s.Stock.TakeOffAsync(first.Id, await s.Batches.TakeoffAsync(mix, 4));
        await s.Stock.TakeOffAsync(topUp.Id, await s.Batches.TakeoffAsync(mix, 2));

        await s.Batches.DeleteBatchAsync(first.BatchId);

        Assert.Empty(await s.Batches.GetOptionsAsync());
        Assert.Equal(10m, (await s.Stock.GetAsync()).Find(leca.Id)!.Amount);
    }

    // Feed

    [Fact]
    public async Task A_can_keeps_the_doses_as_they_were_and_takes_them_off_once()
    {
        var s = Create();
        var product = new Product { Name = "Hydro feed", DefaultDose = 2, Unit = DoseUnit.Millilitres };
        s.Products.Products[product.Id] = product;
        var supply = ProductSupply(product, SupplyUnit.Millilitres);
        s.Supplies.Supplies[supply.Id] = supply;
        await s.Stock.MarkAsync(supply.Id, SupplyEntryKind.Counted, 100, null, null, Now.AddDays(-1));
        var feed = new Feed { Name = "Weekly", Products = [ProductDose.From(product)] };

        var can = await s.Cans.MixAsync(feed, 5, Today);
        feed.Products[0].Amount = 9;
        await s.Stock.TakeOffAsync(can.Id, await s.Cans.TakeoffAsync(can));

        Assert.Equal(2m, can.Doses[0].Amount);
        Assert.Equal(90m, (await s.Stock.GetAsync()).Find(supply.Id)!.Amount);
        Assert.Equal("Weekly", can.FeedName);
    }

    [Fact]
    public async Task Deleting_a_can_gives_the_products_back_and_the_feed_remembers_when_it_was_last_mixed()
    {
        var s = Create();
        var product = new Product { Name = "Hydro feed", DefaultDose = 2 };
        s.Products.Products[product.Id] = product;
        var supply = ProductSupply(product, SupplyUnit.Millilitres);
        s.Supplies.Supplies[supply.Id] = supply;
        await s.Stock.MarkAsync(supply.Id, SupplyEntryKind.Counted, 100, null, null, Now.AddDays(-1));
        var feed = new Feed { Name = "Weekly", Products = [ProductDose.From(product)] };
        await s.Cans.MixAsync(feed, 2, Today.AddDays(-7));
        var newer = await s.Cans.MixAsync(feed, 5, Today);
        await s.Stock.TakeOffAsync(newer.Id, await s.Cans.TakeoffAsync(newer));
        Assert.Equal(Today, (await s.Cans.GetLastMixedAsync())[feed.Id]);

        await s.Cans.DeleteAsync(newer.Id);

        Assert.Equal(100m, (await s.Stock.GetAsync()).Find(supply.Id)!.Amount);
        Assert.Equal(Today.AddDays(-7), (await s.Cans.GetLastMixedAsync())[feed.Id]);
    }

    [Fact]
    public void An_older_app_reads_the_used_kind_as_unknown_and_ignores_it()
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var entry = System.Text.Json.JsonSerializer.Deserialize<SupplyEntry>("{\"kind\":\"SomethingNew\"}", options);

        Assert.Null(entry!.Kind);
    }
}
