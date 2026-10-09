using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class WateringRoundTests
{
    private static readonly DateOnly Today = new(2026, 4, 20);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));

    private readonly List<Plant> plants = [];
    private readonly List<CareLog> logs = [];

    // Four weekly waterings ending on the given day, so it is due a week after
    private Plant Weekly(string name, DateOnly last, int waterings = 4)
    {
        var plant = new Plant { Nickname = name };
        plants.Add(plant);
        for (var i = 0; i < waterings; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = last.AddDays(-7 * i) });
        return plant;
    }

    private IReadOnlyList<WateringDue> Due(PutOffs? putOffs = null, IEnumerable<GrowLight>? lights = null, Places? places = null) =>
        WateringRound.Due(plants, logs, [], null, lights ?? [], places ?? Places.None,
            Hemisphere.Northern, Time, putOffs ?? PutOffs.Empty());

    [Fact]
    public void Plants_that_are_due_come_first_the_longest_overdue_at_the_top_then_those_coming_up()
    {
        Weekly("Coming", Today.AddDays(-5));
        Weekly("Today", Today.AddDays(-7));
        Weekly("Late", Today.AddDays(-10));
        Weekly("Later", Today.AddDays(-2));

        var due = Due();

        Assert.Equal(["Late", "Today", "Coming"], due.Select(d => d.Plant.Nickname));
        Assert.Equal([true, true, false], due.Select(d => d.DueNow));
        Assert.Equal(Today.AddDays(-3), due[0].Guess.CheckOn);
    }

    [Fact]
    public void A_plants_own_guess_is_the_one_Today_has()
    {
        var plant = Weekly("Late", Today.AddDays(-10));
        Weekly("Other", Today.AddDays(-3));

        var guess = WateringRound.GuessFor(plant, plants, logs, [], null, [], Places.None, Hemisphere.Northern, Time);

        Assert.Equal(Due().Single(d => d.Plant.Id == plant.Id).Guess, guess);
    }

    [Fact]
    public void The_name_decides_between_two_on_the_same_day()
    {
        Weekly("Zamioculcas", Today.AddDays(-7));
        Weekly("Aloe", Today.AddDays(-7));

        Assert.Equal(["Aloe", "Zamioculcas"], Due().Select(d => d.Plant.Nickname));
    }

    [Fact]
    public void A_plant_still_learning_is_left_out()
    {
        Weekly("New", Today.AddDays(-20), waterings: 3);

        Assert.Empty(Due());
    }

    [Fact]
    public void A_plant_with_the_reminder_off_or_not_in_the_collection_is_left_out()
    {
        Weekly("Off", Today.AddDays(-10)).WateringReminder = false;
        Weekly("Died", Today.AddDays(-10)).Status = PlantStatus.Died;
        Weekly("Sold", Today.AddDays(-10)).Status = PlantStatus.Sold;
        Weekly("Gone", Today.AddDays(-10)).DeletedAt = DateTimeOffset.UtcNow;
        Weekly("On", Today.AddDays(-10));

        Assert.Equal(["On"], Due().Select(d => d.Plant.Nickname));
    }

    [Fact]
    public void A_plant_put_off_stays_hidden_until_it_comes_back_and_a_new_watering_ends_it()
    {
        var plant = Weekly("Hoya", Today.AddDays(-10));
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-10)), Today.AddDays(3));

        Assert.Empty(Due(putOffs));

        // Watered since, so the next time it is due is a new reminder
        Assert.NotEqual(PutOffs.Water(plant.Id, Today.AddDays(-10)), PutOffs.Water(plant.Id, Today));
        Assert.Contains("2026-04-10", PutOffs.Water(plant.Id, Today.AddDays(-10)));
    }

    [Fact]
    public void A_plant_under_a_light_ignores_the_season()
    {
        var room = new Place { Name = "Living room" };
        var plant = Weekly("Hoya", Today.AddDays(-10));
        plant.PlaceId = room.Id;
        var places = new Places([room]);
        var light = new GrowLight { Name = "Lamp", PlaceId = room.Id };

        Assert.True(Due(lights: [light], places: places).Single().Guess.CheckOn <= Today);
    }
}
