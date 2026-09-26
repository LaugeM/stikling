using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class FeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 26);

    private readonly Guid plant = Guid.NewGuid();
    private readonly Guid otherPlant = Guid.NewGuid();

    private readonly FakeCareLogRepository logs = new();
    private readonly CareService care;

    private readonly Product silica = new() { Name = "Silica", Kind = ProductKind.Silica, DefaultDose = 0.5m };
    private readonly Product hydro = new() { Name = "Hydro fertiliser", DefaultDose = 2 };
    private readonly Product rooting = new() { Name = "Root juice", Kind = ProductKind.Stimulant, DefaultDose = 3, Unit = DoseUnit.Drops };
    private readonly Product phDown = new() { Name = "pH down", Kind = ProductKind.Ph };

    public FeedTests()
    {
        care = new CareService(logs, new FakeTimelineRepository(), new FixedTime(Now));
    }

    private static string Label(Enum value) => value.ToString();

    private Product[] All => [silica, hydro, rooting, phDown];

    private static Feed Mix(string name, params Product[] products) => new()
    {
        Name = name,
        Products = [.. products.Select(ProductDose.From)]
    };

    // The feed

    [Fact]
    public void A_feed_with_a_name_and_a_product_is_valid() =>
        Assert.Empty(Mix("Aroid feed", hydro).Validate());

    [Fact]
    public void Validate_wants_a_name_and_a_product()
    {
        var errors = new Feed { Name = " " }.Validate();

        Assert.Contains("Give the feed a name.", errors);
        Assert.Contains("Add at least one product.", errors);
    }

    [Fact]
    public void Validate_rejects_a_dose_below_zero()
    {
        var feed = Mix("Aroid feed", hydro);
        feed.Products[0].Amount = -1;

        Assert.Contains("A dose can't be less than 0.", feed.Validate());
    }

    [Fact]
    public void A_product_without_a_dose_is_fine() =>
        Assert.Empty(Mix("Aroid feed", phDown).Validate());

    [Fact]
    public void Each_product_starts_at_its_usual_dose_and_can_have_its_own()
    {
        var feed = Mix("Rooting water", hydro);
        feed.Products[0].Amount = 0.5m;

        Assert.Equal("Hydro fertiliser, 0.5 ml/L", feed.Products[0].ToString());
        Assert.Equal(2, hydro.DefaultDose);
    }

    [Fact]
    public void Moving_changes_the_order_and_stops_at_the_ends()
    {
        var feed = Mix("Aroid feed", hydro, silica);

        feed.MoveUp(1);
        Assert.Equal(["Silica", "Hydro fertiliser"], feed.Products.Select(p => p.Name));

        feed.MoveUp(0);
        feed.MoveDown(1);
        Assert.Equal(["Silica", "Hydro fertiliser"], feed.Products.Select(p => p.Name));

        feed.MoveDown(0);
        Assert.Equal(["Hydro fertiliser", "Silica"], feed.Products.Select(p => p.Name));
    }

    [Fact]
    public void Copying_a_feed_copies_its_products()
    {
        var feed = Mix("Aroid feed", hydro);

        var copy = feed.Copy();
        copy.Products[0].Amount = 4;
        copy.Products.Add(ProductDose.From(silica));

        Assert.Equal(2, Assert.Single(feed.Products).Amount);
    }

    // The order note

    [Fact]
    public void Silica_first_and_ph_last_says_nothing() =>
        Assert.Null(Mix("Aroid feed", silica, hydro, rooting, phDown).OrderNote(All));

    [Fact]
    public void Silica_after_something_else_says_so() =>
        Assert.StartsWith("Silica usually goes in first", Mix("Aroid feed", hydro, silica).OrderNote(All));

    [Fact]
    public void Ph_before_something_else_says_so() =>
        Assert.StartsWith("pH up or down usually goes in last", Mix("Aroid feed", phDown, hydro).OrderNote(All));

    [Fact]
    public void Two_silicas_at_the_top_are_fine()
    {
        var other = new Product { Name = "Silica powder", Kind = ProductKind.Silica };

        Assert.Null(Mix("Aroid feed", silica, other, hydro).OrderNote([.. All, other]));
    }

    [Fact]
    public void A_product_not_in_the_list_is_left_out_of_the_note()
    {
        var feed = Mix("Aroid feed", hydro);
        feed.Products.Insert(0, new ProductDose { Name = "Something from a friend" });

        Assert.Null(feed.OrderNote(All));
    }

    // The doses to log

    [Fact]
    public void Doses_are_copies_in_order_with_the_current_names()
    {
        var feed = Mix("Aroid feed", silica, hydro);
        hydro.Name = "Hydro fertiliser (new bottle)";
        hydro.DefaultDose = 3;

        var doses = feed.Doses(All);
        doses[0].Amount = 1;

        Assert.Equal(["Silica, 1 ml/L", "Hydro fertiliser (new bottle), 2 ml/L"], doses.Select(d => d.ToString()));
        // The dose is the feed's own, not the product's usual one, and the feed itself is left alone
        Assert.Equal(0.5m, feed.Products[0].Amount);
        Assert.Equal("Hydro fertiliser", feed.Products[1].Name);
    }

    [Fact]
    public void A_deleted_product_keeps_the_name_it_had()
    {
        var feed = Mix("Aroid feed", hydro);
        hydro.Name = "Renamed";
        hydro.DeletedAt = Now;

        Assert.Equal("Hydro fertiliser", Assert.Single(feed.Doses(All)).Name);
    }

    // Measuring out a batch

    [Theory]
    [InlineData(2, DoseUnit.Millilitres, 5, "10 ml")]
    [InlineData(0.5, DoseUnit.Millilitres, 1.5, "0.75 ml")]
    [InlineData(1.5, DoseUnit.Grams, 2, "3 g")]
    [InlineData(3, DoseUnit.Drops, 0.5, "1.5 drops")]
    [InlineData(1, DoseUnit.Drops, 1, "1 drop")]
    public void A_batch_is_the_dose_times_the_water(decimal dose, DoseUnit unit, decimal litres, string expected)
    {
        var measured = new ProductDose { Name = "X", Amount = dose, Unit = unit }.For(litres);

        Assert.Equal(expected, Doses.Measured(measured!.Value, unit));
    }

    [Fact]
    public void No_dose_means_nothing_to_measure() =>
        Assert.Null(ProductDose.From(phDown).For(2));

    // A dose the way the bottle puts it

    [Theory]
    [InlineData(5, 4, WaterUnit.Litres, 2, "2.5 ml")]
    [InlineData(1, 500, WaterUnit.Millilitres, 2, "4 ml")]
    [InlineData(5, 3, WaterUnit.Litres, 1, "1.67 ml")]
    [InlineData(5, 4, WaterUnit.Litres, 0.5, "0.63 ml")]
    public void A_dose_for_other_than_a_litre_is_scaled(decimal dose, decimal per, WaterUnit perUnit, decimal litres, string expected)
    {
        var measured = new ProductDose { Name = "X", Amount = dose, Per = per, PerUnit = perUnit }.For(litres);

        Assert.Equal(expected, Doses.Measured(measured!.Value, DoseUnit.Millilitres));
    }

    [Theory]
    [InlineData(2, DoseUnit.Millilitres, 1, WaterUnit.Litres, "2 ml/L")]
    [InlineData(5, DoseUnit.Millilitres, 4, WaterUnit.Litres, "5 ml per 4 L")]
    [InlineData(1, DoseUnit.Drops, 500, WaterUnit.Millilitres, "1 drop per 500 ml")]
    [InlineData(1, DoseUnit.Grams, 1, WaterUnit.Millilitres, "1 g per 1 ml")]
    public void A_dose_reads_the_way_it_was_typed(decimal dose, DoseUnit unit, decimal per, WaterUnit perUnit, string expected) =>
        Assert.Equal(expected, Doses.Text(dose, unit, per, perUnit));

    [Fact]
    public void A_product_dose_carries_its_water_onto_the_feed_and_the_entry()
    {
        var bottle = new Product { Name = "Grow", DefaultDose = 5, Per = 4 };

        var dose = ProductDose.From(bottle);

        Assert.Equal("Grow, 5 ml per 4 L", dose.ToString());
        Assert.Equal("Grow, 5 ml per 4 L", dose.Copy().ToString());
    }

    [Fact]
    public void The_water_a_dose_is_for_has_to_be_more_than_nothing()
    {
        var feed = Mix("Aroid feed", hydro);
        feed.Products[0].Per = 0;
        var entry = new CareLog { PlantId = plant, Kind = CareKind.Fertilised, OccurredOn = Today, Products = [feed.Products[0]] };

        Assert.Contains(Doses.NoWater, new Product { Name = "Grow", Per = 0 }.Validate());
        Assert.Contains(Doses.NoWater, feed.Validate());
        Assert.Contains(Doses.NoWater, entry.Validate(Today));
        Assert.Null(feed.Products[0].For(2));
    }

    [Fact]
    public void A_dose_keeps_its_water_through_a_backup()
    {
        var feed = new Feed { Name = "Grow feed", Products = [new ProductDose { Name = "Grow", Amount = 5, Per = 4, Unit = DoseUnit.Drops }] };

        var json = JsonSerializer.Serialize(feed);
        var restored = JsonSerializer.Deserialize<Feed>(json)!;

        Assert.Equal("Grow, 5 drops per 4 L", restored.Products[0].ToString());
        Assert.Contains("\"Drops\"", json);
        Assert.Contains("\"Litres\"", json);
    }

    // Logging a feed

    [Fact]
    public async Task Logging_a_feed_keeps_its_name_the_water_and_each_dose()
    {
        var feed = Mix("Aroid feed", silica, hydro);

        await care.LogManyAsync([plant, otherPlant], CareKind.Fertilised, Today, null, Label,
            products: feed.Doses(All), feed: feed, waterLitres: 2);

        Assert.Equal(2, logs.Logs.Count);
        Assert.All(logs.Logs, entry =>
        {
            Assert.Equal(feed.Id, entry.FeedId);
            Assert.Equal("Aroid feed", entry.FeedName);
            Assert.Equal(2, entry.WaterLitres);
            Assert.Equal(["Silica", "Hydro fertiliser"], entry.Products.Select(p => p.Name));
        });
    }

    [Fact]
    public async Task Changing_the_feed_later_does_not_change_what_was_logged()
    {
        var feed = Mix("Aroid feed", silica, hydro);
        await care.LogManyAsync([plant], CareKind.Fertilised, Today, null, Label,
            products: feed.Doses(All), feed: feed, waterLitres: 2);

        feed.Name = "Aroid feed, stronger";
        feed.Products[1].Amount = 4;
        feed.Products.RemoveAt(0);

        Assert.Equal(
            "Fertilised with Aroid feed (2 L): Silica, 0.5 ml/L + Hydro fertiliser, 2 ml/L",
            CareService.Describe(logs.Logs[0], Label));
    }

    [Fact]
    public void Water_without_a_saved_feed_reads_after_the_kind()
    {
        var entry = new CareLog { PlantId = plant, Kind = CareKind.ToppedUp, OccurredOn = Today, WaterLitres = 1.5m, Products = [ProductDose.From(hydro)] };

        Assert.Equal("ToppedUp (1.5 L): Hydro fertiliser, 2 ml/L", CareService.Describe(entry, Label));
        entry.WaterLitres = 0.5m;
        Assert.Equal("ToppedUp (500 ml): Hydro fertiliser, 2 ml/L", CareService.Describe(entry, Label));
    }

    [Fact]
    public void Only_a_feed_can_have_a_feed_or_water()
    {
        var entry = new CareLog { PlantId = plant, Kind = CareKind.Watered, OccurredOn = Today, FeedName = "Aroid feed" };

        Assert.Contains("Only fertilising and topping up can have a feed.", entry.Validate(Today));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void The_water_has_to_be_more_than_nothing(decimal litres)
    {
        var entry = new CareLog { PlantId = plant, Kind = CareKind.Fertilised, OccurredOn = Today, WaterLitres = litres };

        Assert.Contains("The amount of water has to be more than 0.", entry.Validate(Today));
    }

    // Reading use back

    [Fact]
    public async Task Use_counts_plants_and_the_last_day()
    {
        var feeds = new FakeFeedRepository();
        var aroid = Mix("Aroid feed", hydro);
        var rootingWater = Mix("Rooting water", rooting);
        await feeds.SaveAsync(rootingWater);
        await feeds.SaveAsync(aroid);

        await care.LogManyAsync([plant, otherPlant], CareKind.Fertilised, Today.AddDays(-10), null, Label,
            products: aroid.Doses(All), feed: aroid);
        await care.LogManyAsync([plant], CareKind.ToppedUp, Today.AddDays(-3), null, Label,
            products: aroid.Doses(All), feed: aroid);
        // Picking the same products by hand isn't giving the feed
        await care.LogManyAsync([plant], CareKind.Fertilised, Today, null, Label, products: aroid.Doses(All));

        var uses = await new FeedService(feeds, logs).GetAllAsync();

        Assert.Equal(["Aroid feed", "Rooting water"], uses.Select(u => u.Feed.Name));
        Assert.Equal(2, uses[0].Plants);
        Assert.Equal(Today.AddDays(-3), uses[0].LastUsed);
        Assert.Equal(0, uses[1].Plants);
        Assert.Null(uses[1].LastUsed);
    }

    // Backup

    [Fact]
    public void Feeds_and_what_was_logged_survive_a_backup()
    {
        var feed = Mix("Aroid feed", silica, hydro);
        var entry = new CareLog
        {
            PlantId = plant,
            Kind = CareKind.Fertilised,
            OccurredOn = Today,
            FeedId = feed.Id,
            FeedName = feed.Name,
            WaterLitres = 2,
            Products = feed.Doses(All)
        };
        var backup = new BackupData { Products = [silica], Feeds = [feed], CareLogs = [entry] };

        var json = JsonSerializer.Serialize(backup);
        var restored = JsonSerializer.Deserialize<BackupData>(json)!;

        Assert.Equal(["Silica", "Hydro fertiliser"], Assert.Single(restored.Feeds).Products.Select(p => p.Name));
        Assert.Equal(ProductKind.Silica, Assert.Single(restored.Products).Kind);
        var logged = Assert.Single(restored.CareLogs);
        Assert.Equal(feed.Id, logged.FeedId);
        Assert.Equal(2, logged.WaterLitres);
        Assert.Equal(1, restored.Counts.Feeds);
        // Stored as text, so reordering the kinds can't change what old data means
        Assert.Contains("\"Silica\"", json);
    }
}
