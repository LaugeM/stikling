using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Import;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class UnsureNameTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static string Label(Enum value) => value.ToString();

    // Name building

    [Theory]
    [InlineData(NameQualifier.Cf, "carnosa", "Krimson Queen", null, "Hoya cf. carnosa 'Krimson Queen'")]
    [InlineData(NameQualifier.Aff, "carnosa", null, null, "Hoya aff. carnosa")]
    [InlineData(NameQualifier.Aff, "carnosa", null, "Thailand", "Hoya aff. carnosa Thailand")]
    [InlineData(NameQualifier.Sp, null, null, null, "Hoya sp.")]
    [InlineData(NameQualifier.Sp, "carnosa", null, null, "Hoya sp.")]
    [InlineData(NameQualifier.Sp, null, null, "Sumatra EPC 123", "Hoya sp. Sumatra EPC 123")]
    [InlineData(NameQualifier.Sp, null, "Red", null, "Hoya sp. 'Red'")]
    [InlineData(NameQualifier.Unknown, null, null, null, "Hoya")]
    [InlineData(NameQualifier.Unknown, "carnosa", null, null, "Hoya carnosa")]
    [InlineData(NameQualifier.Unknown, "carnosa", null, "Thailand", "Hoya carnosa Thailand")]
    [InlineData(null, "carnosa", null, "Thailand", "Hoya carnosa Thailand")]
    [InlineData(null, "carnosa", null, null, "Hoya carnosa")]
    public void The_botanical_name_is_written_the_way_collectors_write_it(
        NameQualifier? qualifier, string? species, string? cultivar, string? note, string expected)
    {
        Assert.Equal(expected, new Plant { Genus = "hoya", Species = species, Cultivar = cultivar, NameQualifier = qualifier, NameNote = note }.BotanicalName);
        Assert.Equal(expected, new Propagation { Genus = "hoya", Species = species, Cultivar = cultivar, NameQualifier = qualifier, NameNote = note }.BotanicalName);
        Assert.Equal(expected, new Wish { Genus = "hoya", Species = species, Cultivar = cultivar, NameQualifier = qualifier, NameNote = note }.BotanicalName);
    }

    [Fact]
    public void A_lost_name_with_no_genus_is_the_note_alone_or_nothing()
    {
        Assert.Equal("From Anna", new Plant { NameQualifier = NameQualifier.Unknown, NameNote = " From Anna " }.BotanicalName);
        Assert.Null(new Plant { NameQualifier = NameQualifier.Unknown }.BotanicalName);
    }

    [Fact]
    public void A_nameless_plant_shows_the_nickname_then_Name_unknown()
    {
        Assert.Equal("Name unknown", new Plant { NameQualifier = NameQualifier.Unknown }.DisplayName);
        Assert.Equal("Name unknown", new Propagation { NameQualifier = NameQualifier.Unknown }.DisplayName);
        Assert.Equal("Name unknown", new Wish { NameQualifier = NameQualifier.Unknown }.DisplayName);
        Assert.Equal("Fern", new Plant { NameQualifier = NameQualifier.Unknown, Nickname = "Fern" }.DisplayName);
        Assert.Equal("that one", new Wish { NameQualifier = NameQualifier.Unknown, Name = "that one" }.DisplayName);
    }

    [Fact]
    public void The_three_argument_name_is_unchanged()
    {
        Assert.Equal("Hoya carnosa 'Krimson Queen'", PlantNames.Botanical("hoya", "carnosa", "Krimson Queen"));
    }

    [Fact]
    public void Duplicating_and_copying_a_plant_carries_the_unsure_name()
    {
        var plant = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp, NameNote = "Sumatra" };
        Assert.Equal(NameQualifier.Sp, plant.Duplicate().NameQualifier);
        Assert.Equal("Sumatra", plant.Duplicate().NameNote);
        Assert.Equal("Sumatra", plant.Copy().NameNote);
    }

    // Validation

    [Fact]
    public void A_lost_name_can_be_saved_with_nothing_else()
    {
        Assert.Empty(new Plant { NameQualifier = NameQualifier.Unknown }.Validate(Today));
        Assert.Empty(new Propagation { NameQualifier = NameQualifier.Unknown }.Validate(Today));
        Assert.Empty(new Wish { NameQualifier = NameQualifier.Unknown }.Validate(Today));
        Assert.NotEmpty(new Plant().Validate(Today));
        Assert.NotEmpty(new Wish().Validate(Today));
    }

    [Theory]
    [InlineData(NameQualifier.Cf)]
    [InlineData(NameQualifier.Aff)]
    public void Cf_and_aff_need_a_species(NameQualifier qualifier)
    {
        Assert.Contains("cf. and aff. go with a species name.", new Plant { Genus = "Hoya", NameQualifier = qualifier }.Validate(Today));
        Assert.Contains("cf. and aff. go with a species name.", new Propagation { Genus = "Hoya", NameQualifier = qualifier }.Validate(Today));
        Assert.Contains("cf. and aff. go with a species name.", new Wish { Genus = "Hoya", NameQualifier = qualifier }.Validate(Today));
        Assert.DoesNotContain("cf. and aff. go with a species name.",
            new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = qualifier }.Validate(Today));
    }

    [Fact]
    public void Sp_needs_a_genus()
    {
        Assert.Contains("sp. goes with a genus.", new Plant { Nickname = "x", NameQualifier = NameQualifier.Sp }.Validate(Today));
        Assert.Contains("sp. goes with a genus.", new Propagation { Nickname = "x", NameQualifier = NameQualifier.Sp }.Validate(Today));
        Assert.Contains("sp. goes with a genus.", new Wish { Name = "x", NameQualifier = NameQualifier.Sp }.Validate(Today));
        Assert.Empty(new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp }.Validate(Today));
    }

    // Identified

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();

    private PlantService PlantService() => new(plants, timeline, new FixedTime(Now));

    private PropagationService PropagationService() =>
        new(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FixedTime(Now));

    [Fact]
    public async Task Setting_a_plant_to_sure_notes_when_it_was_identified()
    {
        var plant = new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Cf };
        await plants.SaveAsync(plant);
        var before = plant.Copy();
        plant.NameQualifier = null;

        await PlantService().UpdateAsync(before, plant, Label);

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Identified as Hoya carnosa", entry.Text);
        Assert.Equal(TimelineEvent.Identified, entry.Event);
    }

    [Fact]
    public async Task Finding_the_species_of_an_sp_plant_notes_it()
    {
        var plant = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp };
        await plants.SaveAsync(plant);
        var before = plant.Copy();
        plant.Species = "carnosa";
        plant.NameQualifier = null;

        await PlantService().UpdateAsync(before, plant, Label);

        Assert.Equal("Identified as Hoya carnosa", Assert.Single(timeline.Entries).Text);
    }

    [Fact]
    public async Task A_plain_edit_of_a_sure_name_adds_no_identified_entry()
    {
        var plant = new Plant { Genus = "Hoya", Species = "carnosa" };
        await plants.SaveAsync(plant);
        var before = plant.Copy();
        plant.Species = "pubicalyx";
        plant.Nickname = "Wax";

        await PlantService().UpdateAsync(before, plant, Label);

        Assert.DoesNotContain(timeline.Entries, e => e.Event == TimelineEvent.Identified);
    }

    [Fact]
    public async Task Editing_only_the_note_of_an_unsure_name_adds_no_entry()
    {
        var plant = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp };
        await plants.SaveAsync(plant);
        var before = plant.Copy();
        plant.NameNote = "Sumatra";

        await PlantService().UpdateAsync(before, plant, Label);

        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task A_lost_name_that_is_found_says_identified_with_the_name()
    {
        var plant = new Plant { NameQualifier = NameQualifier.Unknown };
        await plants.SaveAsync(plant);
        var before = plant.Copy();
        plant.NameQualifier = null;
        plant.Genus = "Monstera";

        await PlantService().UpdateAsync(before, plant, Label);

        Assert.Equal("Identified as Monstera", Assert.Single(timeline.Entries).Text);
    }

    [Fact]
    public async Task Setting_a_propagation_to_sure_notes_when_it_was_identified()
    {
        var cutting = new Propagation { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Aff, StartedOn = Today, InitialCount = 1 };
        await propagations.SaveAsync(cutting);
        var before = cutting.Copy();
        cutting.NameQualifier = null;

        await PropagationService().UpdateAsync(before, cutting, Label);

        var entry = Assert.Single(timeline.Entries, e => e.Event == TimelineEvent.Identified);
        Assert.Equal("Identified as Hoya carnosa", entry.Text);
        Assert.Equal(SubjectType.Propagation, entry.SubjectType);
    }

    [Fact]
    public async Task A_propagation_with_a_sure_name_adds_no_identified_entry()
    {
        var cutting = new Propagation { Genus = "Hoya", StartedOn = Today, InitialCount = 1 };
        await propagations.SaveAsync(cutting);
        var before = cutting.Copy();
        cutting.Species = "carnosa";

        await PropagationService().UpdateAsync(before, cutting, Label);

        Assert.DoesNotContain(timeline.Entries, e => e.Event == TimelineEvent.Identified);
    }

    // Care lookup

    [Fact]
    public void Care_lookup_ignores_the_species_for_sp_and_a_lost_name()
    {
        var book = new PlantCareBook(new PlantCareData
        {
            Plants =
            [
                new PlantCare { Name = "Dracaena", Light = LightLevel.Medium },
                new PlantCare { Name = "Dracaena trifasciata", Light = LightLevel.Low }
            ]
        });

        Assert.Equal(LightLevel.Low, book.Find("Dracaena", "trifasciata", NameQualifier.Cf)?.Light);
        Assert.Equal(LightLevel.Low, book.Find("Dracaena", "trifasciata", null)?.Light);
        Assert.Equal(LightLevel.Medium, book.Find("Dracaena", "trifasciata", NameQualifier.Sp)?.Light);
        Assert.Equal(LightLevel.Medium, book.Find("Dracaena", "trifasciata", NameQualifier.Unknown)?.Light);
    }

    // Search

    [Fact]
    public void The_plant_list_search_finds_the_note_and_the_shown_name()
    {
        var plant = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp, NameNote = "Sumatra EPC 123" };
        Assert.Single(new PlantFilter("EPC 123").Apply([plant]));
        Assert.Single(new PlantFilter("sp. sumatra").Apply([plant]));
    }

    // CSV

    [Fact]
    public void The_plant_csv_has_the_name_columns_after_cultivar()
    {
        var plant = new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp, NameNote = "Sumatra, EPC 123", CreatedAt = Now };
        var lines = CsvExport.Plants([plant, new Plant { Nickname = "Basil", CreatedAt = Now }], Places.None)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("Name,Number,Nickname,Genus,Species,Cultivar,Name sure,Locality or code,Status,", lines[0]);
        Assert.Contains(",sp.,\"Sumatra, EPC 123\",Active,", lines[2]);
        Assert.Contains(",,,,,,Active,", lines[1]);
    }

    [Fact]
    public void An_export_from_before_the_name_columns_is_still_recognised()
    {
        string[] headings = ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Status", "Room", "Acquired", "Origin", "Source", "Price paid", "Tags", "Notes", "Added"];
        var guess = ImportFields.Guess(headings);
        Assert.Equal(ImportField.Number, guess[1]);
        Assert.Equal(ImportField.Status, guess[6]);
    }

    // Import

    private static readonly PlantDictionary Dictionary = new(new PlantNameData());

    private static ImportRow Read(string[] headings, string[] cells)
    {
        var sheet = new Sheet(headings, [cells], ',');
        return Assert.Single(SpreadsheetImport.Preview(
            sheet, ImportFields.Guess(sheet.Headings), DateOrder.DayFirst, Dictionary, [], Today));
    }

    [Theory]
    [InlineData("cf", NameQualifier.Cf)]
    [InlineData("Cf.", NameQualifier.Cf)]
    [InlineData("AFF", NameQualifier.Aff)]
    [InlineData("aff.", NameQualifier.Aff)]
    [InlineData("sp", NameQualifier.Sp)]
    [InlineData("sp.", NameQualifier.Sp)]
    [InlineData("unknown", NameQualifier.Unknown)]
    [InlineData("Name lost", NameQualifier.Unknown)]
    public void The_import_reads_the_qualifier_spellings(string cell, NameQualifier expected)
    {
        var row = Read(["Genus", "Species", "Name sure", "Locality"], ["Hoya", "carnosa", cell, "Sumatra"]);

        Assert.Equal(expected, row.Draft.NameQualifier);
        Assert.Equal("Sumatra", row.Draft.NameNote);
    }

    [Fact]
    public void Headings_for_the_qualifier_and_the_note_are_recognised()
    {
        Assert.Equal(ImportField.NameSure, ImportFields.Guess(["cf/aff"])[0]);
        Assert.Equal(ImportField.NameNote, ImportFields.Guess(["Locality"])[0]);
        Assert.Equal(ImportField.NameNote, ImportFields.Guess(["Collector code"])[0]);
    }

    [Fact]
    public void An_unreadable_qualifier_is_kept_in_the_notes()
    {
        var row = Read(["Genus", "Name sure"], ["Hoya", "maybe"]);

        Assert.Null(row.Draft.NameQualifier);
        Assert.Contains("Name sure: maybe", row.Draft.Notes);
    }

    // Older data

    [Fact]
    public void A_backup_without_the_fields_still_reads()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var data = JsonSerializer.Deserialize<BackupData>("""{"version":1,"app":"Stikling","plants":[{"id":"6f1c0b3e-0000-4000-8000-000000000001","nickname":"Basil"}]}""", options)!;
        var plant = Assert.Single(data.Plants);
        Assert.Null(plant.NameQualifier);
        Assert.Null(plant.NameNote);
    }

    [Fact]
    public void A_qualifier_is_stored_as_text_and_an_unknown_one_reads_as_null()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Aff }, options);
        Assert.Contains("\"nameQualifier\":\"Aff\"", json);
        Assert.Equal(NameQualifier.Aff, JsonSerializer.Deserialize<Plant>(json, options)!.NameQualifier);

        var later = JsonSerializer.Deserialize<Plant>("""{"genus":"Hoya","nameQualifier":"Maybe"}""", options)!;
        Assert.Null(later.NameQualifier);
        Assert.Equal("Hoya", later.Genus);
    }
}
