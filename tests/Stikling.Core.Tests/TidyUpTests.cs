using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Tidy;

namespace Stikling.Core.Tests;

public class TidyUpTests
{
    private readonly FakePlaceRepository placeRepository = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakePhotoRepository photos = new();
    private readonly FakeProductRepository products = new();
    private readonly FakeCareLogRepository careLogs = new();
    private readonly FakePropagationCareLogRepository propagationCare = new();
    private readonly FakeFeedRepository feeds = new();
    private readonly FakeSupplyRepository supplies = new();
    private readonly FakeSoilMixRepository mixes = new();
    private readonly FakePutOffRepository putOffs = new();
    private readonly TidyService service;

    public TidyUpTests() =>
        service = new TidyService(plants, propagations, photos, placeRepository, products, careLogs, propagationCare, feeds, supplies, mixes, putOffs);

    private Place Room(string name = "Living room") => placeRepository.Add(name);

    private async Task<Plant> PlantAsync(string? nickname, string? genus = "Monstera", Place? room = null, string? species = null,
        bool photo = true, PlantStatus status = PlantStatus.Active, string? cultivar = null)
    {
        var plant = new Plant { Nickname = nickname, Genus = genus, Species = species, Cultivar = cultivar, PlaceId = room?.Id, Status = status };
        await plants.SaveAsync(plant);
        if (photo)
            await photos.AddAsync(new Photo { SubjectType = SubjectType.Plant, SubjectId = plant.Id });
        return plant;
    }

    [Fact]
    public async Task Nothing_to_tidy_gives_an_empty_report()
    {
        var room = Room();
        await PlantAsync("Big one", room: room);

        Assert.True((await service.GetAsync()).IsEmpty);
    }

    [Fact]
    public async Task Active_plants_without_a_genus_are_listed_and_gone_ones_are_not()
    {
        var room = Room();
        var bare = await PlantAsync("Mystery", genus: null, room: room);
        await PlantAsync("Dead mystery", genus: null, room: room, status: PlantStatus.Died);
        await PlantAsync("Named", room: room);

        Assert.Equal([bare], (await service.GetAsync()).NoGenus);
    }

    [Fact]
    public async Task Active_plants_without_a_photo_are_listed()
    {
        var room = Room();
        var bare = await PlantAsync("No photo", room: room, photo: false);
        await PlantAsync("Has photo", room: room);

        Assert.Equal([bare], (await service.GetAsync()).NoPhoto);
    }

    [Fact]
    public async Task Active_plants_without_a_room_are_listed()
    {
        var room = Room();
        var homeless = await PlantAsync("Homeless");
        await PlantAsync("Housed", room: room);

        Assert.Equal([homeless], (await service.GetAsync()).NoRoom);
    }

    [Fact]
    public async Task A_plant_that_lives_elsewhere_is_not_checked()
    {
        var elsewhere = new Plant { Nickname = "Gran's", LivesElsewhere = true };
        await plants.SaveAsync(elsewhere);

        Assert.True((await service.GetAsync()).IsEmpty);
    }

    [Fact]
    public async Task Propagations_whose_parent_plant_is_deleted_or_missing_are_listed()
    {
        var room = Room();
        var parent = await PlantAsync("Parent", room: room);
        var deleted = await PlantAsync("Deleted parent", room: room);
        var orphanOfDeleted = new Propagation { Nickname = "Cutting A", ParentPlantId = deleted.Id, StartedOn = new DateOnly(2026, 10, 1) };
        var orphanOfMissing = new Propagation { Nickname = "Cutting B", ParentPlantId = Guid.NewGuid(), StartedOn = new DateOnly(2026, 10, 1) };
        var fine = new Propagation { Nickname = "Cutting C", ParentPlantId = parent.Id, StartedOn = new DateOnly(2026, 10, 1) };
        var noParent = new Propagation { Nickname = "Seeds", StartedOn = new DateOnly(2026, 10, 1) };
        foreach (var p in new[] { orphanOfDeleted, orphanOfMissing, fine, noParent })
            await propagations.SaveAsync(p);
        await plants.DeleteAsync(deleted.Id);

        var listed = (await service.GetAsync()).ParentMissing;

        Assert.Equal(["Cutting A", "Cutting B"], listed.Select(p => p.DisplayName));
    }

