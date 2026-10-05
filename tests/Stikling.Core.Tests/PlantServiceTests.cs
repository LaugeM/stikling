using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);

    private readonly FakePlantRepository plants = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly PlantService service;

    public PlantServiceTests() => service = new PlantService(plants, timeline, new FixedTime(Now));

    private static string Label(Enum value) => value.ToString();

    private async Task<Plant> AddTagged(string name, params string[] tags)
    {
        var plant = new Plant { Nickname = name, Tags = [.. tags] };
        await plants.SaveAsync(plant);
        return plant;
    }

    [Fact]
    public async Task RenameTag_changes_it_on_every_plant_that_has_it()
    {
        var a = await AddTagged("A", "Rare", "Swap");
        var b = await AddTagged("B", "swap");
        var c = await AddTagged("C", "Rare");

        var changed = await service.RenameTagAsync("Swap", "  For   swap ");

        Assert.Equal(2, changed);
        Assert.Equal(["Rare", "For swap"], a.Tags);
        Assert.Equal(["For swap"], b.Tags);
        Assert.Equal(["Rare"], c.Tags);
    }

    [Fact]
    public async Task RenameTag_into_an_existing_tag_merges_without_a_duplicate()
    {
        var both = await AddTagged("Both", "Rare", "Special");
        var only = await AddTagged("Only", "special");
        var other = await AddTagged("Other", "Rare");

        var changed = await service.RenameTagAsync("Special", "rare");

        Assert.Equal(2, changed);
        Assert.Equal(["Rare"], both.Tags);
        Assert.Equal(["rare"], only.Tags);
        Assert.Equal(["Rare"], other.Tags);
    }

    [Fact]
    public async Task RenameTag_can_change_only_the_case()
    {
        var plant = await AddTagged("A", "rare");

        Assert.Equal(1, await service.RenameTagAsync("rare", "Rare"));
        Assert.Equal(["Rare"], plant.Tags);
        Assert.Equal(0, await service.RenameTagAsync("Rare", "Rare"));
    }

    [Fact]
    public async Task RenameTag_with_an_empty_name_does_nothing()
    {
        var plant = await AddTagged("A", "Rare");

        Assert.Equal(0, await service.RenameTagAsync("Rare", "   "));
        Assert.Equal(["Rare"], plant.Tags);
    }

    [Fact]
    public async Task RenameTag_leaves_deleted_plants_alone_and_renames_gone_ones()
    {
        var deleted = await AddTagged("Deleted", "Rare");
        deleted.DeletedAt = Now;
        var gone = await AddTagged("Gone", "Rare");
        gone.Status = PlantStatus.Died;

        var changed = await service.RenameTagAsync("Rare", "Scarce");

        Assert.Equal(1, changed);
        Assert.Equal(["Rare"], deleted.Tags);
        Assert.Equal(["Scarce"], gone.Tags);
    }

    [Fact]
    public async Task Create_saves_the_plant_and_records_it_on_the_acquired_date()
    {
        var plant = new Plant { Nickname = "Basil", AcquiredOn = LooseDate.Of(new DateOnly(2026, 5, 1)) };

        await service.CreateAsync(plant);

        Assert.Same(plant, plants.Plants[plant.Id]);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(TimelineKind.Created, entry.Kind);
        Assert.Equal(plant.Id, entry.SubjectId);
        Assert.Equal(new DateOnly(2026, 5, 1), DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime));
        Assert.Equal("Added to collection", entry.Text);
    }

    [Fact]
    public async Task SetFavourite_saves_the_plant_without_a_timeline_entry()
    {
        var plant = new Plant { Nickname = "Basil" };

        await service.SetFavouriteAsync(plant, true);

        Assert.True(plants.Plants[plant.Id].Favourite);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Create_without_a_date_uses_now()
    {
        await service.CreateAsync(new Plant { Nickname = "Coleus", AcquiredOn = null, Origin = PlantOrigin.Propagated });

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("Added as a propagated plant", entry.Text);
    }

    [Fact]
    public async Task Create_with_photos_makes_the_newest_the_cover_and_starts_the_history()
    {
        var taken = Now.AddHours(-2);
        var photos = new[]
        {
            new Photo { SubjectType = SubjectType.Plant, TakenAt = taken },
            new Photo { SubjectType = SubjectType.Plant, TakenAt = taken.AddMinutes(5) }
        };
        var plant = new Plant { Nickname = "Coleus" };

        await service.CreateAsync(plant, photos);

        Assert.Equal(photos[1].Id, plant.CoverPhotoId);
        var entry = Assert.Single(timeline.Entries, e => e.Kind == TimelineKind.Photo);
        Assert.Equal(photos.Select(p => p.Id), entry.PhotoIds);
        Assert.Equal(taken.AddMinutes(5), entry.OccurredAt);
    }

    [Fact]
    public async Task Create_with_old_photos_puts_each_day_on_the_history()
    {
        var photos = new[]
        {
            new Photo { SubjectType = SubjectType.Plant, TakenAt = new(2024, 3, 1, 9, 0, 0, TimeSpan.Zero) },
            new Photo { SubjectType = SubjectType.Plant, TakenAt = Now }
        };
        var plant = new Plant { Nickname = "Coleus" };

        await service.CreateAsync(plant, photos);

        Assert.Equal(photos[1].Id, plant.CoverPhotoId);
        Assert.Equal(
            [photos[0].TakenAt, Now],
            timeline.Entries.Where(e => e.Kind == TimelineKind.Photo).Select(e => e.OccurredAt).Order());
    }

    [Fact]
    public async Task Create_puts_the_plant_before_photos_taken_earlier_the_same_day()
    {
        var morning = new DateTimeOffset(2024, 3, 1, 9, 0, 0, TimeSpan.Zero);
        var photos = new[] { new Photo { SubjectType = SubjectType.Plant, TakenAt = morning } };
        var plant = new Plant { Nickname = "Coleus", AcquiredOn = LooseDate.Of(new DateOnly(2024, 3, 1)) };

        await service.CreateAsync(plant, photos);

        var added = Assert.Single(timeline.Entries, e => e.Kind == TimelineKind.Created);
        Assert.Equal(morning.AddSeconds(-1), added.OccurredAt);
    }

    [Fact]
    public async Task Create_keeps_noon_when_the_photos_are_from_other_days()
    {
        var photos = new[] { new Photo { SubjectType = SubjectType.Plant, TakenAt = new(2024, 3, 2, 9, 0, 0, TimeSpan.Zero) } };
        var plant = new Plant { Nickname = "Coleus", AcquiredOn = LooseDate.Of(new DateOnly(2024, 3, 1)) };

        await service.CreateAsync(plant, photos);

        var added = Assert.Single(timeline.Entries, e => e.Kind == TimelineKind.Created);
        Assert.Equal(new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero), added.OccurredAt);
    }

    [Fact]
    public async Task Create_rejects_invalid_plants_without_writing_history()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new Plant()));

        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Update_records_what_changed_in_one_entry()
    {
        var places = new FakePlaceRepository();
        var plant = new Plant { Nickname = "Big alocasia", PlaceId = places.IdOf("Living room") };
        await service.CreateAsync(plant);
        var before = plant.Copy();
        plant.PlaceId = places.IdOf("Bedroom");
        plant.Medium = GrowingMedium.Leca;

        await service.UpdateAsync(before, plant, Label, placeName: id => places.Current.NameOf(id));

        var change = timeline.Entries.Single(e => e.Kind == TimelineKind.Change);
        Assert.Equal("Moved from Living room to Bedroom\nNow grows in Leca (was Soil)", change.Text);
    }

    [Fact]
    public async Task Move_sets_the_place_and_records_it_on_each_plant()
    {
        var places = new FakePlaceRepository();
        var a = new Plant { Nickname = "A", PlaceId = places.IdOf("Living room") };
        var b = new Plant { Nickname = "B" };
        await plants.SaveAsync(a);
        await plants.SaveAsync(b);
        var bedroom = places.IdOf("Bedroom");

        var moved = await service.MoveAsync([a, b], bedroom, Label, id => places.Current.NameOf(id));

        Assert.Equal(2, moved);
        Assert.Equal(bedroom, a.PlaceId);
        Assert.Equal(bedroom, b.PlaceId);
        var texts = timeline.Entries.Where(e => e.Kind == TimelineKind.Change).ToList();
        Assert.Equal(2, texts.Count);
        Assert.Contains(texts, e => e.SubjectId == a.Id && e.Text == "Moved from Living room to Bedroom");
    }

    [Fact]
    public async Task Move_leaves_plants_already_there_alone()
    {
        var places = new FakePlaceRepository();
        var room = places.IdOf("Bedroom");
        var there = new Plant { Nickname = "There", PlaceId = room };
        await plants.SaveAsync(there);

        var moved = await service.MoveAsync([there], room, Label);

        Assert.Equal(0, moved);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Move_skips_deleted_plants()
    {
        var places = new FakePlaceRepository();
        var gone = new Plant { Nickname = "Gone", DeletedAt = Now };
        await plants.SaveAsync(gone);

        var moved = await service.MoveAsync([gone], places.IdOf("Bedroom"), Label);

        Assert.Equal(0, moved);
        Assert.Null(gone.PlaceId);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Update_records_a_change_of_light()
    {
        var plant = new Plant { Nickname = "Jade" };
        await service.CreateAsync(plant);
        var before = plant.Copy();
        plant.Light = LightLevel.DirectSun;

        await service.UpdateAsync(before, plant, Label);

        var change = timeline.Entries.Single(e => e.Kind == TimelineKind.Change);
        Assert.Equal("Light: DirectSun (was not set)", change.Text);
    }

    [Fact]
    public async Task Update_without_tracked_changes_adds_no_entry()
    {
        var plant = new Plant { Nickname = "Jade" };
        await service.CreateAsync(plant);
        var before = plant.Copy();
        plant.Notes = "Only notes";

        await service.UpdateAsync(before, plant, Label);

        Assert.DoesNotContain(timeline.Entries, e => e.Kind == TimelineKind.Change);
    }

    [Fact]
    public async Task SetStatus_skips_plants_that_already_have_it()
    {
        var alive = new Plant { Nickname = "Parsley" };
        var dead = new Plant { Nickname = "Old basil", Status = PlantStatus.Died };
        plants.Plants[alive.Id] = alive;
        plants.Plants[dead.Id] = dead;

        await service.SetStatusAsync([alive, dead], PlantStatus.Died, Label);

        Assert.Equal(PlantStatus.Died, alive.Status);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(alive.Id, entry.SubjectId);
        Assert.Equal("Status: Died (was Active)", entry.Text);
    }

    [Fact]
    public async Task Marking_a_plant_as_died_saves_the_cause_and_adds_it_to_the_history()
    {
        var plant = new Plant { Nickname = "Basil" };
        plants.Plants[plant.Id] = plant;
        var before = plant.Copy();
        plant.Status = PlantStatus.Died;
        plant.CauseOfDeath = "  root rot after repotting ";

        await service.UpdateAsync(before, plant, Label);

        Assert.Equal("root rot after repotting", plants.Plants[plant.Id].CauseOfDeath);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Status: Died (was Active)\nWhat happened: root rot after repotting", entry.Text);
    }

    [Fact]
    public async Task Bringing_a_plant_back_clears_the_cause()
    {
        var plant = new Plant { Nickname = "Basil", Status = PlantStatus.Died, CauseOfDeath = "Too dry" };
        plants.Plants[plant.Id] = plant;
        var before = plant.Copy();
        plant.Status = PlantStatus.Active;

        await service.UpdateAsync(before, plant, Label);

        Assert.Null(plants.Plants[plant.Id].CauseOfDeath);
    }

    [Fact]
    public async Task Selling_a_plant_saves_who_bought_it_and_the_price_and_adds_them_to_the_history()
    {
        var plant = new Plant { Nickname = "Basil" };
        plants.Plants[plant.Id] = plant;
        var before = plant.Copy();
        plant.Status = PlantStatus.Sold;
        plant.LeftTo = "  Anna ";
        plant.LeftFor = " 150 kr ";

        await service.UpdateAsync(before, plant, Label);

        Assert.Equal("Anna", plants.Plants[plant.Id].LeftTo);
        Assert.Equal("150 kr", plants.Plants[plant.Id].LeftFor);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Status: Sold (was Active)\nSold to Anna for 150 kr", entry.Text);
    }

    [Fact]
    public async Task Swapping_a_plant_away_says_what_came_back_in_the_history()
    {
        var plant = new Plant { Nickname = "Basil" };
        plants.Plants[plant.Id] = plant;
        var before = plant.Copy();
        plant.Status = PlantStatus.GivenAway;
        plant.LeftTo = "Anna";
        plant.LeftFor = "a Hoya carnosa";

        await service.UpdateAsync(before, plant, Label);

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Status: GivenAway (was Active)\nGiven to Anna · swapped for a Hoya carnosa", entry.Text);
    }

    [Fact]
    public async Task Bringing_a_plant_back_clears_who_got_it_and_what_it_went_for()
    {
        var plant = new Plant { Nickname = "Basil", Status = PlantStatus.Sold, LeftTo = "Anna", LeftFor = "150 kr" };
        plants.Plants[plant.Id] = plant;
        var before = plant.Copy();
        plant.Status = PlantStatus.Active;

        await service.UpdateAsync(before, plant, Label);

        Assert.Null(plants.Plants[plant.Id].LeftTo);
        Assert.Null(plants.Plants[plant.Id].LeftFor);
    }

    [Fact]
    public async Task Giving_plants_away_with_their_pots_takes_what_each_one_has()
    {
        var both = new Plant { Nickname = "Hoya", InnerPotId = Guid.NewGuid(), OuterPotId = Guid.NewGuid() };
        var inner = new Plant { Nickname = "Pilea", InnerPotId = Guid.NewGuid() };
        var bare = new Plant { Nickname = "Cutting" };

        await service.SetStatusAsync([both, inner, bare], PlantStatus.GivenAway, Label, PotsTaken.All);

        Assert.Equal(PotsTaken.All, both.PotsTaken);
        Assert.Equal(PotsTaken.Inner, inner.PotsTaken);
        Assert.Equal(PotsTaken.None, bare.PotsTaken);
        Assert.Equal("Status: GivenAway (was Active)\nIts pot and outer pot went with it",
            timeline.Entries.Single(e => e.SubjectId == both.Id).Text);
        Assert.Equal("Status: GivenAway (was Active)", timeline.Entries.Single(e => e.SubjectId == bare.Id).Text);
    }

    [Fact]
    public async Task A_plant_back_in_the_collection_has_its_pots_again()
    {
        var plant = new Plant { Nickname = "Hoya", InnerPotId = Guid.NewGuid(), Status = PlantStatus.Sold, PotsTaken = PotsTaken.Inner };
        var before = plant.Copy();
        plant.Status = PlantStatus.Active;

        await service.UpdateAsync(before, plant, Label);

        Assert.Equal(PotsTaken.None, plant.PotsTaken);
    }

    [Fact]
    public void Only_a_plant_that_left_can_take_its_pots()
    {
        var plant = new Plant { Nickname = "Hoya", PotsTaken = PotsTaken.Inner };

        Assert.Contains("Only a plant that was given away or sold can take its pots with it.", plant.Validate(DateOnly.FromDateTime(Now.Date)));
    }

    [Fact]
    public async Task AddNote_adds_the_same_trimmed_note_to_each_plant()
    {
        var a = new Plant { Nickname = "Philodendron A" };
        var b = new Plant { Nickname = "Philodendron B" };

        await service.AddNoteAsync([a, b], "  Sprayed for thrips  ");

        Assert.Equal(2, timeline.Entries.Count);
        Assert.All(timeline.Entries, e =>
        {
            Assert.Equal(TimelineKind.Note, e.Kind);
            Assert.Equal("Sprayed for thrips", e.Text);
            Assert.Equal(Now, e.OccurredAt);
        });
        Assert.Equal([a.Id, b.Id], timeline.Entries.Select(e => e.SubjectId));
    }

    [Fact]
    public async Task AddNote_requires_text()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddNoteAsync([new Plant { Nickname = "X" }], "  "));
    }

    [Fact]
    public async Task A_date_that_is_only_a_year_is_said_on_the_first_entry()
    {
        await service.CreateAsync(new Plant { Nickname = "Fig", AcquiredOn = LooseDate.Of(2024) });

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Added to collection, 2024", entry.Text);
        Assert.Equal(new DateOnly(2024, 1, 1), DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime));
    }
}
