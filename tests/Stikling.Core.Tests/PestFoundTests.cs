using System.Globalization;
using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PestFoundTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 6);

    private sealed class Setup
    {
        public FakePestCaseRepository Cases { get; } = new();
        public FakeTimelineRepository Timeline { get; } = new();
        public PestFoundService Service { get; }
        public Plant Monstera { get; } = new() { Nickname = "Monstera" };
        public Plant Ficus { get; } = new() { Nickname = "Ficus" };

        public Setup() => Service = new PestFoundService(Cases, Timeline, new FixedTime(Now));

        public PestCase Case(PestScope scope = PestScope.PickedPlants, PestCaseStatus status = PestCaseStatus.Active) =>
            new()
            {
                Scope = scope,
                Status = status,
                Pest = Pest.SpiderMites,
                StartedOn = Today.AddDays(-10),
                PlantIds = scope == PestScope.PickedPlants ? [Monstera.Id] : []
            };

        public List<TimelineEntry> Lines(Plant plant) =>
            [.. Timeline.Entries.Where(e => e.SubjectId == plant.Id && !e.IsDeleted && e.Event == TimelineEvent.Pest)];
    }

    private static DateOnly Day(TimelineEntry entry) => new FixedTime(Now).LocalDay(entry.OccurredAt);

    [Fact]
    public void Copy_copies_the_found_on_plants()
    {
        var item = new PestCase { FoundOnPlantIds = [Guid.NewGuid()] };

        var copy = item.Copy();
        copy.FoundOnPlantIds.Add(Guid.NewGuid());

        Assert.Single(item.FoundOnPlantIds);
        Assert.Equal(2, copy.FoundOnPlantIds.Count);
    }

    [Fact]
    public void A_found_on_plant_outside_the_room_is_still_part_of_the_case()
    {
        var places = new FakePlaceRepository();
        var kitchenPlant = new Plant { Nickname = "Basil", PlaceId = places.IdOf("Kitchen") };
        var roomPlant = new Plant { Nickname = "Monstera", PlaceId = places.IdOf("Living room") };
        var item = new PestCase { Scope = PestScope.Room, PlaceId = places.IdOf("Living room"), FoundOnPlantIds = [kitchenPlant.Id] };

        var covered = PestService.PlantsIn(item, [kitchenPlant, roomPlant], places.Current);
        var cases = PestService.CasesFor(kitchenPlant, [item], places.Current);

        Assert.Equal(2, covered.Count);
        Assert.Contains(kitchenPlant, covered);
        Assert.Same(item, Assert.Single(cases));
        Assert.True(PestService.IsFoundOn(item, kitchenPlant.Id));
        Assert.False(PestService.IsFoundOn(item, roomPlant.Id));
    }

    [Fact]
    public void A_deleted_found_on_plant_is_left_out()
    {
        var gone = new Plant { Nickname = "Gone", DeletedAt = Now };
        var item = new PestCase { Scope = PestScope.Room, PlaceId = Guid.NewGuid(), FoundOnPlantIds = [gone.Id] };

        Assert.Empty(PestService.PlantsIn(item, [gone], new FakePlaceRepository().Current));
    }

    [Fact]
    public async Task Marking_adds_the_plant_once_and_writes_one_line()
    {
        var s = new Setup();
        var item = s.Case();

        await s.Service.MarkAsync(item, s.Monstera, Today.AddDays(-2));
        await s.Service.MarkAsync(item, s.Monstera, Today);

        Assert.Equal([s.Monstera.Id], item.FoundOnPlantIds);
        var line = Assert.Single(s.Lines(s.Monstera));
        Assert.Equal(TimelineKind.Change, line.Kind);
        Assert.Equal(item.Id, line.PestCaseId);
        Assert.Equal("PestFound", Assert.Single(line.Lines!).Key);
        Assert.Equal("SpiderMites found", line.Text);
        Assert.Equal(Today.AddDays(-2), Day(line));
        Assert.Same(item, s.Cases.Cases[item.Id]);
    }

    [Fact]
    public async Task Marking_on_picked_plants_adds_the_plant_to_the_case()
    {
        var s = new Setup();
        var item = s.Case();

        await s.Service.MarkAsync(item, s.Ficus, Today);

        Assert.Contains(s.Ficus.Id, item.PlantIds);
    }

    [Fact]
    public async Task Marking_on_a_room_case_leaves_the_scope_alone()
    {
        var s = new Setup();
        var item = s.Case(PestScope.Room);

        await s.Service.MarkAsync(item, s.Ficus, Today);

        Assert.Empty(item.PlantIds);
        Assert.Equal([s.Ficus.Id], item.FoundOnPlantIds);
    }

    [Fact]
    public async Task Marking_on_a_case_being_watched_goes_back_to_treating()
    {
        var s = new Setup();
        var item = s.Case(status: PestCaseStatus.Monitoring);

        await s.Service.MarkAsync(item, s.Monstera, Today);

        Assert.Equal(PestCaseStatus.Active, item.Status);
        Assert.Equal(Today, item.TreatingAgainOn);
    }

    [Fact]
    public async Task Resume_leaves_a_case_being_treated_alone()
    {
        var s = new Setup();
        var item = s.Case();

        await s.Service.ResumeAsync(item);

        Assert.Null(item.TreatingAgainOn);
        Assert.Empty(s.Cases.Cases);
    }

    [Fact]
    public async Task Unmarking_removes_the_plant_and_deletes_the_line()
    {
        var s = new Setup();
        var item = s.Case();
        await s.Service.MarkAsync(item, s.Monstera, Today);

        await s.Service.UnmarkAsync(item, s.Monstera.Id);

        Assert.Empty(item.FoundOnPlantIds);
        Assert.Empty(s.Lines(s.Monstera));
        Assert.Contains(s.Monstera.Id, item.PlantIds);
    }

    [Fact]
    public async Task Unmarking_keeps_a_line_with_photos_as_a_photo_entry()
    {
        var s = new Setup();
        var item = s.Case();
        await s.Service.MarkAsync(item, s.Monstera, Today);
        var line = s.Lines(s.Monstera).Single();
        var photoId = Guid.NewGuid();
        line.PhotoIds.Add(photoId);

        await s.Service.UnmarkAsync(item, s.Monstera.Id);

        Assert.False(line.IsDeleted);
        Assert.Equal(TimelineKind.Photo, line.Kind);
        Assert.Null(line.Event);
        Assert.Null(line.Text);
        Assert.Null(line.PestCaseId);
        Assert.Equal([photoId], line.PhotoIds);
    }

    [Fact]
    public async Task Saving_a_new_case_marks_the_plants_on_the_day_it_started()
    {
        var s = new Setup();
        var item = s.Case();
        item.FoundOnPlantIds = [s.Monstera.Id];

        await s.Service.SaveAsync(item, [], [s.Monstera, s.Ficus]);

        Assert.Equal(item.StartedOn, Day(Assert.Single(s.Lines(s.Monstera))));
        Assert.Same(item, s.Cases.Cases[item.Id]);
    }

    [Fact]
    public async Task Saving_marks_and_unmarks_the_difference()
    {
        var s = new Setup();
        var item = s.Case(PestScope.Everywhere);
        await s.Service.MarkAsync(item, s.Monstera, Today.AddDays(-3));

        var edited = item.Copy();
        edited.FoundOnPlantIds = [s.Ficus.Id];
        await s.Service.SaveAsync(edited, [s.Monstera.Id], [s.Monstera, s.Ficus]);

        Assert.Empty(s.Lines(s.Monstera));
        Assert.Equal(Today, Day(Assert.Single(s.Lines(s.Ficus))));
        Assert.Equal([s.Ficus.Id], s.Cases.Cases[item.Id].FoundOnPlantIds);
    }

    [Fact]
    public async Task Saving_leaves_a_plant_that_was_already_marked_alone()
    {
        var s = new Setup();
        var item = s.Case();
        await s.Service.MarkAsync(item, s.Monstera, Today.AddDays(-3));

        var edited = item.Copy();
        await s.Service.SaveAsync(edited, [s.Monstera.Id], [s.Monstera]);

        Assert.Single(s.Lines(s.Monstera));
    }

    [Fact]
    public async Task Taking_a_picked_plant_out_of_the_case_unmarks_it()
    {
        var s = new Setup();
        var item = s.Case();
        await s.Service.MarkAsync(item, s.Ficus, Today);
        Assert.Equal(2, item.PlantIds.Count);

        var edited = item.Copy();
        edited.PlantIds.Remove(s.Ficus.Id);
        await s.Service.SaveAsync(edited, [s.Ficus.Id], [s.Monstera, s.Ficus]);

        Assert.Empty(edited.FoundOnPlantIds);
        Assert.Empty(s.Lines(s.Ficus));
    }

    [Fact]
    public void Backup_round_trips_the_new_fields_and_old_json_still_loads()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var plantId = Guid.NewGuid();
        var item = new PestCase { FoundOnPlantIds = [plantId], UpdatedAt = Now };
        var line = new TimelineEntry { SubjectId = plantId, Event = TimelineEvent.Pest, PestCaseId = item.Id, UpdatedAt = Now };

        var json = JsonSerializer.Serialize(new BackupData { PestCases = [item], Timeline = [line] }, options);
        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        Assert.Equal([plantId], Assert.Single(back.PestCases).FoundOnPlantIds);
        var restored = Assert.Single(back.Timeline);
        Assert.Equal((TimelineEvent.Pest, item.Id), (restored.Event, restored.PestCaseId));

        var old = JsonSerializer.Deserialize<PestCase>("""{"pest":"Thrips","plantIds":[]}""", options)!;
        Assert.Empty(old.FoundOnPlantIds);
        var oldLine = JsonSerializer.Deserialize<TimelineEntry>("""{"kind":"Change"}""", options)!;
        Assert.Null(oldLine.PestCaseId);
    }

    [Fact]
    public void An_older_version_reads_the_new_event_as_nothing()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var entry = JsonSerializer.Deserialize<TimelineEntry>("""{"kind":"Change","event":"SomethingNewer"}""", options)!;

        Assert.Null(entry.Event);
    }

    [Fact]
    public void The_line_is_shown_in_the_readers_language()
    {
        var entry = new TimelineEntry { Kind = TimelineKind.Change, Event = TimelineEvent.Pest };
        HistoryText.Set(entry, [HistoryText.Line("PestFound", HistoryArg.Of(Pest.Thrips))], _ => "Thrips");

        var danish = HistoryText.Show(entry, _ => "Trips", _ => "Thrips", CultureInfo.GetCultureInfo("da-DK"));
        var english = HistoryText.Show(entry, _ => "Thrips", _ => "Thrips", CultureInfo.GetCultureInfo("en-GB"));

        Assert.Equal("Trips fundet", danish);
        Assert.Equal("Thrips found", english);
    }
}