    [Fact]
    public async Task Two_plants_in_the_same_room_with_the_same_nickname_may_be_the_same_ignoring_case_and_spaces()
    {
        var room = Room();
        var one = await PlantAsync("Basil", room: room);
        var two = await PlantAsync("  basil ", genus: "Ocimum", room: room);

        var pair = Assert.Single((await service.GetAsync()).MaybeSame);

        Assert.Equal(new[] { one.Id, two.Id }.Order(), new[] { pair.First.Id, pair.Second.Id });
        Assert.Equal(TidyChecks.SameKey(one.Id, two.Id), pair.Key);
    }

    [Fact]
    public async Task A_spot_counts_as_its_room_for_the_same_plant_check()
    {
        var room = Room();
        var spot = placeRepository.Add("Windowsill", room);
        await PlantAsync("Basil", room: room);
        await PlantAsync("Basil", room: spot);

        Assert.Single((await service.GetAsync()).MaybeSame);
    }

    [Fact]
    public async Task The_same_nickname_in_two_rooms_is_not_a_pair()
    {
        await PlantAsync("Basil", room: Room("Kitchen"));
        await PlantAsync("Basil", room: Room("Bedroom"));

        Assert.Empty((await service.GetAsync()).MaybeSame);
    }

    [Fact]
    public async Task Without_nicknames_the_same_genus_species_and_cultivar_may_be_the_same()
    {
        var room = Room();
        await PlantAsync(null, "Hoya", room, "carnosa", cultivar: "Krimson Queen");
        await PlantAsync(null, "hoya", room, "Carnosa", cultivar: "krimson queen");
        await PlantAsync(null, "Hoya", room, "carnosa");

        Assert.Single((await service.GetAsync()).MaybeSame);
    }

    [Fact]
    public async Task A_nickname_on_one_of_them_leaves_the_botanical_name_out_of_it()
    {
        var room = Room();
        await PlantAsync("Krimson", "Hoya", room, "carnosa");
        await PlantAsync(null, "Hoya", room, "carnosa");

        Assert.Empty((await service.GetAsync()).MaybeSame);
    }

    [Fact]
    public void Plants_with_no_name_at_all_are_not_taken_for_the_same()
    {
        var room = Room();
        var input = new TidyInput(
            [new Plant { PlaceId = room.Id }, new Plant { PlaceId = room.Id }], [], [], placeRepository.Current,
            [], [], [], [], [], [], []);

        Assert.Empty(TidyChecks.Build(input).MaybeSame);
    }

    [Fact]
    public async Task Not_the_same_stops_the_pair_showing_with_a_fixed_record()
    {
        var room = Room();
        var one = await PlantAsync("Basil", room: room);
        var two = await PlantAsync("Basil", room: room);

        await service.NotTheSameAsync(two.Id, one.Id);
        await service.NotTheSameAsync(one.Id, two.Id);

        Assert.Empty((await service.GetAsync()).MaybeSame);
        var saved = Assert.Single(putOffs.PutOffs.Values);
        Assert.Equal(DateOnly.MaxValue, saved.Until);
        Assert.Equal(PutOff.IdFor(saved.Key), saved.Id);
        var (low, high) = one.Id.CompareTo(two.Id) <= 0 ? (one.Id, two.Id) : (two.Id, one.Id);
        Assert.Equal($"tidy:same:{low}:{high}", saved.Key);
    }

