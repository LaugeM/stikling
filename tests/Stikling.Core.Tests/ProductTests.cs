using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Products;

namespace Stikling.Core.Tests;

public class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 24);

    private readonly Guid plant = Guid.NewGuid();
    private readonly Guid otherPlant = Guid.NewGuid();

    private readonly FakeCareLogRepository logs = new();
    private readonly FakeProductRepository products = new();
    private readonly CareService care;

    public ProductTests()
    {
        care = new CareService(logs, new FakeTimelineRepository(), new FixedTime(Now));
    }

    private static string Label(Enum value) => value.ToString();

    private static Product Hydro() =>
        new() { Name = "Hydro fertiliser", DefaultDose = 2, Unit = DoseUnit.Millilitres };

    private static Product Silica() =>
        new() { Name = "Silica", DefaultDose = 0.5m, Unit = DoseUnit.Millilitres };

    private CareLog Feed(CareKind kind = CareKind.Fertilised, params Product[] used) => new()
    {
        PlantId = plant,
        Kind = kind,
        OccurredOn = Today,
        Products = [.. used.Select(ProductDose.From)]
    };

    // The product

    [Fact]
    public void A_product_with_only_a_name_is_valid() =>
        Assert.Empty(new Product { Name = "Hydro fertiliser" }.Validate());

    [Fact]
    public void Validate_wants_a_name() =>
        Assert.Contains("Give the product a name.", new Product { Name = " " }.Validate());

    [Fact]
    public void Validate_rejects_a_dose_below_zero() =>
        Assert.Contains("A dose can't be less than 0.", new Product { Name = "Hydro", DefaultDose = -1 }.Validate());

    [Fact]
    public void The_usual_dose_reads_with_its_unit()
    {
        Assert.Equal("2 ml/L", Hydro().DoseText);
        Assert.Equal("1.5 g/L", new Product { Name = "Cal-mag", DefaultDose = 1.5m, Unit = DoseUnit.Grams }.DoseText);
        Assert.Null(new Product { Name = "Hydro" }.DoseText);
    }

    // A dose on an entry

    [Fact]
    public void Picking_a_product_starts_at_its_usual_dose()
    {
        var product = Hydro();

        var dose = ProductDose.From(product);

        Assert.Equal(product.Id, dose.ProductId);
        Assert.Equal("Hydro fertiliser", dose.Name);
        Assert.Equal(2, dose.Amount);
        Assert.Equal("Hydro fertiliser, 2 ml/L", dose.ToString());
    }

    [Fact]
    public void A_dose_with_no_amount_is_just_the_name() =>
        Assert.Equal("Hydro fertiliser", ProductDose.From(new Product { Name = "Hydro fertiliser" }).ToString());

    [Fact]
    public void Topping_up_can_have_a_product() =>
        Assert.Empty(Feed(CareKind.ToppedUp, Hydro()).Validate(Today));

    [Theory]
    [InlineData(CareKind.Watered)]
    [InlineData(CareKind.Repotted)]
    public void Only_a_feed_can_have_a_product(CareKind kind) =>
        Assert.Contains("Only fertilising and topping up can have a product.", Feed(kind, Hydro()).Validate(Today));

    [Fact]
    public void A_dose_on_an_entry_cant_be_below_zero()
    {
        var entry = Feed(CareKind.Fertilised, Hydro());
        entry.Products[0].Amount = -2;

        Assert.Contains("A dose can't be less than 0.", entry.Validate(Today));
    }

    [Fact]
    public void Copying_an_entry_copies_its_doses()
    {
        var entry = Feed(CareKind.Fertilised, Hydro());

        var copy = entry.Copy();
        copy.Products[0].Amount = 4;

        Assert.Equal(2, entry.Products[0].Amount);
    }

    // Describing

    [Fact]
    public void A_feed_reads_with_its_product_and_dose() =>
        Assert.Equal("Fertilised: Hydro fertiliser, 2 ml/L", CareService.Describe(Feed(CareKind.Fertilised, Hydro()), Label));

    [Fact]
    public void Several_products_read_in_the_order_they_went_in()
    {
        var entry = Feed(CareKind.Fertilised, Silica(), Hydro());
        entry.Notes = "half strength";

        Assert.Equal(
            "Fertilised: Silica, 0.5 ml/L + Hydro fertiliser, 2 ml/L · half strength",
            CareService.Describe(entry, Label));
    }

    // Logging

    [Fact]
    public async Task Each_plant_gets_its_own_copy_of_the_dose()
    {
        await care.LogManyAsync([plant, otherPlant], CareKind.Fertilised, Today, null, Label,
            products: [ProductDose.From(Hydro())]);

        Assert.Equal(2, logs.Logs.Count);
        Assert.All(logs.Logs, l => Assert.Equal("Hydro fertiliser, 2 ml/L", Assert.Single(l.Products).ToString()));

        logs.Logs[0].Products[0].Amount = 4;
        Assert.Equal(2, logs.Logs[1].Products[0].Amount);
    }

    [Fact]
    public async Task Changing_the_product_later_does_not_change_what_was_logged()
    {
        var product = Hydro();
        await products.SaveAsync(product);
        await care.LogManyAsync([plant], CareKind.Fertilised, Today, null, Label, products: [ProductDose.From(product)]);

        product.Name = "Hydro fertiliser (new bottle)";
        product.DefaultDose = 3;
        await products.SaveAsync(product);

        Assert.Equal("Fertilised: Hydro fertiliser, 2 ml/L", CareService.Describe(logs.Logs[0], Label));
    }

    [Fact]
    public async Task A_dose_changed_for_one_feed_is_what_gets_saved()
    {
        var dose = ProductDose.From(Hydro());
        dose.Amount = 1;

        await care.LogManyAsync([plant], CareKind.Fertilised, Today, null, Label, products: [dose]);

        Assert.Equal(1, logs.Logs[0].Products[0].Amount);
    }

    [Fact]
    public async Task Rows_without_a_name_are_dropped_and_names_trimmed()
    {
        await care.LogManyAsync([plant], CareKind.Fertilised, Today, null, Label,
            products: [new ProductDose { Name = "  Hydro fertiliser " }, new ProductDose { Name = " " }]);

        Assert.Equal("Hydro fertiliser", Assert.Single(logs.Logs[0].Products).Name);
    }

    [Fact]
    public async Task A_product_on_a_watering_saves_nothing() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            care.LogManyAsync([plant], CareKind.Watered, Today, null, Label, products: [ProductDose.From(Hydro())]));

    // Reading use back

    [Fact]
    public async Task Use_counts_plants_and_the_last_day()
    {
        var hydro = Hydro();
        var silica = Silica();
        await products.SaveAsync(hydro);
        await products.SaveAsync(silica);

        await care.LogManyAsync([plant, otherPlant], CareKind.Fertilised, Today.AddDays(-10), null, Label,
            products: [ProductDose.From(hydro)]);
        await care.LogManyAsync([plant], CareKind.ToppedUp, Today.AddDays(-2), null, Label,
            products: [ProductDose.From(hydro)]);

        var uses = await new ProductService(products, logs).GetAllAsync();

        // Sorted by name, and a product never logged says so
        Assert.Equal(["Hydro fertiliser", "Silica"], uses.Select(u => u.Product.Name));
        Assert.Equal(2, uses[0].Plants);
        Assert.Equal(Today.AddDays(-2), uses[0].LastUsed);
        Assert.Equal(0, uses[1].Plants);
        Assert.Null(uses[1].LastUsed);
    }

    [Fact]
    public void Deleted_entries_and_products_are_left_out()
    {
        var hydro = Hydro();
        var gone = Silica();
        gone.DeletedAt = Now;
        var deletedFeed = Feed(CareKind.Fertilised, hydro);
        deletedFeed.DeletedAt = Now;

        var uses = ProductUse.List([hydro, gone], [deletedFeed]);

        var use = Assert.Single(uses);
        Assert.Equal(0, use.Plants);
    }

    // Backup

    [Fact]
    public void Products_and_doses_survive_a_backup()
    {
        var hydro = Hydro();
        var backup = new BackupData { Products = [hydro], CareLogs = [Feed(CareKind.Fertilised, hydro)] };

        var json = JsonSerializer.Serialize(backup);
        var restored = JsonSerializer.Deserialize<BackupData>(json)!;

        Assert.Equal("Hydro fertiliser", Assert.Single(restored.Products).Name);
        var dose = Assert.Single(Assert.Single(restored.CareLogs).Products);
        Assert.Equal(hydro.Id, dose.ProductId);
        Assert.Equal("Hydro fertiliser, 2 ml/L", dose.ToString());
        Assert.Equal(1, restored.Counts.Products);
        // Stored as text, so reordering the units can't change what old data means
        Assert.Contains("\"Millilitres\"", json);
    }

    [Fact]
    public void A_care_entry_from_before_products_reads_with_none()
    {
        var json = $$"""{"PlantId":"{{plant}}","OccurredOn":"2026-09-01","Kind":"Fertilised","Notes":"Hydro fertiliser, 2 ml/L"}""";

        var entry = JsonSerializer.Deserialize<CareLog>(json)!;

        Assert.Empty(entry.Products);
        Assert.Equal("Fertilised: Hydro fertiliser, 2 ml/L", CareService.Describe(entry, Label));
    }
}
