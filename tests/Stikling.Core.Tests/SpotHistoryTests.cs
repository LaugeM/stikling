using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class SpotHistoryTests
{
    private readonly Place living = new() { Name = "Living room" };
    private readonly Place bedroom = new() { Name = "Bedroom" };

    private readonly List<Plant> plants = [];
    private readonly List<TimelineEntry> entries = [];

    private static DateOnly Day(int month, int day) => new(2026, month, day);

    private static DateTimeOffset At(DateOnly day) => new(day, new TimeOnly(12, 0), TimeSpan.Zero);

    private static DateOnly LocalDay(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime);

    private Plant AddPlant(Place? place = null, DateOnly? added = null)
    {
        var plant = new Plant { Genus = "Hoya", PlaceId = place?.Id, CreatedAt = At(added ?? Day(1, 1)) };
        plants.Add(plant);
        return plant;
    }

    private TimelineEntry Log(Plant plant, DateOnly day, string key, params string[] args)
    {
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plant.Id,
            Kind = TimelineKind.Change,
            OccurredAt = At(day),
            CreatedAt = At(day),
            Lines = [HistoryText.Line(key, [.. args.Select(HistoryArg.Of)])]
        };
        entries.Add(entry);
        return entry;
    }

    private TimelineEntry LogText(Plant plant, DateOnly day, string text)
    {
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plant.Id,
            Kind = TimelineKind.Change,
            OccurredAt = At(day),
            CreatedAt = At(day),
            Text = text
        };
        entries.Add(entry);
        return entry;
    }

    private IReadOnlyList<SpotPlant> For(Place place, params Place[] others) =>
        SpotHistory.Of(place.Id, new Places([living, bedroom, .. others]), plants, entries, LocalDay);

    [Fact]
    public void An_empty_place_lists_nothing()
    {
        Assert.Empty(For(living));
    }

    [Fact]
    public void A_plant_with_no_room_changes_stands_there_from_when_it_was_added_and_says_so()
    {
        var plant = AddPlant(living, added: Day(2, 1));

        var stay = Assert.Single(Assert.Single(For(living)).Stays);

        Assert.Equal(Day(2, 1), stay.From);
        Assert.False(stay.FromLogged);
        Assert.Null(stay.To);
        Assert.Equal(SpotStayEnding.StillThere, stay.Ending);
        Assert.Empty(For(bedroom));
        Assert.Equal(plant, For(living)[0].Plant);
    }

    [Fact]
    public void A_plant_that_never_had_a_room_lists_nowhere()
    {
        AddPlant();

        Assert.Empty(For(living));
    }

    [Fact]
    public void A_move_ends_the_stay_in_one_place_and_starts_it_in_the_other()
    {
        var plant = AddPlant(bedroom, added: Day(1, 10));
        Log(plant, Day(3, 1), "MovedFromTo", "Living room", "Bedroom");

        var there = Assert.Single(Assert.Single(For(living)).Stays);
        Assert.Equal(Day(1, 10), there.From);
        Assert.False(there.FromLogged);
        Assert.Equal(Day(3, 1), there.To);
        Assert.Equal(SpotStayEnding.MovedOn, there.Ending);

        var here = Assert.Single(Assert.Single(For(bedroom)).Stays);
        Assert.Equal(Day(3, 1), here.From);
        Assert.True(here.FromLogged);
        Assert.Null(here.To);
        Assert.Equal(SpotStayEnding.StillThere, here.Ending);
    }

    [Fact]
    public void A_plant_that_returned_has_one_row_with_both_stays_newest_first()
    {
        var plant = AddPlant(living, added: Day(1, 1));
        Log(plant, Day(2, 1), "MovedFromTo", "Living room", "Bedroom");
        Log(plant, Day(3, 1), "MovedFromTo", "Bedroom", "Living room");

        var row = Assert.Single(For(living));

        Assert.Equal(2, row.Stays.Count);
        Assert.Equal(SpotStayEnding.StillThere, row.Stays[0].Ending);
        Assert.Equal(Day(3, 1), row.Stays[0].From);
        Assert.Equal(SpotStayEnding.MovedOn, row.Stays[1].Ending);
        Assert.Equal(Day(2, 1), row.Stays[1].To);
    }

    [Fact]
    public void A_plant_removed_from_a_room_moved_on_from_it()
    {
        var plant = AddPlant(added: Day(1, 1));
        Log(plant, Day(2, 1), "PlacedIn", "Living room");
        Log(plant, Day(3, 1), "RemovedFrom", "Living room");

        var stay = Assert.Single(Assert.Single(For(living)).Stays);

        Assert.Equal(Day(2, 1), stay.From);
        Assert.Equal(Day(3, 1), stay.To);
        Assert.Equal(SpotStayEnding.MovedOn, stay.Ending);
    }

    [Fact]
    public void A_plant_that_died_in_a_place_died_there_and_nowhere_else()
    {
        var plant = AddPlant(bedroom, added: Day(1, 1));
        Log(plant, Day(2, 1), "MovedFromTo", "Living room", "Bedroom");
        plant.Status = PlantStatus.Died;
        plant.StatusSince = Day(4, 1);

        var here = Assert.Single(Assert.Single(For(bedroom)).Stays);
        Assert.Equal(SpotStayEnding.Died, here.Ending);
        Assert.Equal(Day(4, 1), here.To);

        var there = Assert.Single(Assert.Single(For(living)).Stays);
        Assert.Equal(SpotStayEnding.MovedOn, there.Ending);
    }

    [Fact]
    public void Moving_a_dead_plant_afterwards_doesnt_change_where_it_died()
    {
        var plant = AddPlant(bedroom, added: Day(1, 1));
        plant.Status = PlantStatus.Died;
        plant.StatusSince = Day(2, 1);
        Log(plant, Day(3, 1), "MovedFromTo", "Living room", "Bedroom");

        var stay = Assert.Single(Assert.Single(For(living)).Stays);

        Assert.Equal(SpotStayEnding.Died, stay.Ending);
        Assert.Equal(Day(2, 1), stay.To);
        Assert.Empty(For(bedroom));
    }

    [Fact]
    public void A_plant_that_left_a_place_before_it_died_moved_on_from_there()
    {
        var plant = AddPlant(added: Day(1, 1));
        Log(plant, Day(2, 1), "RemovedFrom", "Living room");
        plant.Status = PlantStatus.Died;
        plant.StatusSince = Day(3, 1);

        Assert.Equal(SpotStayEnding.MovedOn, Assert.Single(Assert.Single(For(living)).Stays).Ending);
    }

    [Theory]
    [InlineData(PlantStatus.GivenAway, SpotStayEnding.GivenAway)]
    [InlineData(PlantStatus.Sold, SpotStayEnding.Sold)]
    public void A_plant_that_left_the_home_is_said_to_have_left_from_where_it_stood(PlantStatus status, SpotStayEnding ending)
    {
        var plant = AddPlant(living);
        plant.Status = status;

        var stay = Assert.Single(Assert.Single(For(living)).Stays);

        Assert.Equal(ending, stay.Ending);
    }

    [Fact]
    public void The_day_a_plant_died_comes_from_its_history_on_a_plant_without_a_status_date()
    {
        var plant = AddPlant(living);
        plant.Status = PlantStatus.Died;
        LogText(plant, Day(5, 1), "Status: Died (was Active)");

        Assert.Equal(Day(5, 1), Assert.Single(Assert.Single(For(living)).Stays).To);
    }

    [Fact]
    public void A_room_that_was_renamed_keeps_what_stood_in_it_under_its_old_name()
    {
        // Moved to Stue, and Stue was renamed afterwards, so the line still has the old name
        living.Name = "Living room";
        var plant = AddPlant(living);
        Log(plant, Day(2, 1), "MovedFromTo", "Bedroom", "Stue");

        var stay = Assert.Single(Assert.Single(For(living)).Stays);

        Assert.Equal(Day(2, 1), stay.From);
        Assert.Equal(SpotStayEnding.StillThere, stay.Ending);
    }

    [Fact]
    public void The_line_that_ends_a_stay_shows_the_new_name_of_a_renamed_room()
    {
        var plant = AddPlant(bedroom);
        Log(plant, Day(2, 1), "PlacedIn", "Stue");
        Log(plant, Day(3, 1), "MovedFromTo", "Living room", "Bedroom");

        var stay = Assert.Single(Assert.Single(For(living)).Stays);

        Assert.Equal(Day(2, 1), stay.From);
        Assert.Equal(Day(3, 1), stay.To);
    }

    [Fact]
    public void What_one_plant_shows_about_a_rename_counts_for_the_others()
    {
        var now = AddPlant(living);
        Log(now, Day(2, 1), "PlacedIn", "Stue");

        var before = AddPlant(bedroom);
        Log(before, Day(1, 5), "PlacedIn", "Stue");
        Log(before, Day(1, 20), "MovedFromTo", "Stue", "Bedroom");

        Assert.Equal(2, For(living).Count);
    }

    [Fact]
    public void A_renamed_room_keeps_what_stood_in_its_spots()
    {
        var sill = new Place { Name = "Windowsill", RoomId = living.Id };
        var showsRename = AddPlant(living);
        Log(showsRename, Day(2, 1), "PlacedIn", "Stue");

        var plant = AddPlant(bedroom);
        Log(plant, Day(1, 5), "PlacedIn", "Stue / Windowsill");
        Log(plant, Day(1, 20), "MovedFromTo", "Stue / Windowsill", "Bedroom");

        Assert.Single(For(sill, sill));
    }

    [Fact]
    public void A_rename_nothing_shows_leaves_the_stay_out_instead_of_guessing()
    {
        var plant = AddPlant(bedroom);
        Log(plant, Day(1, 5), "PlacedIn", "Stue");
        Log(plant, Day(1, 20), "MovedFromTo", "Stue", "Bedroom");

        Assert.Empty(For(living));
    }

    [Fact]
    public void A_name_two_places_claim_means_nothing()
    {
        var one = AddPlant(living);
        Log(one, Day(2, 1), "PlacedIn", "Stue");
        var other = AddPlant(bedroom);
        Log(other, Day(2, 1), "PlacedIn", "Stue");

        var stays = For(living).Single(p => p.Plant == one).Stays;

        // Each plant is where it is now, whatever the old name stood for
        Assert.Equal(SpotStayEnding.StillThere, Assert.Single(stays).Ending);
        Assert.Equal(Day(2, 1), stays[0].From);

        // A plant that left long ago can't be put in either of them
        var left = AddPlant();
        Log(left, Day(1, 5), "PlacedIn", "Stue");
        Log(left, Day(1, 20), "RemovedFrom", "Stue");
        Assert.DoesNotContain(For(living), p => p.Plant == left);
        Assert.DoesNotContain(For(bedroom), p => p.Plant == left);
    }

    [Fact]
    public void What_stood_in_a_merged_room_counts_for_the_room_it_went_into()
    {
        var stue = new Place { Name = "Stue", DeletedAt = At(Day(6, 1)), MergedIntoId = living.Id };
        var plant = AddPlant(bedroom);
        Log(plant, Day(1, 5), "PlacedIn", "Stue");
        Log(plant, Day(1, 20), "MovedFromTo", "Stue", "Bedroom");

        var stay = Assert.Single(Assert.Single(For(living, stue)).Stays);

        Assert.Equal(Day(1, 20), stay.To);
    }

    [Fact]
    public void A_plant_still_pointing_at_a_merged_room_is_still_there()
    {
        var stue = new Place { Name = "Stue", DeletedAt = At(Day(6, 1)), MergedIntoId = living.Id };
        AddPlant(stue);

        Assert.Equal(SpotStayEnding.StillThere, Assert.Single(Assert.Single(For(living, stue)).Stays).Ending);
    }

    [Fact]
    public void A_spot_lists_only_what_stood_in_it_and_a_room_lists_its_spots_too()
    {
        var sill = new Place { Name = "Windowsill", RoomId = living.Id };
        var shelf = new Place { Name = "Shelf", RoomId = living.Id };
        var onSill = AddPlant(sill);
        var inRoom = AddPlant(living);
        var onShelf = AddPlant(shelf);

        Assert.Equal(onSill, Assert.Single(For(sill, sill, shelf)).Plant);
        Assert.Empty(Assert.Single(For(sill, sill, shelf)).Stays[0].Spots);
        Assert.Equal(3, For(living, sill, shelf).Count);
        Assert.Equal(["Shelf"], For(living, sill, shelf).Single(p => p.Plant == onShelf).Stays[0].Spots);
        Assert.Empty(For(living, sill, shelf).Single(p => p.Plant == inRoom).Stays[0].Spots);
    }

    [Fact]
    public void Moving_between_spots_in_a_room_is_one_stay_in_the_room_and_a_move_in_the_spot()
    {
        var sill = new Place { Name = "Windowsill", RoomId = living.Id };
        var shelf = new Place { Name = "Shelf", RoomId = living.Id };
        var plant = AddPlant(shelf, added: Day(1, 1));
        Log(plant, Day(2, 1), "MovedFromTo", "Bedroom", "Living room / Windowsill");
        Log(plant, Day(3, 1), "MovedFromTo", "Living room / Windowsill", "Living room / Shelf");

        var inRoom = Assert.Single(Assert.Single(For(living, sill, shelf)).Stays);
        Assert.Equal(Day(2, 1), inRoom.From);
        Assert.Null(inRoom.To);
        Assert.Equal(SpotStayEnding.StillThere, inRoom.Ending);
        Assert.Equal(["Windowsill", "Shelf"], inRoom.Spots);

        var onSill = Assert.Single(Assert.Single(For(sill, sill, shelf)).Stays);
        Assert.Equal(Day(3, 1), onSill.To);
        Assert.Equal(SpotStayEnding.MovedOn, onSill.Ending);
    }

    [Fact]
    public void A_spot_that_was_removed_still_counts_for_its_room()
    {
        var plant = AddPlant(bedroom);
        Log(plant, Day(1, 5), "PlacedIn", "Living room / Old shelf");
        Log(plant, Day(1, 20), "MovedFromTo", "Living room / Old shelf", "Bedroom");

        Assert.Single(For(living));
    }

    [Fact]
    public void Deleted_plants_are_left_out()
    {
        var plant = AddPlant(living);
        Log(plant, Day(2, 1), "MovedFromTo", "Bedroom", "Living room");
        plant.DeletedAt = At(Day(6, 1));

        Assert.Empty(For(living));
        Assert.Empty(For(bedroom));
    }

    [Fact]
    public void Deleted_history_lines_are_left_out()
    {
        var plant = AddPlant(living, added: Day(1, 1));
        var entry = Log(plant, Day(2, 1), "MovedFromTo", "Bedroom", "Living room");
        entry.DeletedAt = At(Day(6, 1));

        Assert.Empty(For(bedroom));
        Assert.Equal(Day(1, 1), Assert.Single(Assert.Single(For(living)).Stays).From);
    }

    [Fact]
    public void A_plant_living_with_someone_else_is_left_out()
    {
        var plant = AddPlant(living);
        plant.LivesElsewhere = true;

        Assert.Empty(For(living));
    }

    [Fact]
    public void Records_arriving_out_of_order_read_the_same()
    {
        var plant = AddPlant(living, added: Day(1, 1));
        var moves = new[]
        {
            Log(plant, Day(2, 1), "MovedFromTo", "Living room", "Bedroom"),
            Log(plant, Day(3, 1), "MovedFromTo", "Bedroom", "Living room")
        };
        var inOrder = For(living);

        entries.Clear();
        entries.AddRange(moves.Reverse());

        Assert.Equal(inOrder[0].Stays, For(living)[0].Stays, new StayComparer());
    }

    [Fact]
    public void A_line_that_arrived_after_a_later_move_still_counts_in_its_place_in_time()
    {
        // A move logged on another device with an earlier date
        var plant = AddPlant(bedroom, added: Day(1, 1));
        Log(plant, Day(3, 1), "MovedFromTo", "Living room", "Bedroom");
        var backdated = Log(plant, Day(2, 1), "MovedFromTo", "Bedroom", "Living room");
        backdated.CreatedAt = At(Day(4, 1));

        Assert.Equal(Day(2, 1), Assert.Single(Assert.Single(For(living)).Stays).From);
        Assert.Equal(2, Assert.Single(For(bedroom)).Stays.Count);
    }

    [Fact]
    public void A_plant_that_is_somewhere_the_history_doesnt_lead_to_is_there_without_a_date()
    {
        // The line for the last move hasn't arrived from the other device yet
        var plant = AddPlant(bedroom, added: Day(1, 1));
        Log(plant, Day(2, 1), "PlacedIn", "Living room");

        var there = Assert.Single(Assert.Single(For(living)).Stays);
        Assert.Equal(SpotStayEnding.MovedOn, there.Ending);
        Assert.Null(there.To);

        var here = Assert.Single(Assert.Single(For(bedroom)).Stays);
        Assert.Equal(SpotStayEnding.StillThere, here.Ending);
        Assert.Null(here.From);
    }

    [Fact]
    public void Old_text_lines_are_read_too()
    {
        var plant = AddPlant(bedroom, added: Day(1, 1));
        LogText(plant, Day(2, 1), "Moved from Living room to Bedroom");

        Assert.Equal(Day(2, 1), Assert.Single(Assert.Single(For(living)).Stays).To);
        Assert.Equal(Day(2, 1), Assert.Single(Assert.Single(For(bedroom)).Stays).From);
    }

    [Fact]
    public void An_old_text_line_with_to_in_a_name_is_cut_where_the_names_exist()
    {
        var window = new Place { Name = "Next to the window" };
        var plant = AddPlant(bedroom, added: Day(1, 1));
        LogText(plant, Day(2, 1), "Moved from Next to the window to Bedroom");

        Assert.Equal(Day(2, 1), Assert.Single(Assert.Single(For(window, window)).Stays).To);
        Assert.Equal(Day(2, 1), Assert.Single(Assert.Single(For(bedroom, window)).Stays).From);
    }

    [Fact]
    public async Task A_move_made_through_the_service_reads_back()
    {
        var repo = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var clock = new FixedTime(At(Day(5, 1)));
        var service = new PlantService(repo, timeline, clock);
        var places = new Places([living, bedroom]);

        var plant = AddPlant(living, added: Day(1, 1));
        plants.Clear();
        await repo.SaveAsync(plant);
        await service.MoveAsync([plant], bedroom.Id, e => e.ToString(), id => places.NameOf(id));

        var all = await timeline.GetPlantChangesAsync();

        var there = Assert.Single(Assert.Single(SpotHistory.Of(living.Id, places, [plant], all, LocalDay)).Stays);
        Assert.Equal(Day(5, 1), there.To);
        var here = Assert.Single(Assert.Single(SpotHistory.Of(bedroom.Id, places, [plant], all, LocalDay)).Stays);
        Assert.Equal(SpotStayEnding.StillThere, here.Ending);
    }

    private sealed class StayComparer : IEqualityComparer<SpotStay>
    {
        public bool Equals(SpotStay? a, SpotStay? b) =>
            a is not null && b is not null && a.From == b.From && a.To == b.To && a.Ending == b.Ending && a.FromLogged == b.FromLogged;

        public int GetHashCode(SpotStay stay) => stay.From.GetHashCode();
    }
}
