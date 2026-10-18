using Stikling.Core.Care;
using Stikling.Core.Fields;
using Stikling.Core.Import;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class SpreadsheetImportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 14);

    private readonly FakePlantRepository plants = new();
    private readonly FakeCareLogRepository logs = new();
    private readonly FakePlaceRepository places = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakeOwnFieldRepository fields = new();
    private readonly SpreadsheetImportService service;

    public SpreadsheetImportServiceTests()
    {
        var time = new FixedTime(Now);
        service = new SpreadsheetImportService(
            new PlantService(plants, timeline, time),
            new CareService(logs, timeline, plants, new FakeFeedRepository(), new FakeProductRepository(), new FakeOwnCareKindRepository(), time),
            new RoomService(places, plants, new FakePropagationRepository()),
            new OwnFieldService(fields, plants),
            plants,
            logs,
            time);
    }

    private static string Label(Enum value) => value.ToString();

    private static IReadOnlyList<ImportRow> Rows(string csv, params ImportField[] fields)
    {
        var sheet = SheetReader.Read(csv);
        return SpreadsheetImport.Preview(sheet, fields, DateOrder.DayFirst, PlantDictionary.Empty, [], Today);
    }

    private string? PlaceOf(Guid plantId) => places.Current.NameOf(plants.Plants[plantId].PlaceId);

    [Fact]
    public async Task Adds_the_plants_with_their_history()
    {
        var rows = Rows("Name,Notes\nBasil,Kitchen herb\nMint,", ImportField.Name, ImportField.AboutIt);
        var progress = new List<int>();

        var result = await service.ImportAsync(rows, Label, new Progress(progress));

        Assert.Equal(2, result.Added);
        Assert.Equal(["Basil", "Mint"], result.PlantIds.Select(id => plants.Plants[id].Nickname));
        Assert.Equal("Kitchen herb", plants.Plants[result.PlantIds[0]].Notes);
        Assert.Equal(2, timeline.Entries.Count(e => e.Kind == TimelineKind.Created));
        Assert.Equal([1, 2], progress);
        Assert.Same(result, service.Last);
    }

    [Fact]
    public async Task Reuses_a_room_that_exists_and_makes_the_rooms_and_spots_that_dont()
    {
        var kitchen = places.Add("Kitchen");
        var rows = Rows(
            "Name,Room\nA,Kitchen\nB,Living room/Window\nC,Living room / Shelf\nD,living room\nE,",
            ImportField.Name, ImportField.Place);

        var result = await service.ImportAsync(rows, Label);

        Assert.Equal(5, result.Added);
        Assert.Equal(3, result.RoomsMade); // Living room, Window and Shelf
        Assert.DoesNotContain(kitchen.Id, result.PlacesMade);
        Assert.Equal(
            ["Kitchen", "Living room / Window", "Living room / Shelf", "Living room", null],
            result.PlantIds.Select(PlaceOf));
        Assert.Equal(2, places.Current.Rooms.Count());
    }

    [Fact]
    public async Task Logs_the_care_dates()
    {
        var rows = Rows("Name,Watered,Repotted\nBasil,1/10/2026,5/9/2026", ImportField.Name, ImportField.LastWatered, ImportField.LastRepotted);

        var result = await service.ImportAsync(rows, Label);

        Assert.Equal(2, result.CareDates);
        var plantId = Assert.Single(result.PlantIds);
        Assert.All(logs.Logs, l => Assert.Equal(plantId, l.PlantId));
        Assert.Equal(
            [(CareKind.Watered, new DateOnly(2026, 10, 1)), (CareKind.Repotted, new DateOnly(2026, 9, 5))],
            logs.Logs.Select(l => (l.Kind, l.OccurredOn)));
        Assert.Equal(logs.Logs.Select(l => l.Id), result.CareLogIds);
    }

    [Fact]
    public async Task Skips_rows_that_cant_be_added_and_goes_on_with_the_rest()
    {
        var good = Rows("Name,Room\nBasil,Hall", ImportField.Name, ImportField.Place)[0];
        var noName = Rows("Name,Room\n,Cellar", ImportField.Name, ImportField.Place)[0];
        var broken = good with { Line = 7, Draft = new Plant { Nickname = "Mint", PricePaid = -1 } };
        var progress = new List<int>();

        var result = await service.ImportAsync([noName, broken, good with { Line = 9 }], Label, new Progress(progress));

        Assert.Equal(1, result.Added);
        Assert.Equal(
            [new ImportSkipped(2, "Unnamed plant", "No name"), new ImportSkipped(7, "Mint", "The price paid can't be negative.")],
            result.Skipped);
        Assert.Equal([1, 2, 3], progress);
        // The rows that were skipped made no rooms
        Assert.Equal(1, result.RoomsMade);
        Assert.Null(places.Current.RoomNamed("Cellar"));
    }

    [Fact]
    public async Task What_was_added_before_a_failure_is_still_in_Last()
    {
        var rows = Rows("Name\nBasil\nMint\nSage", ImportField.Name);
        var failing = new FailingPlants(plants, failOn: 2);
        var time = new FixedTime(Now);
        var broken = new SpreadsheetImportService(
            new PlantService(failing, timeline, time),
            new CareService(logs, timeline, plants, new FakeFeedRepository(), new FakeProductRepository(), new FakeOwnCareKindRepository(), time),
            new RoomService(places, plants, new FakePropagationRepository()),
            new OwnFieldService(fields, plants),
            plants, logs, time);

        await Assert.ThrowsAsync<IOException>(() => broken.ImportAsync(rows, Label));

        Assert.NotNull(broken.Last);
        Assert.Equal(1, broken.Last.Added);
        await broken.UndoAsync(broken.Last);
        Assert.Empty(await plants.GetAllAsync());
    }

    [Fact]
    public async Task The_rows_given_are_left_as_they_were()
    {
        var rows = Rows("Name,Room\nBasil,Hall", ImportField.Name, ImportField.Place);
        await service.ImportAsync(rows, Label);
        Assert.Null(rows[0].Draft.PlaceId);
    }

    [Fact]
    public async Task Undo_deletes_what_the_import_added_and_the_places_it_made()
    {
        var kitchen = places.Add("Kitchen");
        var rows = Rows(
            "Name,Room,Watered\nA,Kitchen,1/10/2026\nB,Living room / Window,",
            ImportField.Name, ImportField.Place, ImportField.LastWatered);
        var result = await service.ImportAsync(rows, Label);

        await service.UndoAsync(result);

        Assert.Empty(await plants.GetAllAsync());
        Assert.Empty(await logs.GetAllAsync());
        Assert.Equal(["Kitchen"], places.Current.Rooms.Select(r => r.Name));
        Assert.Empty(places.Current.SpotsIn(kitchen.Id));
        Assert.All(result.PlantIds, id => Assert.True(plants.Plants[id].IsDeleted));
    }

    [Fact]
    public async Task Undo_leaves_a_made_room_that_now_holds_another_plant()
    {
        var rows = Rows("Name,Room\nA,Living room / Window\nB,Hall", ImportField.Name, ImportField.Place);
        var result = await service.ImportAsync(rows, Label);

        // Another plant moves into the window afterwards, and one into the hall
        var window = places.Current.Rooms.Single(r => r.Name == "Living room");
        var spot = places.Current.SpotsIn(window.Id).Single();
        var hall = places.Current.RoomNamed("Hall")!;
        await plants.SaveAsync(new Plant { Nickname = "Moved in", PlaceId = spot.Id });

        await service.UndoAsync(result);

        // The window and its room stay; the hall is empty and goes
        Assert.NotNull(places.Current.Find(spot.Id));
        Assert.NotNull(places.Current.Find(window.Id));
        Assert.Null(places.Current.Find(hall.Id));
        Assert.Equal("Moved in", Assert.Single(await plants.GetAllAsync()).Nickname);
    }

    [Fact]
    public async Task Undo_copes_with_a_place_that_is_already_gone()
    {
        var rows = Rows("Name,Room\nA,Hall", ImportField.Name, ImportField.Place);
        var result = await service.ImportAsync(rows, Label);
        await places.DeleteAsync(result.PlacesMade.Single());

        await service.UndoAsync(result);

        Assert.Empty(await plants.GetAllAsync());
    }

    private sealed class Progress(List<int> seen) : IProgress<int>
    {
        public void Report(int value) => seen.Add(value);
    }

    // Saves the plants until the one that should fail
    private sealed class FailingPlants(FakePlantRepository inner, int failOn) : IPlantRepository
    {
        private int saves;

        public Task<IReadOnlyList<Plant>> GetAllAsync() => inner.GetAllAsync();
        public Task<Plant?> GetAsync(Guid id) => inner.GetAsync(id);
        public Task<bool> IsDeletedAsync(Guid id) => inner.IsDeletedAsync(id);
        public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);

        public Task SaveAsync(Plant plant) =>
            ++saves == failOn ? throw new IOException("The database went away.") : inner.SaveAsync(plant);
    }
}
