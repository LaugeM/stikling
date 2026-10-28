using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class BackListTests
{
    private static readonly DateOnly Back = new(2026, 10, 12);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    private readonly List<Plant> plants = [];
    private readonly List<Propagation> propagations = [];
    private readonly List<CareLog> logs = [];
    private readonly List<TimelineEntry> timeline = [];
    private readonly List<PestCase> cases = [];
    private readonly List<Place> placeList = [];

    private Plant Plant(string name, Place? place = null)
    {
        var plant = new Plant { Nickname = name, PlaceId = place?.Id };
        plants.Add(plant);
        return plant;
    }

    private Propagation Cutting(string name, GrowingMedium medium = GrowingMedium.Water, int startedDaysAgo = 20)
    {
        var cutting = new Propagation { Nickname = name, Medium = medium, StartedOn = Back.AddDays(-startedDaysAgo) };
        propagations.Add(cutting);
        return cutting;
    }

    // Weekly waterings, the last one this many days before the back day
    private Plant Weekly(string name, int lastDaysAgo)
    {
        var plant = Plant(name);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Back.AddDays(-lastDaysAgo - 7 * i) });
        return plant;
    }

    private PestCase CaseOn(Plant plant, int startedDaysAgo)
    {
        var item = new PestCase { Scope = PestScope.PickedPlants, PlantIds = [plant.Id], StartedOn = Back.AddDays(-startedDaysAgo) };
        cases.Add(item);
        return item;
    }

    private static void Flag(Entity subject, int sinceDaysAgo)
    {
        var attention = new Attention("Repot", Back.AddDays(-sinceDaysAgo));
        if (subject is Plant plant)
            plant.Attention = attention;
        else
            ((Propagation)subject).Attention = attention;
    }

    private void LookOver(Entity subject, DateOnly day, SubjectType type = SubjectType.Plant) =>
        timeline.Add(new TimelineEntry
        {
            SubjectType = type, SubjectId = subject.Id, Kind = TimelineKind.Change, Event = TimelineEvent.LookedOver,
            OccurredAt = new DateTimeOffset(day.Year, day.Month, day.Day, 15, 0, 0, TimeSpan.Zero)
        });

    private IReadOnlyList<BackRow> List(int? length = 0) =>
        BackList.Of(Back, plants, propagations, logs, timeline, cases, null, [], new Places(placeList), [],
            Hemisphere.Northern, Time, length);

    [Fact]
    public void The_list_goes_pests_then_cuttings_in_water_then_past_the_pace_then_flagged()
    {
        var flagged = Plant("Flagged");
        Flag(flagged, 3);
        Weekly("Dry", lastDaysAgo: 20);
        var cutting = Cutting("Pilea");
        var pest = Plant("Pest");
        CaseOn(pest, 5);

        var list = List();

        Assert.Equal(["Pest", "Pilea", "Dry", "Flagged"], list.Select(r => r.Name));
        Assert.Equal([BackReason.Pest, BackReason.InWater, BackReason.PastPace, BackReason.Flagged], list.Select(r => r.Reason));
        Assert.Same(cutting, list[1].Subject);
    }

    [Fact]
    public void Each_row_carries_what_its_line_needs()
    {
        var pest = Plant("Pest");
        CaseOn(pest, 5).Pest = Pest.Thrips;
        var cutting = Cutting("Pilea");
        logs.Add(new CareLog { PlantId = cutting.Id, Kind = CareKind.Watered, OccurredOn = Back.AddDays(-4) });
        Weekly("Dry", lastDaysAgo: 20);
        Flag(Plant("Flagged"), 9);

        var list = List();

        Assert.Equal((Pest.Thrips, Back.AddDays(-5)), (list[0].Pest, list[0].CaseStart));
        Assert.Equal(4, list[1].DaysSinceLogged);
        Assert.Equal((7, 20), (list[2].UsualDays, list[2].DaysSince));
        Assert.Equal(("Repot", Back.AddDays(-9)), (list[3].FlagReason, list[3].FlagSince));
    }

    [Fact]
    public void The_pest_tier_starts_with_the_oldest_case_and_a_room_covers_its_spots()
    {
        var room = new Place { Name = "Living room" };
        var spot = new Place { Name = "Shelf", RoomId = room.Id };
        placeList.AddRange([room, spot]);
        var inSpot = Plant("In the spot", spot);
        var picked = Plant("Picked");
        cases.Add(new PestCase { Scope = PestScope.Room, PlaceId = room.Id, StartedOn = Back.AddDays(-2) });
        CaseOn(picked, 8);

        Assert.Equal(["Picked", "In the spot"], List().Select(r => r.Name));
        Assert.All(List(), r => Assert.Equal(BackReason.Pest, r.Reason));
        Assert.Contains(List(), r => r.Subject == inSpot);
    }

    [Fact]
    public void An_everywhere_case_puts_nothing_in_the_pest_tier()
    {
        var plant = Plant("Anywhere");
        Flag(plant, 2);
        cases.Add(new PestCase { Scope = PestScope.Everywhere, StartedOn = Back.AddDays(-5) });

        var row = Assert.Single(List());

        Assert.Equal(BackReason.Flagged, row.Reason);
    }

    [Fact]
    public void A_resolved_case_does_not_count()
    {
        var plant = Plant("Better");
        CaseOn(plant, 20).Status = PestCaseStatus.Resolved;

        Assert.Empty(List());
    }

    [Fact]
    public void A_subject_shows_once_at_its_first_tier()
    {
        var plant = Weekly("Everything", lastDaysAgo: 20);
        Flag(plant, 4);
        CaseOn(plant, 3);
        var cutting = Cutting("Cutting");
        Flag(cutting, 6);

        var list = List();

        Assert.Equal(2, list.Count);
        Assert.Equal(BackReason.Pest, list.Single(r => r.Subject == plant).Reason);
        Assert.Equal(BackReason.InWater, list.Single(r => r.Subject == cutting).Reason);
    }

    [Fact]
    public void Cuttings_in_water_start_with_the_longest_since_anything_was_logged()
    {
        var recent = Cutting("Recent", startedDaysAgo: 30);
        LookOver(recent, Back.AddDays(-2), SubjectType.Propagation);
        Cutting("Oldest", startedDaysAgo: 15);
        Cutting("In soil", GrowingMedium.Soil, startedDaysAgo: 40);

        var list = List();

        Assert.Equal(["Oldest", "Recent"], list.Select(r => r.Name));
        Assert.Equal([15, 2], list.Select(r => r.DaysSinceLogged!.Value));
    }

    [Fact]
    public void A_cutting_that_is_finished_is_not_listed()
    {
        Cutting("Done").Stage = PropagationStage.Done;

        Assert.Empty(List());
    }

    [Fact]
    public void Plants_past_their_pace_start_with_the_furthest_past_it_in_proportion()
    {
        Weekly("A bit late", lastDaysAgo: 9);
        Weekly("Very late", lastDaysAgo: 28);
        Weekly("On time", lastDaysAgo: 5);

        Assert.Equal(["Very late", "A bit late"], List().Select(r => r.Name));
    }

    [Fact]
    public void A_plant_still_learning_has_no_pace_to_be_past()
    {
        var plant = Plant("New");
        for (var i = 0; i < 3; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Back.AddDays(-30 - 7 * i) });

        Assert.Empty(List());
    }

    [Fact]
    public void Flagged_ones_start_with_the_oldest_flag_and_include_cuttings()
    {
        Flag(Plant("Newer"), 2);
        Flag(Plant("Older"), 10);
        Flag(Cutting("Cutting", GrowingMedium.Soil), 5);

        Assert.Equal(["Older", "Cutting", "Newer"], List().Select(r => r.Name));
    }

    [Fact]
    public void Plants_that_are_not_in_the_collection_are_left_out()
    {
        var gone = Plant("Gone");
        gone.Status = PlantStatus.Died;
        Flag(gone, 5);
        var deleted = Plant("Deleted");
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        Flag(deleted, 5);

        Assert.Empty(List());
    }

    [Fact]
    public void A_look_over_on_or_after_the_day_back_takes_it_off()
    {
        var onTheDay = Plant("On the day");
        var after = Plant("After");
        var before = Plant("Before");
        foreach (var plant in new[] { onTheDay, after, before })
            Flag(plant, 5);
        LookOver(onTheDay, Back);
        LookOver(after, Back.AddDays(1));
        LookOver(before, Back.AddDays(-1));

        Assert.Equal(["Before"], List().Select(r => r.Name));
    }

    [Fact]
    public void Watering_or_topping_up_on_or_after_the_day_back_takes_it_off()
    {
        var watered = Weekly("Watered", lastDaysAgo: 20);
        var toppedUp = Weekly("Topped up", lastDaysAgo: 20);
        var feeding = Weekly("Fed", lastDaysAgo: 20);
        var beforeIt = Weekly("Before", lastDaysAgo: 20);
        logs.Add(new CareLog { PlantId = watered.Id, Kind = CareKind.Watered, OccurredOn = Back });
        logs.Add(new CareLog { PlantId = toppedUp.Id, Kind = CareKind.ToppedUp, OccurredOn = Back.AddDays(1) });
        logs.Add(new CareLog { PlantId = feeding.Id, Kind = CareKind.Pruned, OccurredOn = Back });
        logs.Add(new CareLog { PlantId = beforeIt.Id, Kind = CareKind.Watered, OccurredOn = Back.AddDays(-1) });

        var names = List().Select(r => r.Name).Order();

        // Pruning isn't a watering, and a watering the day before isn't since the day back. The one the
        // day before is also no longer past its pace, so it isn't listed for that.
        Assert.Equal(["Fed"], names);
    }

    [Fact]
    public void A_cutting_looked_over_leaves_the_list_even_with_a_pest_case_elsewhere()
    {
        var cutting = Cutting("Pilea");
        LookOver(cutting, Back, SubjectType.Propagation);
        var pest = Plant("Pest");
        CaseOn(pest, 3);

        Assert.Equal(["Pest"], List().Select(r => r.Name));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(null, 5)]
    [InlineData(10, 10)]
    [InlineData(0, 12)]
    public void The_list_is_cut_to_the_length_in_settings(int? length, int shown)
    {
        for (var i = 0; i < 12; i++)
            Flag(Plant($"Plant {i:00}"), 20 - i);

        var list = List(length);

        Assert.Equal(shown, list.Count);
        Assert.Equal("Plant 00", list[0].Name);
    }

    [Fact]
    public void A_list_of_ten_shows_all_there_are_when_there_are_fewer()
    {
        Flag(Plant("Only"), 2);

        Assert.Single(List(10));
    }

    [Fact]
    public void The_list_is_empty_when_nothing_qualifies()
    {
        Weekly("Fine", lastDaysAgo: 3);
        Plant("New");
        Cutting("Soil", GrowingMedium.Soil);

        Assert.Empty(List());
    }

    [Fact]
    public void The_list_holds_back_Today_only_while_it_shows_and_has_not_been_dismissed()
    {
        var row = new BackRow(Plant("A"), "A", BackReason.Flagged);
        var shown = PutOffs.Empty();
        var done = PutOffs.Empty();
        done.PutOff(PutOffs.BackList(Back), Back.AddDays(3));

        Assert.Equal($"back:{Back:yyyy-MM-dd}", PutOffs.BackList(Back));
        Assert.True(BackList.IsHolding(Back, [row], shown, Back));
        Assert.False(BackList.IsHolding(Back, [], shown, Back));
        Assert.False(BackList.IsHolding(null, [row], shown, Back));
        Assert.False(BackList.IsHolding(Back, [row], done, Back.AddDays(1)));
        Assert.True(BackList.IsHolding(Back, [row], done, Back.AddDays(3)));
    }

    [Fact]
    public void The_round_for_the_list_walks_it_in_its_order()
    {
        var room = new Place { Name = "Kitchen" };
        placeList.Add(room);
        var pest = Plant("Pest", room);
        CaseOn(pest, 3);
        var cutting = Cutting("Pilea");
        var list = List();

        var cards = GoingRound.ForBack(list, plants, logs, timeline, null, [], new Places(placeList), [], Hemisphere.Northern, Time, true);

        Assert.Equal([pest.Id, cutting.Id], cards.Select(c => c.Subject.Id));
        Assert.Equal("Kitchen", cards[0].Heading);
        Assert.Equal(WateringForm.TopWatered, cards[0].Form);
        Assert.Null(cards[1].Form);
        Assert.Null(cards[1].Guess);
    }
}
