using Stikling.Core.Import;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class PropagationImportTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static Plant Plant(string? nickname, string? genus = null, string? species = null, string? cultivar = null, PlantStatus status = PlantStatus.Active) =>
        new() { Nickname = nickname, Genus = genus, Species = species, Cultivar = cultivar, Status = status };

    private static IReadOnlyList<PropagationImportRow> Preview(string csv, IReadOnlyList<Plant> plants, IReadOnlyList<Propagation>? existing = null)
    {
        var sheet = SheetReader.Read(csv);
        return PropagationImport.Preview(sheet, PropagationImportFields.Guess(sheet.Headings), DateOrder.DayFirst, PlantDictionary.Empty, plants, existing ?? [], Today);
    }

    [Fact]
    public void The_parent_is_found_by_nickname_ignoring_case_and_spaces()
    {
        var monstera = Plant("Big Monstera", "Monstera", "deliciosa");

        var (match, found) = PropagationImport.FindParent("  big monstera ", [monstera, Plant("Pothos")]);

        Assert.Equal(ParentMatch.Found, match);
        Assert.Same(monstera, found);
    }

    [Fact]
    public void Without_a_nickname_match_the_full_botanical_name_is_tried()
    {
        var hoya = Plant(null, "Hoya", "carnosa", "Krimson Queen");

        var (match, found) = PropagationImport.FindParent("hoya carnosa 'Krimson Queen'", [hoya]);

        Assert.Equal(ParentMatch.Found, match);
        Assert.Same(hoya, found);
    }

    [Fact]
    public void A_nickname_wins_over_a_botanical_name()
    {
        var byNickname = Plant("Hoya carnosa", "Hoya", "pubicalyx");
        var byBotanical = Plant("Other", "Hoya", "carnosa");

        var (match, found) = PropagationImport.FindParent("Hoya carnosa", [byNickname, byBotanical]);

        Assert.Equal(ParentMatch.Found, match);
        Assert.Same(byNickname, found);
    }

    [Fact]
    public void No_plant_with_the_name_leaves_the_parent_empty()
    {
        var (match, found) = PropagationImport.FindParent("Rubber plant", [Plant("Pothos")]);

        Assert.Equal(ParentMatch.NotFound, match);
        Assert.Null(found);
    }

    [Fact]
    public void More_than_one_plant_with_the_name_leaves_the_parent_empty()
    {
        var (byNickname, _) = PropagationImport.FindParent("Basil", [Plant("Basil"), Plant("basil")]);
        var (byBotanical, _) = PropagationImport.FindParent("Ocimum basilicum", [Plant(null, "Ocimum", "basilicum"), Plant(null, "Ocimum", "basilicum")]);

        Assert.Equal(ParentMatch.Several, byNickname);
        Assert.Equal(ParentMatch.Several, byBotanical);
    }

    [Fact]
    public void Plants_that_are_not_active_are_not_parents()
    {
        var gone = Plant("Basil", status: PlantStatus.Died);
        var deleted = Plant("Mint");
        deleted.DeletedAt = Now;

        Assert.Equal(ParentMatch.NotFound, PropagationImport.FindParent("Basil", [gone]).Match);
        Assert.Equal(ParentMatch.NotFound, PropagationImport.FindParent("Mint", [deleted]).Match);
    }

    [Fact]
    public void An_empty_cell_has_no_parent_to_look_for()
    {
        Assert.Equal(ParentMatch.NotGiven, PropagationImport.FindParent(" ", [Plant("Basil")]).Match);
        Assert.Equal(ParentMatch.NotGiven, PropagationImport.FindParent(null, [Plant("Basil")]).Match);
    }

    [Fact]
    public void The_headings_are_matched_to_the_propagation_fields()
    {
        var guess = PropagationImportFields.Guess(["Name", "Parent plant", "Started", "Method", "Medium", "How many", "Room", "Notes", "Something else"]);

        Assert.Equal(
        [
            PropagationImportField.Name, PropagationImportField.Parent, PropagationImportField.StartedOn, PropagationImportField.Method,
            PropagationImportField.Medium, PropagationImportField.HowMany, PropagationImportField.Place, PropagationImportField.AboutIt,
            PropagationImportField.AboutLine
        ], guess);
    }

    [Fact]
    public void A_row_becomes_a_propagation_with_the_parent_linked()
    {
        var monstera = Plant("Big Monstera", "Monstera", "deliciosa");
        var csv = "Name,Parent,Started,Method,Medium,How many,Room,Notes\nMonstera cutting,big monstera,03/11/2026,cutting,water,3,Kitchen / Sill,Two nodes";

        var row = Assert.Single(Preview(csv, [monstera]));

        Assert.Equal(monstera.Id, row.Draft.ParentPlantId);
        Assert.Equal(ParentMatch.Found, row.Parent);
        Assert.Equal(new DateOnly(2026, 11, 3), row.Draft.StartedOn);
        Assert.Equal(PropagationType.Cutting, row.Draft.Type);
        Assert.Equal(GrowingMedium.Water, row.Draft.Medium);
        Assert.Equal(3, row.Draft.InitialCount);
        Assert.Equal("Kitchen / Sill", row.Place);
        Assert.Equal("Two nodes", row.Draft.Notes);
        Assert.Null(row.CantAdd);
        Assert.True(row.TickedByDefault);
    }

    [Fact]
    public void A_parent_that_is_not_found_is_kept_in_the_notes_and_the_row_says_so()
    {
        var csv = "Name,Parent\nMonstera cutting,Mystery plant";

        var row = Assert.Single(Preview(csv, [Plant("Pothos")]));

        Assert.Null(row.Draft.ParentPlantId);
        Assert.Equal(ParentMatch.NotFound, row.Parent);
        Assert.Equal("Mystery plant", row.ParentText);
        Assert.Contains("Parent: Mystery plant", row.Draft.Notes);
        Assert.Null(row.CantAdd);
    }

    [Fact]
    public void A_parent_that_matches_more_than_one_plant_is_left_empty()
    {
        var csv = "Name,Parent\nBasil cutting,Basil";

        var row = Assert.Single(Preview(csv, [Plant("Basil"), Plant("Basil")]));

        Assert.Null(row.Draft.ParentPlantId);
        Assert.Equal(ParentMatch.Several, row.Parent);
    }

    [Fact]
    public void Something_that_cant_be_read_is_kept_in_the_notes()
    {
        var csv = "Name,Started,Method,How many\nCutting,next week,magic,lots";

        var row = Assert.Single(Preview(csv, []));

        Assert.Equal(Today, row.Draft.StartedOn);
        Assert.Equal(PropagationType.Cutting, row.Draft.Type);
        Assert.Equal(1, row.Draft.InitialCount);
        Assert.Equal(3, row.Problems.Count);
        Assert.Contains("Started: next week", row.Draft.Notes);
        Assert.Contains("Method: magic", row.Draft.Notes);
        Assert.Contains("How many: lots", row.Draft.Notes);
    }

    [Fact]
    public void A_row_that_looks_like_one_you_have_starts_unticked()
    {
        var existing = new Propagation { Nickname = "Monstera cutting", StartedOn = new DateOnly(2026, 11, 3) };
        var csv = "Nickname,Started\nMonstera cutting,03/11/2026\nMonstera cutting,04/11/2026";

        var rows = Preview(csv, [], [existing]);

        Assert.True(rows[0].LooksLikeYours);
        Assert.False(rows[0].TickedByDefault);
        Assert.False(rows[1].LooksLikeYours);
    }

    [Fact]
    public void A_row_without_a_name_cant_be_added()
    {
        var row = Assert.Single(Preview("Name,Notes\n,just a note", []));

        Assert.NotNull(row.CantAdd);
        Assert.False(row.TickedByDefault);
    }

    [Theory]
    [InlineData("cutting", PropagationType.Cutting)]
    [InlineData("Stikling", PropagationType.Cutting)]
    [InlineData("air layering", PropagationType.AirLayer)]
    [InlineData("seeds", PropagationType.Seed)]
    [InlineData("Corm", PropagationType.Corm)]
    [InlineData("deling", PropagationType.Division)]
    public void Methods_are_read_in_English_and_Danish(string text, PropagationType expected) =>
        Assert.Equal(expected, PropagationImport.ReadMethod(text));

    private sealed record Setup(
        PropagationImportService Importer,
        FakePropagationRepository Propagations,
        FakePlantRepository Plants,
        FakePlaceRepository Places);

    private static Setup Build()
    {
        var plants = new FakePlantRepository();
        var propagations = new FakePropagationRepository();
        var places = new FakePlaceRepository();
        var time = new FixedTime(Now);
        var service = new PropagationService(propagations, plants, new FakeTimelineRepository(), new FakePropagationCareLogRepository(),
            new FakeCareLogRepository(), new FakePhotoRepository(), time);
        var rooms = new RoomService(places, plants, propagations);
        return new Setup(new PropagationImportService(service, rooms, time), propagations, plants, places);
    }

    [Fact]
    public async Task Importing_adds_the_ticked_rows_makes_the_rooms_and_undo_takes_them_back()
    {
        var setup = Build();
        var monstera = Plant("Big Monstera", "Monstera", "deliciosa");
        await setup.Plants.SaveAsync(monstera);
        var csv = "Name,Parent,Room\nMonstera cutting,Big Monstera,Kitchen / Sill\nPothos cutting,Nobody,Kitchen";
        var rows = Preview(csv, [monstera]);

        var result = await setup.Importer.ImportAsync(rows, e => e.ToString());

        Assert.Equal(2, result.Added);
        Assert.Equal(2, result.RoomsMade);
        var first = setup.Propagations.Propagations[result.PropagationIds[0]];
        Assert.Equal(monstera.Id, first.ParentPlantId);
        Assert.Equal("Sill", setup.Places.Current.Find(first.PlaceId)!.Name);
        Assert.Null(setup.Propagations.Propagations[result.PropagationIds[1]].ParentPlantId);

        await setup.Importer.UndoAsync(result);

        Assert.All(result.PropagationIds, id => Assert.True(setup.Propagations.Propagations[id].IsDeleted));
        Assert.Empty(setup.Places.Current.Rooms);
    }

    [Fact]
    public async Task A_row_that_cant_be_added_is_skipped_with_its_reason_and_the_rest_go_on()
    {
        var setup = Build();
        var rows = Preview("Name\nBasil cutting\n", []);
        var bad = rows[0] with { CantAdd = "No name" };

        var result = await setup.Importer.ImportAsync([bad, rows[0]], e => e.ToString());

        Assert.Equal(1, result.Added);
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("No name", skipped.Reason);
    }
}