    [Fact]
    public async Task Not_the_same_for_one_pair_leaves_the_other_pairs()
    {
        var room = Room();
        var a = await PlantAsync("Basil", room: room);
        var b = await PlantAsync("Basil", room: room);
        await PlantAsync("Basil", room: room);

        await service.NotTheSameAsync(a.Id, b.Id);

        Assert.Equal(2, (await service.GetAsync()).MaybeSame.Count);
    }

    [Fact]
    public async Task A_tag_only_on_plants_that_are_gone_is_unused_and_removing_it_leaves_other_tags()
    {
        var room = Room();
        var alive = await PlantAsync("Alive", room: room);
        alive.Tags = ["Rare"];
        await plants.SaveAsync(alive);
        var dead = await PlantAsync("Dead", room: room, status: PlantStatus.Died);
        dead.Tags = ["Rare", "Old shelf"];
        await plants.SaveAsync(dead);

        Assert.Equal(["Old shelf"], (await service.GetAsync()).UnusedTags);

        Assert.True(await service.RemoveTagAsync("Old shelf"));
        Assert.Equal(["Rare"], plants.Plants[dead.Id].Tags);
        Assert.False(await service.RemoveTagAsync("Rare"));
        Assert.Equal(["Rare"], plants.Plants[alive.Id].Tags);
    }

    [Fact]
    public async Task A_product_nothing_points_at_is_unused_and_any_reference_makes_it_used()
    {
        var plainUnused = new Product { Name = "Old spray" };
        var inCareLog = new Product { Name = "Feed A" };
        var inPropagationLog = new Product { Name = "Feed B" };
        var inFeed = new Product { Name = "Feed C" };
        var inStock = new Product { Name = "Feed D" };
        foreach (var p in new[] { plainUnused, inCareLog, inPropagationLog, inFeed, inStock })
            await products.SaveAsync(p);

        await careLogs.SaveAsync(new CareLog { PlantId = Guid.NewGuid(), Kind = CareKind.Fertilised, Products = [new ProductDose { ProductId = inCareLog.Id, Name = "Feed A" }] });
        await propagationCare.SaveAsync(new PropagationCareLog { PropagationId = Guid.NewGuid(), Products = [new ProductDose { ProductId = inPropagationLog.Id, Name = "Feed B" }] });
        await feeds.SaveAsync(new Feed { Name = "Mix", Products = [new ProductDose { ProductId = inFeed.Id, Name = "Feed C" }] });
        await supplies.SaveAsync(new Supply { ProductId = inStock.Id });

        var unused = (await service.GetAsync()).UnusedProducts;

        Assert.Equal(["Old spray"], unused.Select(p => p.Name));
        Assert.False(await service.RemoveProductAsync(inCareLog.Id));
        Assert.True(await service.RemoveProductAsync(plainUnused.Id));
        Assert.True(products.Products[plainUnused.Id].IsDeleted);
    }

    [Fact]
    public async Task A_soil_mix_nothing_is_in_is_unused()
    {
        var room = Room();
        var unused = new SoilMix { Name = "Old mix", Ingredients = [new MixIngredient { Name = "Bark" }] };
        var inPlant = new SoilMix { Name = "Aroid mix", Ingredients = [new MixIngredient { Name = "Bark" }] };
        var inPropagation = new SoilMix { Name = "Seed mix", Ingredients = [new MixIngredient { Name = "Sand" }] };
        foreach (var m in new[] { unused, inPlant, inPropagation })
            await mixes.SaveAsync(m);
        var plant = await PlantAsync("Basil", room: room);
        plant.SoilMixId = inPlant.Id;
        await plants.SaveAsync(plant);
        await propagations.SaveAsync(new Propagation { Nickname = "Cutting", SoilMixId = inPropagation.Id, StartedOn = new DateOnly(2026, 10, 1) });

        Assert.Equal([unused], (await service.GetAsync()).UnusedMixes);
        Assert.False(await service.RemoveMixAsync(inPlant.Id));
        Assert.True(await service.RemoveMixAsync(unused.Id));
        Assert.True(mixes.Mixes[unused.Id].IsDeleted);
    }
}
