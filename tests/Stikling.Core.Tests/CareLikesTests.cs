using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class CareLikesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static Plant Monstera(Action<Plant>? set = null)
    {
        var plant = new Plant { Nickname = "Monstera", Genus = "Monstera", Species = "deliciosa" };
        set?.Invoke(plant);
        return plant;
    }

    [Fact]
    public void Fit_is_null_when_either_light_is_missing()
    {
        Assert.Null(CareLikes.Fit(new Plant { Light = LightLevel.Low }));
        Assert.Null(CareLikes.Fit(new Plant { LikesLight = LightLevel.Low }));
        Assert.Null(CareLikes.Fit(new Plant()));
    }

    [Theory]
    [InlineData(LightLevel.Low, LightLevel.BrightIndirect, LightFit.Darker)]
    [InlineData(LightLevel.DirectSun, LightLevel.Medium, LightFit.Brighter)]
    [InlineData(LightLevel.Medium, LightLevel.Medium, LightFit.Fits)]
    public void Fit_compares_where_it_stands_with_what_it_likes(LightLevel stands, LightLevel likes, LightFit expected)
    {
        Assert.Equal(expected, CareLikes.Fit(new Plant { Light = stands, LikesLight = likes }));
    }

    [Fact]
    public void HasAny_is_true_when_any_of_the_five_is_set()
    {
        Assert.False(CareLikes.HasAny(new Plant { Light = LightLevel.Low }));
        Assert.True(CareLikes.HasAny(new Plant { LikesLight = LightLevel.Low }));
        Assert.True(CareLikes.HasAny(new Plant { LikesWater = WaterNeed.DryOut }));
        Assert.True(CareLikes.HasAny(new Plant { LikesHumidity = HumidityNeed.Low }));
        Assert.True(CareLikes.HasAny(new Plant { GrowingSeason = GrowingSeason.NoRest }));
        Assert.True(CareLikes.HasAny(new Plant { RestHow = RestKind.Drier }));
    }

    [Fact]
    public void Changed_lists_the_fields_that_differ()
    {
        var before = Monstera(p => p.LikesWater = WaterNeed.DryOut);
        var after = before.Copy();
        after.LikesWater = WaterNeed.EvenlyMoist;
        after.LikesHumidity = HumidityNeed.High;
        after.GrowingSeason = GrowingSeason.Summer;
        after.RestHow = RestKind.Cooler;
        after.LikesLight = LightLevel.Medium;

        Assert.Equal(
            [LikesField.Light, LikesField.Water, LikesField.Humidity, LikesField.Season, LikesField.Rest],
            CareLikes.Changed(before, after));
        Assert.Empty(CareLikes.Changed(before, before.Copy()));
    }

    [Fact]
    public void Copy_sets_only_the_given_fields()
    {
        var from = Monstera(p =>
        {
            p.LikesLight = LightLevel.DirectSun;
            p.LikesWater = WaterNeed.EvenlyMoist;
            p.LikesHumidity = HumidityNeed.High;
        });
        var to = Monstera();

        CareLikes.Copy(from, to, [LikesField.Water, LikesField.Humidity]);

        Assert.Null(to.LikesLight);
        Assert.Equal(WaterNeed.EvenlyMoist, to.LikesWater);
        Assert.Equal(HumidityNeed.High, to.LikesHumidity);
    }

    [Fact]
    public void SameSpecies_ignores_case_spaces_and_cultivar()
    {
        var plant = Monstera();
        var thai = new Plant { Genus = " monstera ", Species = "DELICIOSA", Cultivar = "Thai Constellation" };
        var other = new Plant { Genus = "Monstera", Species = "adansonii" };

        Assert.Equal([thai], CareLikes.SameSpecies(plant, [plant, thai, other]));
    }

    [Fact]
    public void SameSpecies_leaves_out_deleted_and_gone_plants()
    {
        var plant = Monstera();
        var deleted = Monstera(p => p.DeletedAt = Now);
        var died = Monstera(p => p.Status = PlantStatus.Died);
        var sold = Monstera(p => p.Status = PlantStatus.Sold);
        var kept = Monstera();

        Assert.Equal([kept], CareLikes.SameSpecies(plant, [plant, deleted, died, sold, kept]));
    }

    [Fact]
    public void SameSpecies_is_empty_without_a_genus_and_species()
    {
        var noSpecies = new Plant { Genus = "Monstera" };
        var twin = new Plant { Genus = "Monstera" };
        var noGenus = new Plant { Species = "deliciosa" };

        Assert.Empty(CareLikes.SameSpecies(noSpecies, [twin]));
        Assert.Empty(CareLikes.SameSpecies(noGenus, [noGenus, new Plant { Species = "deliciosa" }]));
    }

    [Theory]
    [InlineData(GrowingSeason.Summer, true)]
    [InlineData(GrowingSeason.Winter, true)]
    [InlineData(GrowingSeason.NoRest, false)]
    [InlineData(null, false)]
    public void Normalize_keeps_how_it_rests_only_for_a_season_with_a_rest(GrowingSeason? season, bool kept)
    {
        var plant = Monstera(p =>
        {
            p.GrowingSeason = season;
            p.RestHow = RestKind.Drier;
        });

        CareLikes.Normalize(plant);

        Assert.Equal(kept ? RestKind.Drier : null, plant.RestHow);
    }

    [Fact]
    public void WouldChange_leaves_out_plants_a_way_of_resting_means_nothing_to()
    {
        var from = Monstera(p => { p.GrowingSeason = GrowingSeason.Summer; p.RestHow = RestKind.Cooler; });
        var rests = Monstera(p => { p.Nickname = "Rests"; p.GrowingSeason = GrowingSeason.Summer; });
        var noRest = Monstera(p => { p.Nickname = "No rest"; p.GrowingSeason = GrowingSeason.NoRest; });
        var same = Monstera(p => { p.Nickname = "Same"; p.GrowingSeason = GrowingSeason.Summer; p.RestHow = RestKind.Cooler; });

        var result = CareLikes.WouldChange(from, [LikesField.Rest], [rests, noRest, same]);

        Assert.Equal(["Rests"], result.Select(p => p.Nickname));
    }

    [Fact]
    public void The_fields_are_stored_as_text_and_older_plants_read_as_null()
    {
        var plant = Monstera(p =>
        {
            p.LikesLight = LightLevel.BrightIndirect;
            p.LikesWater = WaterNeed.DryPartly;
            p.LikesHumidity = HumidityNeed.High;
            p.GrowingSeason = GrowingSeason.Summer;
            p.RestHow = RestKind.CoolerAndDrier;
        });

        var json = JsonSerializer.Serialize(plant);
        Assert.Contains("\"LikesWater\":\"DryPartly\"", json);
        Assert.Contains("\"RestHow\":\"CoolerAndDrier\"", json);
        var back = JsonSerializer.Deserialize<Plant>(json)!;
        Assert.Equal(plant.LikesLight, back.LikesLight);
        Assert.Equal(plant.LikesWater, back.LikesWater);
        Assert.Equal(plant.LikesHumidity, back.LikesHumidity);
        Assert.Equal(plant.GrowingSeason, back.GrowingSeason);
        Assert.Equal(plant.RestHow, back.RestHow);

        // Backups carry them as part of the plant
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var backup = JsonSerializer.Serialize(new Stikling.Core.Backup.BackupData { Plants = [plant] }, options);
        Assert.Contains("\"growingSeason\":\"Summer\"", backup);
        var restored = JsonSerializer.Deserialize<Stikling.Core.Backup.BackupData>(backup, options)!.Plants[0];
        Assert.Equal(RestKind.CoolerAndDrier, restored.RestHow);
        Assert.Equal(WaterNeed.DryPartly, restored.LikesWater);

        var old = JsonSerializer.Deserialize<Plant>("{\"Nickname\":\"Old\"}")!;
        Assert.False(CareLikes.HasAny(old));
    }

    [Theory]
    [InlineData(LikesFilter.HighHumidity, "humid")]
    [InlineData(LikesFilter.StaysMoist, "moist")]
    [InlineData(LikesFilter.DriesOut, "dry")]
    [InlineData(LikesFilter.RestsInWinter, "winter")]
    [InlineData(LikesFilter.RestsInSummer, "summer")]
    [InlineData(LikesFilter.DarkerThanItLikes, "dark")]
    public void The_list_can_be_filtered_by_each_need(LikesFilter filter, string expected)
    {
        Plant[] all =
        [
            new() { Nickname = "humid", LikesHumidity = HumidityNeed.High },
            new() { Nickname = "moist", LikesWater = WaterNeed.EvenlyMoist },
            new() { Nickname = "dry", LikesWater = WaterNeed.DryOut },
            new() { Nickname = "winter", GrowingSeason = GrowingSeason.Summer },
            new() { Nickname = "summer", GrowingSeason = GrowingSeason.Winter },
            new() { Nickname = "dark", Light = LightLevel.Low, LikesLight = LightLevel.DirectSun },
            new() { Nickname = "bright", Light = LightLevel.DirectSun, LikesLight = LightLevel.Low },
            new() { Nickname = "plain" }
        ];

        var result = new PlantFilter(Likes: filter).Apply(all).Select(p => p.Nickname).ToList();

        Assert.Equal([expected], result);
    }

    [Fact]
    public void No_likes_filter_shows_every_plant()
    {
        Plant[] all = [new() { Nickname = "a" }, new() { Nickname = "b", LikesWater = WaterNeed.DryOut }];

        Assert.Equal(2, new PlantFilter().Apply(all).Count());
    }

    [Fact]
    public void Table_columns_sort_by_the_order_of_the_values_with_blanks_last()
    {
        Plant[] plants =
        [
            new() { Nickname = "Moist", LikesWater = WaterNeed.EvenlyMoist },
            new() { Nickname = "None" },
            new() { Nickname = "Dry", LikesWater = WaterNeed.DryOut }
        ];
        var rows = PlantTable.Rows(plants, [], Today, _ => null, _ => null);

        var up = PlantTable.Sort(rows, new PlantTableSort(PlantColumn.LikesWater));
        var down = PlantTable.Sort(rows, new PlantTableSort(PlantColumn.LikesWater, true));

        Assert.Equal(["Dry", "Moist", "None"], up.Select(r => r.Plant.Nickname));
        Assert.Equal(["Moist", "Dry", "None"], down.Select(r => r.Plant.Nickname));
    }

    [Fact]
    public void Table_columns_have_headers_and_survive_a_round_trip()
    {
        Assert.Equal("Likes light", PlantTable.Header(PlantColumn.LikesLight));
        Assert.Equal("Season", PlantTable.Header(PlantColumn.Season));

        var text = PlantTable.Write([PlantColumn.Season, PlantColumn.Medium, PlantColumn.LikesHumidity]);

        Assert.Equal("Name,Season,Medium,LikesHumidity", text);
        Assert.Equal(
            [PlantColumn.Name, PlantColumn.Season, PlantColumn.Medium, PlantColumn.LikesHumidity],
            PlantTable.Parse(text));
    }

    [Fact]
    public async Task ApplyLikes_copies_only_the_given_fields_and_saves()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var from = Monstera(p =>
        {
            p.LikesWater = WaterNeed.EvenlyMoist;
            p.LikesHumidity = HumidityNeed.High;
        });
        var other = Monstera(p => p.LikesHumidity = HumidityNeed.Low);
        await plants.SaveAsync(other);

        await service.ApplyLikesAsync(from, [LikesField.Water], [other]);

        var saved = plants.Plants[other.Id];
        Assert.Equal(WaterNeed.EvenlyMoist, saved.LikesWater);
        Assert.Equal(HumidityNeed.Low, saved.LikesHumidity);
    }

    [Fact]
    public async Task ApplyLikes_writes_nothing_to_the_timeline()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var service = new PlantService(plants, timeline, new FixedTime(Now));
        var from = Monstera(p => p.LikesWater = WaterNeed.DryOut);
        var other = Monstera();
        await plants.SaveAsync(other);

        await service.ApplyLikesAsync(from, [LikesField.Water], [other]);

        Assert.Empty(timeline.Entries);
    }

    private static PropagationService PropagationsWith(FakePlantRepository plants) => new(
        new FakePropagationRepository(), plants, new FakeTimelineRepository(),
        new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FixedTime(Now));

    [Fact]
    public async Task Potting_up_copies_what_the_parent_likes()
    {
        var plants = new FakePlantRepository();
        var parent = Monstera(p =>
        {
            p.LikesLight = LightLevel.BrightIndirect;
            p.LikesWater = WaterNeed.DryPartly;
            p.LikesHumidity = HumidityNeed.Medium;
            p.GrowingSeason = GrowingSeason.Winter;
            p.RestHow = RestKind.DiesBack;
        });
        plants.Plants[parent.Id] = parent;
        var service = PropagationsWith(plants);
        var cuttings = service.StartFrom(parent);
        cuttings.InitialCount = 2;
        await service.CreateAsync(cuttings, e => e.ToString());

        var made = await service.PotUpAsync(cuttings, new PotUpRequest(2, Today));

        Assert.All(made, p =>
        {
            Assert.Equal(LightLevel.BrightIndirect, p.LikesLight);
            Assert.Equal(WaterNeed.DryPartly, p.LikesWater);
            Assert.Equal(HumidityNeed.Medium, p.LikesHumidity);
            Assert.Equal(GrowingSeason.Winter, p.GrowingSeason);
            Assert.Equal(RestKind.DiesBack, p.RestHow);
        });
    }

    [Fact]
    public async Task Potting_up_without_a_parent_leaves_them_unset()
    {
        var plants = new FakePlantRepository();
        var service = PropagationsWith(plants);
        var seeds = new Propagation { Genus = "Ocimum", Type = PropagationType.Seed, StartedOn = Today };
        await service.CreateAsync(seeds, e => e.ToString());

        var made = await service.PotUpAsync(seeds, new PotUpRequest(1, Today));

        Assert.False(CareLikes.HasAny(made[0]));
    }

    [Fact]
    public async Task Potting_up_from_a_deleted_parent_leaves_them_unset()
    {
        var plants = new FakePlantRepository();
        var parent = Monstera(p => p.LikesWater = WaterNeed.DryOut);
        plants.Plants[parent.Id] = parent;
        var service = PropagationsWith(plants);
        var cuttings = service.StartFrom(parent);
        await service.CreateAsync(cuttings, e => e.ToString());
        parent.DeletedAt = Now;

        var made = await service.PotUpAsync(cuttings, new PotUpRequest(1, Today));

        Assert.Null(made[0].LikesWater);
    }
}
