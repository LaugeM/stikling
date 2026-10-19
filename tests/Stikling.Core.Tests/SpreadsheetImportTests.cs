using Stikling.Core.Backup;
using Stikling.Core.Import;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class SpreadsheetImportTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static readonly PlantDictionary Dictionary = new(new PlantNameData
    {
        Genera =
        [
            new GenusNames { Name = "Alocasia", Species = ["baginda", "zebrina"], Cultivars = [new("Polly")] },
            new GenusNames
            {
                Name = "Monstera",
                Species = ["adansonii", "deliciosa"],
                Cultivars = [new("Thai Constellation", "deliciosa")]
            }
        ]
    });

    /// <summary>One row under the headings, with the columns matched the way the test says.</summary>
    private static ImportRow One(string[] headings, string[] cells, ImportField[] fields, IReadOnlyList<Plant>? existing = null, DateOrder order = DateOrder.DayFirst) =>
        Assert.Single(SpreadsheetImport.Preview(
            new Sheet(headings, [cells], ','), fields, order, Dictionary, existing ?? [], Today));

    private static ImportRow One(string cell, ImportField field, IReadOnlyList<Plant>? existing = null) =>
        One(["Column"], [cell], [field], existing);

    // Guessing

    [Fact]
    public void Our_own_export_is_recognised()
    {
        var headings = SheetReader.Read(CsvExport.Plants([], new Rooms.Places([]), [])).Headings;
        Assert.Equal(
        [
            ImportField.Skip, ImportField.Number, ImportField.Nickname, ImportField.Genus, ImportField.Species, ImportField.Cultivar, ImportField.NameSure, ImportField.NameNote,
            ImportField.Status, ImportField.Place, ImportField.GotOn, ImportField.Origin, ImportField.Raised, ImportField.Source,
            ImportField.Price, ImportField.HowMany, ImportField.Tags, ImportField.AboutIt, ImportField.Skip
        ], ImportFields.Guess(headings));
    }

    [Fact]
    public void Our_own_export_with_columns_for_own_fields_is_recognised_and_the_extra_columns_are_kept_in_About_it()
    {
        var field = new OwnField { Name = "Clone name" };
        var headings = SheetReader.Read(CsvExport.Plants([], new Rooms.Places([]), [field])).Headings;
        var guess = ImportFields.Guess(headings);

        Assert.Equal(headings.Count, guess.Count);
        Assert.Equal(ImportField.Raised, guess[12]);
        Assert.Equal(ImportField.Skip, guess[18]);
        Assert.Equal(ImportField.AboutLine, guess[19]);
    }

    [Fact]
    public void An_export_from_before_the_raised_column_is_still_recognised()
    {
        string[] headings = ["Name", "Number", "Nickname", "Genus", "Species", "Cultivar", "Name sure", "Locality or code", "Status", "Room", "Acquired", "Origin", "Source", "Price paid", "Tags", "Notes", "Added"];
        var guess = ImportFields.Guess(headings);

        Assert.Equal(headings.Length, guess.Count);
        Assert.DoesNotContain(ImportField.Raised, guess);
        Assert.Equal(ImportField.Origin, guess[11]);
        Assert.Equal(ImportField.Source, guess[12]);
        Assert.Equal(ImportField.Skip, guess[^1]);
    }

    [Fact]
    public void An_export_from_before_the_number_column_is_still_recognised()
    {
        string[] headings = ["Name", "Nickname", "Genus", "Species", "Cultivar", "Status", "Room", "Acquired", "Origin", "Source", "Price paid", "Tags", "Notes", "Added"];
        var guess = ImportFields.Guess(headings);

        Assert.Equal(headings.Length, guess.Count);
        Assert.DoesNotContain(ImportField.Number, guess);
        Assert.Equal(ImportField.Nickname, guess[1]);
        Assert.Equal(ImportField.Skip, guess[^1]);
    }

    [Fact]
    public void Number_headings_are_recognised()
    {
        Assert.Equal(ImportField.Number, ImportFields.Guess(["Number"])[0]);
        Assert.Equal(ImportField.Number, ImportFields.Guess(["Collection number"])[0]);
        Assert.Equal(ImportField.Number, ImportFields.Guess(["No"])[0]);
        Assert.Equal(ImportField.Number, ImportFields.Guess(["No."])[0]);
        Assert.Equal(ImportField.Number, ImportFields.Guess(["#"])[0]);
    }

    [Fact]
    public void A_number_is_normalized_and_kept_when_it_is_valid()
    {
        var row = One(" #117C ", ImportField.Number);

        Assert.Equal("117c", row.Draft.CollectionNumber);
        Assert.Empty(row.Problems);
    }

    [Fact]
    public void A_number_that_is_not_valid_goes_to_About_it()
    {
        var row = One(["Name", "No"], ["Basil", "twelve"], [ImportField.Name, ImportField.Number]);

        Assert.Null(row.Draft.CollectionNumber);
        Assert.Single(row.Problems);
        Assert.Contains("No: twelve", row.Draft.Notes);
    }

    [Fact]
    public void Danish_headings()
    {
        Assert.Equal(
            [ImportField.Name, ImportField.Place, ImportField.GotOn, ImportField.Price, ImportField.LastWatered, ImportField.Genus],
            ImportFields.Guess(["Navn", "Rum", "Købt", "Pris", "Vandet", "Slægt"]));
    }

    [Fact]
    public void Headings_are_matched_without_case_punctuation_or_extra_spaces()
    {
        Assert.Equal(
            [ImportField.GotOn, ImportField.Price, ImportField.LastWatered, ImportField.AboutIt, ImportField.Source],
            ImportFields.Guess(["DATE_ACQUIRED", "Price paid:", "  last   watered ", "Comments!", "Bought from"]));
    }

    [Fact]
    public void A_field_goes_to_the_first_column_that_matches_and_the_rest_are_added_to_About_it()
    {
        Assert.Equal(
            [ImportField.Name, ImportField.AboutLine, ImportField.AboutIt, ImportField.AboutLine],
            ImportFields.Guess(["Name", "Plant", "Notes", "Comment"]));
    }

    [Fact]
    public void Unknown_and_numbered_headings_are_added_to_About_it()
    {
        Assert.Equal(
            [ImportField.AboutLine, ImportField.AboutLine, ImportField.Name],
            ImportFields.Guess(["Colour", "Column 2", "Name"]));
    }

    [Fact]
    public void Every_field_has_a_label()
    {
        foreach (var field in Enum.GetValues<ImportField>())
            Assert.False(string.IsNullOrWhiteSpace(ImportFields.Label(field)));
        Assert.Equal("Leave out", ImportFields.Label(ImportField.Skip));
        Assert.Equal("Date you got it", ImportFields.Label(ImportField.GotOn));
    }

    [Fact]
    public void Every_field_is_in_one_picker_group()
    {
        var grouped = ImportFields.Groups.SelectMany(g => g.Fields).ToList();
        Assert.Equal(grouped.Count, grouped.Distinct().Count());
        Assert.Equal(Enum.GetValues<ImportField>().Order(), grouped.Order());
    }

    [Fact]
    public void Guess_reads_where_as_the_room()
    {
        Assert.Equal([ImportField.Name, ImportField.Place], ImportFields.Guess(["Plant", "Where"]));
    }

    // QuickAdd.ReadName

    [Fact]
    public void ReadName_reads_a_name_the_way_quick_add_does()
    {
        var plant = QuickAdd.ReadName("Monstera deliciosa 'Thai Constellation' from the swap", Dictionary);
        Assert.Equal(("Monstera", "deliciosa", "Thai Constellation"), (plant.Genus, plant.Species, plant.Cultivar));
        Assert.Equal("from the swap", plant.Notes);

        var unknown = QuickAdd.ReadName("Big basil", Dictionary);
        Assert.Equal("Big basil", unknown.Nickname);
    }

    [Fact]
    public void ReadName_of_nothing_is_a_plant_with_nothing_set()
    {
        var plant = QuickAdd.ReadName("  ", Dictionary);
        Assert.Null(plant.Nickname);
        Assert.Null(plant.Genus);
        Assert.Null(plant.Notes);
    }

    // Preview

    [Fact]
    public void A_name_column_is_read_like_quick_add_and_the_rest_goes_in_About_it()
    {
        var row = One(["Plant", "Notes"], ["Alocasia zebrina from the swap", "Big one"], [ImportField.Name, ImportField.AboutIt]);
        Assert.Equal(("Alocasia", "zebrina"), (row.Draft.Genus, row.Draft.Species));
        Assert.Null(row.Draft.Nickname);
        Assert.Equal("Big one\nfrom the swap", row.Draft.Notes);
        Assert.Null(row.CantAdd);
        Assert.True(row.TickedByDefault);
    }

    [Fact]
    public void An_unknown_name_is_the_nickname_unless_a_nickname_column_has_one()
    {
        Assert.Equal("Big basil", One("Big basil", ImportField.Name).Draft.Nickname);

        var row = One(["Name", "Nick"], ["Big basil", "Basil"], [ImportField.Name, ImportField.Nickname]);
        Assert.Equal("Basil", row.Draft.Nickname);
        Assert.Equal("Name: Big basil", row.Draft.Notes);

        var same = One(["Name", "Nick"], ["Basil", "basil"], [ImportField.Name, ImportField.Nickname]);
        Assert.Null(same.Draft.Notes);
    }

    [Fact]
    public void A_name_next_to_a_genus_and_a_nickname_is_kept_in_About_it()
    {
        var row = One(["Name", "Genus", "Nick"], ["Window monstera", "Monstera", "Big one"],
            [ImportField.Name, ImportField.Genus, ImportField.Nickname]);

        Assert.Equal("Big one", row.Draft.Nickname);
        Assert.Equal("Name: Window monstera", row.Draft.Notes);
    }

    [Fact]
    public void A_name_next_to_a_genus_column_is_the_nickname_when_it_differs_from_the_botanical_name()
    {
        string[] headings = ["Name", "Genus", "Species"];
        ImportField[] fields = [ImportField.Name, ImportField.Genus, ImportField.Species];

        var nicknamed = One(headings, ["Big Monstera", "Monstera", "deliciosa"], fields);
        Assert.Equal("Big Monstera", nicknamed.Draft.Nickname);
        Assert.Equal(("Monstera", "deliciosa"), (nicknamed.Draft.Genus, nicknamed.Draft.Species));

        var same = One(headings, ["monstera DELICIOSA", "Monstera", "deliciosa"], fields);
        Assert.Null(same.Draft.Nickname);
    }

    [Fact]
    public void A_cultivar_is_stored_without_quotes()
    {
        string[] headings = ["Genus", "Cultivar"];
        ImportField[] fields = [ImportField.Genus, ImportField.Cultivar];
        Assert.Equal("Thai Con", One(headings, ["Monstera", "'Thai Con'"], fields).Draft.Cultivar);
        Assert.Equal("Thai Con", One(headings, ["Monstera", "\"Thai Con\""], fields).Draft.Cultivar);
    }

    [Fact]
    public void Values_that_cant_be_read_are_kept_in_About_it_with_a_problem()
    {
        string[] headings = ["Name", "Notes", "Colour", "Got", "Price", "Soil", "Status", "How"];
        var row = One(
            headings,
            ["Basil", "Nice", "Green", "31/02", "lots", "hydro", "zombie", "found"],
            [ImportField.Name, ImportField.AboutIt, ImportField.AboutLine, ImportField.GotOn, ImportField.Price,
             ImportField.Medium, ImportField.Status, ImportField.Origin]);

        string[] expected =
        [
            "Couldn't read \"31/02\" as a date, kept in About it",
            "Didn't know \"found\" as an origin, kept in About it",
            "Didn't know \"hydro\" as a medium, kept in About it",
            "Didn't know \"zombie\" as a status, kept in About it",
            "Couldn't read \"lots\" as a price, kept in About it"
        ];
        Assert.Equal(expected.Order(), row.Problems.Order());
        Assert.Equal("Nice\nColour: Green\nGot: 31/02\nPrice: lots\nSoil: hydro\nStatus: zombie\nHow: found", row.Draft.Notes);

        // The plant gets what the model starts with
        Assert.Null(row.Draft.AcquiredOn);
        Assert.Null(row.Draft.PricePaid);
        Assert.Equal(GrowingMedium.Soil, row.Draft.Medium);
        Assert.Equal(PlantStatus.Active, row.Draft.Status);
        Assert.Equal(PlantOrigin.Purchased, row.Draft.Origin);
        Assert.Null(row.CantAdd);
    }

    [Fact]
    public void A_date_you_got_it_in_the_future_is_kept_in_About_it()
    {
        var row = One(["Name", "Got"], ["Basil", "2027"], [ImportField.Name, ImportField.GotOn]);
        Assert.Null(row.Draft.AcquiredOn);
        Assert.Equal("\"2027\" is in the future, kept in About it", Assert.Single(row.Problems));
        Assert.Equal("Got: 2027", row.Draft.Notes);
    }

    [Fact]
    public void Values_that_can_be_read_fill_the_plant()
    {
        var row = One(
            ["Name", "Got", "Origin", "From", "Price", "Soil", "Status", "Tags"],
            ["Basil", "3. maj 2024", "gave", "Anna", "150 kr", "LECA", "dead", "Rare; for swap"],
            [ImportField.Name, ImportField.GotOn, ImportField.Origin, ImportField.Source, ImportField.Price,
             ImportField.Medium, ImportField.Status, ImportField.Tags]);

        Assert.Equal("2024-05-03", row.Draft.AcquiredOn?.ToString());
        Assert.Equal(PlantOrigin.Gift, row.Draft.Origin);
        Assert.Equal("Anna", row.Draft.Source);
        Assert.Equal(150m, row.Draft.PricePaid);
        Assert.Equal(GrowingMedium.Leca, row.Draft.Medium);
        Assert.Equal(PlantStatus.Died, row.Draft.Status);
        Assert.Null(row.Draft.StatusSince);
        Assert.Equal(["Rare", "for swap"], row.Draft.Tags);
        Assert.Empty(row.Problems);
        Assert.Null(row.Draft.Notes);
    }

    [Fact]
    public void Care_dates_become_entries_when_they_are_a_day_that_has_passed()
    {
        var row = One(
            ["Name", "Watered", "Fed", "Repotted"],
            ["Basil", "1/10/2026", "2026-10", "2027-01-01"],
            [ImportField.Name, ImportField.LastWatered, ImportField.LastFed, ImportField.LastRepotted]);

        Assert.Equal(new ImportCare(CareKind.Watered, new DateOnly(2026, 10, 1)), Assert.Single(row.Care));
        Assert.Equal(2, row.Problems.Count);
        Assert.Equal("Fed: 2026-10\nRepotted: 2027-01-01", row.Draft.Notes);
    }

    [Fact]
    public void A_care_date_that_isnt_a_date_is_kept_in_About_it()
    {
        var row = One(["Name", "Watered"], ["Basil", "yesterday"], [ImportField.Name, ImportField.LastWatered]);
        Assert.Empty(row.Care);
        Assert.Equal("Couldn't read \"yesterday\" as a date, kept in About it", Assert.Single(row.Problems));
        Assert.Equal("Watered: yesterday", row.Draft.Notes);
    }

    [Fact]
    public void The_order_decides_how_a_date_is_read()
    {
        string[] headings = ["Name", "Got"];
        ImportField[] fields = [ImportField.Name, ImportField.GotOn];
        Assert.Equal("2024-04-03", One(headings, ["Basil", "3/4/2024"], fields, order: DateOrder.DayFirst).Draft.AcquiredOn?.ToString());
        Assert.Equal("2024-03-04", One(headings, ["Basil", "3/4/2024"], fields, order: DateOrder.MonthFirst).Draft.AcquiredOn?.ToString());
    }

    [Fact]
    public void A_row_with_no_name_cant_be_added()
    {
        var row = One(["Name", "Room"], ["", "Kitchen"], [ImportField.Name, ImportField.Place]);
        Assert.Equal("No name", row.CantAdd);
        Assert.False(row.TickedByDefault);
    }

    [Fact]
    public void A_species_alone_is_not_a_name()
    {
        Assert.Equal("No name", One("deliciosa", ImportField.Species).CantAdd);
    }

    [Fact]
    public void The_place_is_kept_as_written_with_the_spot_after_a_slash()
    {
        Assert.Equal("Living room / Window", One(["Name", "Room"], ["Basil", "Living room/Window"], [ImportField.Name, ImportField.Place]).Place);
        Assert.Equal("Living room / Window", One(["Name", "Room"], ["Basil", " Living room / Window "], [ImportField.Name, ImportField.Place]).Place);
        Assert.Null(One(["Name", "Room"], ["Basil", ""], [ImportField.Name, ImportField.Place]).Place);
    }

    [Fact]
    public void A_plant_that_died_or_left_also_counts_as_one_you_have()
    {
        List<Plant> existing =
        [
            new() { Nickname = "Basil", Status = PlantStatus.Died },
            new() { Nickname = "Pilea", Status = PlantStatus.GivenAway },
            new() { Nickname = "Fern", DeletedAt = DateTimeOffset.UnixEpoch }
        ];

        Assert.True(One("Basil", ImportField.Name, existing).LooksLikeYours);
        Assert.True(One("Pilea", ImportField.Name, existing).LooksLikeYours);
        Assert.False(One("Fern", ImportField.Name, existing).LooksLikeYours);
    }

    [Fact]
    public void A_plant_you_already_have_with_the_same_name_and_date_is_not_ticked()
    {
        List<Plant> existing = [new() { Nickname = "Basil", AcquiredOn = LooseDate.Of(new DateOnly(2024, 4, 3)) }];
        string[] headings = ["Name", "Got"];
        ImportField[] fields = [ImportField.Name, ImportField.GotOn];

        var same = One(headings, ["Basil", "2024-04-03"], fields, existing);
        Assert.True(same.LooksLikeYours);
        Assert.False(same.TickedByDefault);
        Assert.Null(same.CantAdd);

        Assert.False(One(headings, ["Basil", "2025-01-01"], fields, existing).LooksLikeYours);
        Assert.False(One(headings, ["Basil", ""], fields, existing).LooksLikeYours);
        Assert.False(One(headings, ["Mint", "2024-04-03"], fields, existing).LooksLikeYours);
    }

    [Fact]
    public void Two_plants_with_no_date_look_alike()
    {
        List<Plant> existing = [new() { Genus = "Monstera", Species = "deliciosa" }];
        Assert.True(One("monstera deliciosa", ImportField.Name, existing).LooksLikeYours);
    }

    [Fact]
    public void Lines_count_the_heading_row_as_one()
    {
        var withHeadings = SheetReader.Read("Name\nBasil\n\nMint");
        var lines = SpreadsheetImport.Preview(withHeadings, [ImportField.Name], DateOrder.DayFirst, Dictionary, [], Today).Select(r => r.Line);
        Assert.Equal([2, 4], lines);

        var without = SheetReader.Read("Basil\nMint", firstRowIsHeadings: false);
        lines = SpreadsheetImport.Preview(without, [ImportField.Name], DateOrder.DayFirst, Dictionary, [], Today).Select(r => r.Line);
        Assert.Equal([1, 2], lines);
    }

    [Fact]
    public void Skipped_columns_are_left_out_of_About_it()
    {
        var row = One(["Name", "Secret"], ["Basil", "x"], [ImportField.Name, ImportField.Skip]);
        Assert.Null(row.Draft.Notes);
    }

    // Reading our own export back

    [Fact]
    public void An_export_reads_back_as_the_same_plants()
    {
        var places = new FakePlaceRepository();
        var big = new Plant
        {
            Nickname = "Big, \"Thai\" one",
            Genus = "Monstera",
            Species = "deliciosa",
            Cultivar = "Thai Constellation",
            PlaceId = places.IdOf("Living room / Window"),
            AcquiredOn = LooseDate.Of(new DateOnly(2024, 4, 3)),
            Origin = PlantOrigin.Gift,
            Source = "Anna",
            Status = PlantStatus.Died,
            PricePaid = 150.5m,
            Tags = ["rare", "for swap"],
            Notes = "Line 1\nLine 2, with a comma"
        };
        var basil = new Plant { Nickname = "Basil", AcquiredOn = LooseDate.Of(2023, 6), PlaceId = places.IdOf("Kitchen") };
        var bare = new Plant { Genus = "Alocasia" };

        var csv = CsvExport.Plants([big, basil, bare], places.Current, []);
        var sheet = SheetReader.Read(SheetReader.Decode(CsvExport.Encode(csv)));
        var rows = SpreadsheetImport.Preview(sheet, ImportFields.Guess(sheet.Headings), DateOrder.DayFirst, Dictionary, [], Today);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.True(r.TickedByDefault));

        var back = rows.Single(r => r.Draft.Nickname == big.Nickname);
        Assert.Equal(
            (big.Genus, big.Species, big.Cultivar, big.Status, big.Origin, big.Source, big.PricePaid, big.Notes),
            (back.Draft.Genus, back.Draft.Species, back.Draft.Cultivar, back.Draft.Status, back.Draft.Origin, back.Draft.Source, back.Draft.PricePaid, back.Draft.Notes));
        Assert.Equal(big.AcquiredOn, back.Draft.AcquiredOn);
        Assert.Equal(big.Tags, back.Draft.Tags);
        Assert.Equal("Living room / Window", back.Place);
        Assert.Empty(back.Problems);

        var basilBack = rows.Single(r => r.Draft.Nickname == "Basil");
        Assert.Equal(basil.AcquiredOn, basilBack.Draft.AcquiredOn);
        Assert.Equal("Kitchen", basilBack.Place);
        Assert.Null(basilBack.Draft.Notes);

        var bareBack = rows.Single(r => r.Draft.Genus == "Alocasia");
        Assert.Null(bareBack.Draft.Nickname);
        Assert.Null(bareBack.Place);
    }
}
