using System.Globalization;
using System.Text.Json;
using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;
using Stikling.Core.Settings;
using Stikling.Core.Text;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

/// <summary>Water type, pot weights and harvest amounts. They show numbers, so they run with the other tests of the display choices.</summary>
[Collection("Display choices")]
public sealed class WeighingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly CultureInfo was = CultureInfo.CurrentCulture;

    public WeighingTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        Lengths.Unit = LengthUnit.Centimetres;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = was;
        Lengths.Unit = LengthUnit.Centimetres;
    }

    private static CareLog Harvest(Guid plantId, DateOnly on, decimal? amount = null, HarvestUnit? unit = null) =>
        new() { PlantId = plantId, Kind = CareKind.Harvested, OccurredOn = on, HarvestAmount = amount, HarvestUnit = unit };

    // Weights

    [Fact]
    public void Grams_are_shown_with_a_thousands_separator()
    {
        Assert.Equal("840 g", Weights.Grams(840));
        Assert.Equal("1,320 g", Weights.Grams(1320));
        Assert.Equal("40.5 g", Weights.Grams(40.5m));
    }

    [Fact]
    public void Ounces_are_shown_with_one_decimal_when_lengths_are_in_inches()
    {
        Lengths.Unit = LengthUnit.Inches;

        Assert.Equal("oz", Weights.UnitName);
        Assert.Equal("29.6 oz", Weights.Grams(840));
        Assert.Equal("46.6 oz", Weights.Grams(1320));
    }

    [Fact]
    public void A_number_typed_in_ounces_is_stored_as_grams_and_comes_back_the_same()
    {
        Lengths.Unit = LengthUnit.Inches;

        Assert.Equal(29.6m, Weights.ToInput(Weights.FromInput(29.6m)));
        Assert.Equal(839, Weights.WholeFromInput(29.6m));
        Assert.Equal(840, Weights.WholeFromInput(840m / 28.349523125m));
    }

    [Fact]
    public void A_number_typed_in_grams_is_stored_as_it_is()
    {
        Assert.Equal(840m, Weights.FromInput(840m));
        Assert.Equal(840m, Weights.ToInput(840m));
        Assert.Equal(841, Weights.WholeFromInput(840.5m));
    }

    [Theory]
    [InlineData("840", 840)]
    [InlineData(" 40,5 ", 40.5)]
    [InlineData("40.5", 40.5)]
    public void Typed_text_is_read_with_a_point_or_a_comma(string text, double grams)
    {
        Assert.True(Weights.TryParse(text, out var parsed));
        Assert.Equal((decimal)grams, parsed);
    }

    [Fact]
    public void An_empty_box_is_no_weight_and_text_that_is_not_a_number_is_refused()
    {
        Assert.True(Weights.TryParse("  ", out var none));
        Assert.Null(none);
        Assert.False(Weights.TryParse("heavy", out _));
    }

    // Plant validation

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100001)]
    public void A_weight_has_to_be_above_0_and_at_most_100_kg(int grams)
    {
        Assert.Contains("A weight has to be above 0 and at most 100 kg.", new Plant { Nickname = "Fern", DryWeightGrams = grams }.Validate(Today));
        Assert.Contains("A weight has to be above 0 and at most 100 kg.", new Plant { Nickname = "Fern", WateredWeightGrams = grams }.Validate(Today));
    }

    [Fact]
    public void The_dry_weight_has_to_be_below_the_watered_weight()
    {
        const string error = "The weight when dry has to be less than the weight just watered.";

        Assert.Contains(error, new Plant { Nickname = "Fern", DryWeightGrams = 900, WateredWeightGrams = 900 }.Validate(Today));
        Assert.Contains(error, new Plant { Nickname = "Fern", DryWeightGrams = 1000, WateredWeightGrams = 900 }.Validate(Today));
        Assert.Empty(new Plant { Nickname = "Fern", DryWeightGrams = 840, WateredWeightGrams = 1320 }.Validate(Today));
        Assert.Empty(new Plant { Nickname = "Fern", DryWeightGrams = 100000 }.Validate(Today));
        Assert.Empty(new Plant { Nickname = "Fern", WateredWeightGrams = 1 }.Validate(Today));
    }

    [Fact]
    public async Task The_form_dates_the_weighing_today_and_clears_it_when_both_weights_are_emptied()
    {
        var plants = new FakePlantRepository();
        var stored = new Plant { Nickname = "Fern", DryWeightGrams = 800, WeighedOn = new DateOnly(2026, 1, 5) };
        await plants.SaveAsync(stored);
        var forms = FormsFor(plants);
        var form = (await forms.EditAsync(stored.Id, e => e.ToString()))!;

        form.SetDryWeight(800);
        Assert.Equal(new DateOnly(2026, 1, 5), form.Plant.WeighedOn);

        form.SetWateredWeight(1200);
        Assert.Equal(Today, form.Plant.WeighedOn);

        form.SetDryWeight(null);
        Assert.Equal(Today, form.Plant.WeighedOn);

        form.SetWateredWeight(null);
        Assert.Null(form.Plant.WeighedOn);
    }

    private static PlantForms FormsFor(FakePlantRepository plants)
    {
        var time = new FixedTime(Now);
        var propagations = new FakePropagationRepository();
        var settingsRepository = new FakeSettingsRepository();
        var settings = new SettingsService(settingsRepository);
        var places = new FakePlaceRepository();
        return new PlantForms(
            plants,
            propagations,
            new FakePotRepository(),
            new FakeSoilMixRepository(),
            new FakeFeedRepository(),
            new FakePestTreatmentRepository(),
            settings,
            new PlantService(plants, new FakeTimelineRepository(), time),
            new Wishlist.WishService(new FakeWishRepository(), new FakePhotoRepository(), time),
            new Names.PlantNameService(new FakePlantNameSource(Names.PlantNameData.Empty), plants, propagations, settings, new FakeDeviceLanguages()),
            new RoomService(places, plants, propagations),
            new Sellers.SellerService(new FakeSellerRepository(), plants, propagations),
            new Fields.OwnFieldService(new FakeOwnFieldRepository(), plants),
            time);
    }

    // Old records

    [Fact]
    public void A_plant_saved_before_these_fields_reads_them_as_not_set()
    {
        var plant = JsonSerializer.Deserialize<Plant>("""{"Nickname":"Fern"}""")!;

        Assert.Null(plant.WaterType);
        Assert.Null(plant.DryWeightGrams);
        Assert.Null(plant.WateredWeightGrams);
        Assert.Null(plant.WeighedOn);
    }

    [Fact]
    public void A_water_type_this_version_does_not_know_reads_as_not_set()
    {
        Assert.Equal(WaterType.Rain, JsonSerializer.Deserialize<Plant>("""{"WaterType":"Rain"}""")!.WaterType);
        Assert.Null(JsonSerializer.Deserialize<Plant>("""{"WaterType":"Snow"}""")!.WaterType);
        Assert.Contains("\"WaterType\":\"Demineralised\"", JsonSerializer.Serialize(new Plant { WaterType = WaterType.Demineralised }));
    }

    [Fact]
    public void A_care_entry_saved_before_harvest_amounts_reads_them_as_not_set_and_an_unknown_unit_is_not_set()
    {
        var old = JsonSerializer.Deserialize<CareLog>("""{"Kind":"Harvested"}""")!;
        Assert.Null(old.HarvestAmount);
        Assert.Null(old.HarvestUnit);

        Assert.Null(JsonSerializer.Deserialize<CareLog>("""{"Kind":"Harvested","HarvestAmount":2,"HarvestUnit":"Bunches"}""")!.HarvestUnit);
        Assert.Equal(HarvestUnit.Pieces, JsonSerializer.Deserialize<CareLog>("""{"Kind":"Harvested","HarvestAmount":2,"HarvestUnit":"Pieces"}""")!.HarvestUnit);
    }

    // Water type and weights as text

    [Fact]
    public void Only_water_other_than_tap_gets_a_note_or_a_line_on_the_sitter_sheet()
    {
        Assert.Equal("Rain water", PotWeight.Note(WaterType.Rain));
        Assert.Equal("Demineralised water", PotWeight.Note(WaterType.Demineralised));
        Assert.Null(PotWeight.Note(WaterType.Tap));
        Assert.Null(PotWeight.Note(null));

        Assert.Equal("Use rain water", PotWeight.SitterLine(WaterType.Rain));
        Assert.Equal("Use demineralised water", PotWeight.SitterLine(WaterType.Demineralised));
        Assert.Null(PotWeight.SitterLine(WaterType.Tap));
        Assert.Null(PotWeight.SitterLine(null));

        Assert.Equal("Gets rain water", PotWeight.Gets(WaterType.Rain));
        Assert.Null(PotWeight.Gets(null));
    }

    [Fact]
    public void The_weights_read_with_either_part_alone()
    {
        Assert.Equal("Dry 840 g · just watered 1,320 g", PotWeight.Line(new Plant { DryWeightGrams = 840, WateredWeightGrams = 1320 }));
        Assert.Equal("Dry 840 g", PotWeight.Line(new Plant { DryWeightGrams = 840 }));
        Assert.Equal("Just watered 1,320 g", PotWeight.Line(new Plant { WateredWeightGrams = 1320 }));
        Assert.Null(PotWeight.Line(new Plant()));
        Assert.Equal("Dry at 840 g", PotWeight.DryAt(new Plant { DryWeightGrams = 840, WateredWeightGrams = 1320 }));
        Assert.Null(PotWeight.DryAt(new Plant { WateredWeightGrams = 1320 }));
    }

    [Fact]
    public void A_plant_weighed_before_it_was_repotted_is_told_so()
    {
        var plant = new Plant { DryWeightGrams = 840, WeighedOn = new DateOnly(2026, 9, 1) };
        var repotted = new CareLog { PlantId = plant.Id, Kind = CareKind.Repotted, OccurredOn = new DateOnly(2026, 9, 2) };

        Assert.True(PotWeight.WeighedBeforeRepot(plant, [repotted]));
        Assert.False(PotWeight.WeighedBeforeRepot(plant, []));
        Assert.False(PotWeight.WeighedBeforeRepot(plant, [new CareLog { PlantId = plant.Id, Kind = CareKind.Repotted, OccurredOn = plant.WeighedOn!.Value }]));
        Assert.False(PotWeight.WeighedBeforeRepot(plant, [new CareLog { PlantId = Guid.NewGuid(), Kind = CareKind.Repotted, OccurredOn = Today }]));
        Assert.False(PotWeight.WeighedBeforeRepot(plant, [new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today }]));
        Assert.False(PotWeight.WeighedBeforeRepot(new Plant(), [repotted]));
    }

    // Harvest validation

    [Fact]
    public void A_harvest_can_have_an_amount_and_a_unit()
    {
        Assert.Empty(Harvest(Guid.NewGuid(), Today, 40, HarvestUnit.Grams).Validate(Today));
        Assert.Empty(Harvest(Guid.NewGuid(), Today, 0.5m, HarvestUnit.Pieces).Validate(Today));
        Assert.Empty(Harvest(Guid.NewGuid(), Today).Validate(Today));
    }

    [Fact]
    public void An_amount_has_to_be_above_0_and_come_with_a_unit()
    {
        Assert.Contains("The amount harvested has to be above 0.", Harvest(Guid.NewGuid(), Today, 0, HarvestUnit.Grams).Validate(Today));
        Assert.Contains("The amount harvested has to be above 0.", Harvest(Guid.NewGuid(), Today, -3, HarvestUnit.Pieces).Validate(Today));
        Assert.Contains("Pick grams or pieces for the amount.", Harvest(Guid.NewGuid(), Today, 40).Validate(Today));
        Assert.Contains("Write how much was harvested.", Harvest(Guid.NewGuid(), Today, null, HarvestUnit.Grams).Validate(Today));
    }

    [Fact]
    public void Only_a_built_in_harvest_has_an_amount()
    {
        const string error = "Only a harvest can have an amount.";
        var watered = new CareLog { PlantId = Guid.NewGuid(), Kind = CareKind.Watered, OccurredOn = Today, HarvestAmount = 4, HarvestUnit = HarvestUnit.Grams };
        var own = Harvest(Guid.NewGuid(), Today, 4, HarvestUnit.Grams);
        own.Kind = CareKind.Rotated;
        own.OwnKindId = Guid.NewGuid();
        own.OwnKindName = "Mist";

        Assert.Contains(error, watered.Validate(Today));
        Assert.Contains(error, own.Validate(Today));
    }

    // Harvest text and totals

    [Fact]
    public void An_amount_reads_in_grams_or_pieces()
    {
        Assert.Equal("40 g", Harvests.Amount(40, HarvestUnit.Grams));
        Assert.Equal("6 pieces", Harvests.Amount(6, HarvestUnit.Pieces));
        Assert.Equal("1 piece", Harvests.Amount(1, HarvestUnit.Pieces));

        Lengths.Unit = LengthUnit.Inches;
        Assert.Equal("1.4 oz", Harvests.Amount(40, HarvestUnit.Grams));
        Assert.Equal("6 pieces", Harvests.Amount(6, HarvestUnit.Pieces));
    }

    [Fact]
    public async Task A_harvest_is_described_with_its_amount_or_just_as_harvested()
    {
        var logs = new FakeCareLogRepository();
        var service = new CareService(logs, new FakeTimelineRepository(), new FakePlantRepository(), new FakeFeedRepository(),
            new FakeProductRepository(), new FakeOwnCareKindRepository(), new FixedTime(Now));
        var plant = Guid.NewGuid();

        Assert.Equal("Harvested 40 g", CareService.Describe(Harvest(plant, Today, 40, HarvestUnit.Grams), e => e.ToString()));
        Assert.Equal("Harvested 6 pieces", CareService.Describe(Harvest(plant, Today, 6, HarvestUnit.Pieces), e => e.ToString()));
        Assert.Equal("Harvested", CareService.Describe(Harvest(plant, Today), e => e.ToString()));

        var withNote = Harvest(plant, Today, 40, HarvestUnit.Grams);
        withNote.Notes = "Top of the pot";
        Assert.Equal("Harvested 40 g: Top of the pot", CareService.Describe(withNote, e => e.ToString()));

        await service.LogManyAsync([plant], CareKind.Harvested, Today, null, e => e.ToString(), harvestAmount: 40, harvestUnit: HarvestUnit.Grams);
        Assert.Equal((40m, HarvestUnit.Grams), (logs.Logs[0].HarvestAmount, logs.Logs[0].HarvestUnit));
    }

    [Fact]
    public async Task A_harvest_can_be_logged_without_an_amount_and_the_amount_can_be_fixed_later()
    {
        var logs = new FakeCareLogRepository();
        var service = new CareService(logs, new FakeTimelineRepository(), new FakePlantRepository(), new FakeFeedRepository(),
            new FakeProductRepository(), new FakeOwnCareKindRepository(), new FixedTime(Now));
        var plant = Guid.NewGuid();

        await service.LogManyAsync([plant], CareKind.Harvested, Today, null, e => e.ToString(), harvestUnit: HarvestUnit.Grams);
        var entry = logs.Logs[0];
        Assert.Null(entry.HarvestAmount);
        Assert.Null(entry.HarvestUnit);

        await service.UpdateAsync(new CareLog { Id = entry.Id, OccurredOn = Today, HarvestAmount = 12, HarvestUnit = HarvestUnit.Pieces }, Today);
        Assert.Equal((12m, HarvestUnit.Pieces), (logs.Logs[0].HarvestAmount, logs.Logs[0].HarvestUnit));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(new CareLog { Id = entry.Id, OccurredOn = Today, HarvestAmount = 0, HarvestUnit = HarvestUnit.Pieces }, Today));
    }

    [Fact]
    public void The_year_adds_up_grams_and_pieces_apart_and_counts_every_harvest()
    {
        var plant = Guid.NewGuid();
        var other = Guid.NewGuid();
        List<CareLog> logs =
        [
            Harvest(plant, new DateOnly(2026, 3, 1), 40, HarvestUnit.Grams),
            Harvest(plant, new DateOnly(2026, 4, 1), 100, HarvestUnit.Grams),
            Harvest(plant, new DateOnly(2026, 5, 1), 5, HarvestUnit.Pieces),
            Harvest(plant, new DateOnly(2026, 6, 1), 7, HarvestUnit.Pieces),
            Harvest(plant, new DateOnly(2026, 7, 1)),
            Harvest(plant, new DateOnly(2026, 8, 1)),
            Harvest(plant, new DateOnly(2025, 12, 31), 500, HarvestUnit.Grams),
            Harvest(other, new DateOnly(2026, 3, 1), 900, HarvestUnit.Grams),
            new CareLog { PlantId = plant, Kind = CareKind.Watered, OccurredOn = new DateOnly(2026, 3, 1) }
        ];
        var gone = Harvest(plant, new DateOnly(2026, 9, 1), 1000, HarvestUnit.Grams);
        gone.DeletedAt = Now;
        logs.Add(gone);

        var year = Harvests.ThisYear(logs, plant, Today);

        Assert.Equal(new HarvestYear(6, 140, 12), year);
        Assert.Equal("This year: 140 g and 12 pieces over 6 harvests", year.Text());
    }

    [Fact]
    public void The_year_leaves_out_the_parts_that_are_zero()
    {
        var plant = Guid.NewGuid();

        Assert.Equal("This year: 40 g over 2 harvests",
            Harvests.ThisYear([Harvest(plant, new DateOnly(2026, 3, 1), 40, HarvestUnit.Grams), Harvest(plant, new DateOnly(2026, 4, 1))], plant, Today).Text());
        Assert.Equal("This year: 6 pieces from 1 harvest",
            Harvests.ThisYear([Harvest(plant, new DateOnly(2026, 3, 1), 6, HarvestUnit.Pieces)], plant, Today).Text());
        Assert.Equal("This year: 2 harvests",
            Harvests.ThisYear([Harvest(plant, new DateOnly(2026, 3, 1)), Harvest(plant, new DateOnly(2026, 4, 1))], plant, Today).Text());
        Assert.Equal("", Harvests.ThisYear([], plant, Today).Text());
    }

    [Fact]
    public void The_unit_starts_on_the_one_the_last_harvest_used()
    {
        var plant = Guid.NewGuid();

        Assert.Equal(HarvestUnit.Grams, Harvests.LastUnit([], plant));
        Assert.Equal(HarvestUnit.Pieces, Harvests.LastUnit(
            [Harvest(plant, new DateOnly(2026, 3, 1), 40, HarvestUnit.Grams), Harvest(plant, new DateOnly(2026, 4, 1), 6, HarvestUnit.Pieces), Harvest(plant, new DateOnly(2026, 5, 1))],
            plant));
        Assert.Equal(HarvestUnit.Grams, Harvests.LastUnit([Harvest(Guid.NewGuid(), Today, 6, HarvestUnit.Pieces)], plant));
    }

    // The sitter sheet

    private static SitterSheetView Sheet(SitterSheet sheet, params Plant[] plants)
    {
        var stretches = new[] { new AwayStretch { From = new DateOnly(2026, 10, 20), Until = new DateOnly(2026, 11, 3), Kind = AwayKind.Away } };
        return SitterSheetBuilder.Build(sheet, stretches, plants, [], [], [], null, [], new Places([]), Hemisphere.Northern, new FixedTime(Now));
    }

    [Fact]
    public void The_sitter_sheet_carries_the_water_type_and_the_weights()
    {
        var fern = new Plant { Nickname = "Sundew", WaterType = WaterType.Rain, DryWeightGrams = 840, WateredWeightGrams = 1320 };
        var view = Sheet(new SitterSheet { Visits = VisitPattern.Weekly }, fern);
        var subject = view.Subjects.Single();

        Assert.Equal("Use rain water", subject.Water);
        Assert.Equal("To check by lifting: about 840 g when dry, 1,320 g just watered.", subject.Weight);

        var text = SitterSheetText.Write(view);
        Assert.Contains("Use rain water", text);
        Assert.Contains("To check by lifting: about 840 g when dry, 1,320 g just watered.", text);
    }

    [Fact]
    public void Tap_water_and_a_plant_that_is_not_weighed_add_nothing_to_the_sitter_sheet()
    {
        var view = Sheet(new SitterSheet { Visits = VisitPattern.Weekly },
            new Plant { Nickname = "Tap", WaterType = WaterType.Tap },
            new Plant { Nickname = "Plain" });

        Assert.All(view.Subjects, s => Assert.Null(s.Water));
        Assert.All(view.Subjects, s => Assert.Null(s.Weight));
        Assert.DoesNotContain("lifting", SitterSheetText.Write(view));
    }

    [Fact]
    public void The_water_type_goes_with_how_it_is_watered_but_the_weight_does_not()
    {
        var plant = new Plant { Nickname = "Sundew", WaterType = WaterType.Demineralised, DryWeightGrams = 500 };
        var view = Sheet(new SitterSheet { Visits = VisitPattern.Weekly, HideHowWatered = true }, plant);

        var subject = view.Subjects.Single();
        Assert.Null(subject.Water);
        Assert.Equal("To check by lifting: about 500 g when dry.", subject.Weight);
    }

    [Fact]
    public void Nothing_on_the_sheet_says_when_to_water()
    {
        var plant = new Plant { Nickname = "Sundew", WaterType = WaterType.Rain, DryWeightGrams = 840, WateredWeightGrams = 1320 };
        var text = SitterSheetText.Write(Sheet(new SitterSheet(), plant));

        Assert.DoesNotContain("water it when", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("To check by lifting", text);
    }
}
