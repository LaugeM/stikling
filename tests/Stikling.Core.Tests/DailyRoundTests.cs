using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class DailyRoundTests
{
    private static readonly DateOnly Today = new(2026, 4, 20);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Place living = new() { Name = "Living room", CreatedAt = Created };
    private readonly Place kitchen = new() { Name = "Kitchen", CreatedAt = Created.AddDays(1) };
    private readonly Place bedroom = new() { Name = "Bedroom", CreatedAt = Created.AddDays(2) };
    private readonly Place windowsill;
    private readonly List<Plant> plants = [];
    private readonly List<Propagation> propagations = [];
    private readonly List<CareLog> logs = [];
    private readonly Dictionary<Guid, DateTimeOffset> timeline = [];

    public DailyRoundTests() =>
        windowsill = new Place { Name = "Windowsill", RoomId = living.Id, CreatedAt = Created.AddDays(3) };

    private Places Places => new([living, kitchen, bedroom, windowsill]);

    private Plant PlantIn(Place place, DateOnly? caredFor = null)
    {
        var plant = new Plant { Nickname = "P", PlaceId = place.Id };
        plants.Add(plant);
        if (caredFor is { } day)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = day });
        return plant;
    }

    private DailyRoundSuggestion? Suggest(PutOffs? putOffs = null) =>
        DailyRound.Suggest(Places, plants, propagations, logs, timeline, putOffs ?? PutOffs.Empty(), Time);

    [Fact]
    public void The_room_that_has_gone_longest_without_anything_logged_wins()
    {
        PlantIn(living, Today.AddDays(-2));
        PlantIn(kitchen, Today.AddDays(-9));
        PlantIn(bedroom, Today.AddDays(-5));

        Assert.Equal(kitchen.Id, Suggest()!.Room.Id);
    }

    [Fact]
    public void The_newest_plant_in_a_room_decides_how_long_it_has_waited()
    {
        PlantIn(living, Today.AddDays(-20));
        PlantIn(living, Today.AddDays(-1));
        PlantIn(kitchen, Today.AddDays(-6));

        Assert.Equal(kitchen.Id, Suggest()!.Room.Id);
    }

    [Fact]
    public void Things_logged_today_are_ignored_so_the_suggestion_stays_the_same_all_day()
    {
        PlantIn(living, Today.AddDays(-9));
        PlantIn(kitchen, Today.AddDays(-5));
        var before = Suggest()!;

        logs.Add(new CareLog { PlantId = plants[0].Id, Kind = CareKind.MoistureReading, Moisture = 5, OccurredOn = Today });
        timeline[plants[0].Id] = Time.GetUtcNow();

        Assert.Equal(living.Id, before.Room.Id);
        Assert.Equal(living.Id, Suggest()!.Room.Id);
    }

    [Fact]
    public void A_room_with_nothing_logged_ever_counts_as_the_oldest()
    {
        PlantIn(living, Today.AddDays(-40));
        PlantIn(kitchen);

        Assert.Equal(kitchen.Id, Suggest()!.Room.Id);
    }

    [Fact]
    public void Timeline_activity_counts_for_a_plant_or_propagation_in_the_room()
    {
        PlantIn(living, Today.AddDays(-9));
        var kitchenPlant = PlantIn(kitchen, Today.AddDays(-20));
        var cutting = new Propagation { Nickname = "Cutting", PlaceId = bedroom.Id };
        propagations.Add(cutting);
        timeline[kitchenPlant.Id] = Time.GetUtcNow().AddDays(-2);
        timeline[cutting.Id] = Time.GetUtcNow().AddDays(-12);

        Assert.Equal(bedroom.Id, Suggest()!.Room.Id);
    }

    [Fact]
    public void Plants_in_spots_count_for_their_room_and_the_numbers_are_given()
    {
        PlantIn(windowsill, Today.AddDays(-9));
        PlantIn(living);
        propagations.Add(new Propagation { Nickname = "Cutting", PlaceId = windowsill.Id });
        PlantIn(kitchen, Today.AddDays(-3));

        var suggestion = Suggest()!;

        Assert.Equal(living.Id, suggestion.Room.Id);
        Assert.Equal(2, suggestion.Plants);
        Assert.Equal(1, suggestion.Propagations);
    }

    [Fact]
    public void A_room_put_off_is_skipped_and_the_next_is_suggested()
    {
        PlantIn(living, Today.AddDays(-9));
        PlantIn(kitchen, Today.AddDays(-5));
        PlantIn(bedroom, Today.AddDays(-2));
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Round(living.Id), Today.AddDays(1));

        Assert.Equal(kitchen.Id, Suggest(putOffs)!.Room.Id);

        putOffs.PutOff(PutOffs.Round(kitchen.Id), Today.AddDays(1));
        putOffs.PutOff(PutOffs.Round(bedroom.Id), Today.AddDays(1));
        Assert.Null(Suggest(putOffs));
    }

    [Fact]
    public void Putting_off_the_whole_round_gives_nothing()
    {
        PlantIn(living);
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.DailyRound, Today.AddDays(1));

        Assert.Null(Suggest(putOffs));
    }

    [Fact]
    public void Rooms_with_nothing_active_in_them_are_skipped()
    {
        PlantIn(living, Today.AddDays(-1));
        PlantIn(kitchen).Status = PlantStatus.Died;
        PlantIn(bedroom).DeletedAt = Created;
        propagations.Add(new Propagation { Nickname = "Done", PlaceId = bedroom.Id, Stage = PropagationStage.Done });

        Assert.Equal(living.Id, Suggest()!.Room.Id);
        Assert.Null(DailyRound.Suggest(Places, [], [], [], timeline, PutOffs.Empty(), Time));
    }

    [Fact]
    public void Care_on_a_plant_that_has_left_does_not_count()
    {
        PlantIn(living, Today.AddDays(-9));
        PlantIn(kitchen, Today.AddDays(-5));
        var gone = PlantIn(living, Today.AddDays(-1));
        gone.Status = PlantStatus.Sold;

        Assert.Equal(living.Id, Suggest()!.Room.Id);
    }

    [Fact]
    public void A_tie_goes_to_the_room_name()
    {
        PlantIn(living, Today.AddDays(-3));
        PlantIn(kitchen, Today.AddDays(-3));
        PlantIn(bedroom, Today.AddDays(-3));

        Assert.Equal(bedroom.Id, Suggest()!.Room.Id);
    }

    [Fact]
    public void The_due_plants_in_a_room_are_counted()
    {
        var inLiving = PlantIn(living);
        var inSpot = PlantIn(windowsill);
        var inKitchen = PlantIn(kitchen);
        var notYet = PlantIn(living);
        var guess = new WateringGuess(WateringForm.TopWatered, WateringMode.Interval, null, null, 7, 4, 0, Today, null, false, false);
        WateringDue[] due =
        [
            new(inLiving, guess, true), new(inSpot, guess, true), new(inKitchen, guess, true), new(notYet, guess, false)
        ];

        Assert.Equal(2, DailyRound.DueIn(living.Id, due, Places));
        Assert.Equal(1, DailyRound.DueIn(kitchen.Id, due, Places));
        Assert.Equal(0, DailyRound.DueIn(bedroom.Id, due, Places));
    }
}
