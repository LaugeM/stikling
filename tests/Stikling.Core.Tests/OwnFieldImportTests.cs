using Stikling.Core.Backup;
using Stikling.Core.Care;
using Stikling.Core.Fields;
using Stikling.Core.Import;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

// Columns that fit no field become fields of their own: the guess, the preview and the import
public class OwnFieldImportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 18);

    private readonly FakePlantRepository plants = new();
    private readonly FakeCareLogRepository logs = new();
    private readonly FakePlaceRepository places = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FakeOwnFieldRepository fields = new();
    private readonly SpreadsheetImportService service;
    private readonly OwnFieldService fieldService;

    public OwnFieldImportTests()
    {
        var time = new FixedTime(Now);
        fieldService = new OwnFieldService(fields, plants);
        service = new SpreadsheetImportService(
            new PlantService(plants, timeline, time),
            new CareService(logs, timeline, plants, new FakeFeedRepository(), new FakeProductRepository(), new FakeOwnCareKindRepository(), time),
            new RoomService(places, plants, new FakePropagationRepository()),
            fieldService,
            plants,
            logs,
            time);
    }

    private static string Label(Enum value) => value.ToString();

    private static OwnField Live(string name, OwnFieldKind kind, params string[] choices) =>
        new() { Id = OwnField.IdFor(name), Name = name, Kind = kind, Choices = [.. choices] };

    private static ImportGuess Guess(string csv, params OwnField[] live) =>
        ImportFields.Guess(SheetReader.Read(csv), live, DateOrder.DayFirst, Today);

    private static IReadOnlyList<ImportRow> Preview(string csv, ImportGuess guess) =>
        SpreadsheetImport.Preview(SheetReader.Read(csv), guess.Matches, DateOrder.DayFirst, PlantDictionary.Empty, [], Today, guess.Targets);

    // GuessKind

    [Fact]
    public void GuessKind_looks_at_the_cells_that_are_not_empty()
    {
        Assert.Equal(OwnFieldKind.Text, ImportFields.GuessKind([], DateOrder.DayFirst, Today));
        Assert.Equal(OwnFieldKind.Text, ImportFields.GuessKind(["", "  "], DateOrder.DayFirst, Today));
        Assert.Equal(OwnFieldKind.Number, ImportFields.GuessKind(["1", "", "2,5", "-3"], DateOrder.DayFirst, Today));
        Assert.Equal(OwnFieldKind.Date, ImportFields.GuessKind(["2026-05-01", "3/4/2025", ""], DateOrder.DayFirst, Today));
        Assert.Equal(OwnFieldKind.Choice, ImportFields.GuessKind(["Full", "full", "Part", "Full"], DateOrder.DayFirst, Today));
        Assert.Equal(OwnFieldKind.Text, ImportFields.GuessKind(["a", "b", "c"], DateOrder.DayFirst, Today));
    }

    [Fact]
    public void GuessKind_needs_a_whole_day_for_a_date_and_few_values_for_a_choice()
    {
        // Only a month is not a whole day
        Assert.NotEqual(OwnFieldKind.Date, ImportFields.GuessKind(["2026-05", "2026-06"], DateOrder.DayFirst, Today));
        // Eleven different values repeated are too many to be choices
        var many = Enumerable.Range(0, 11).Select(i => $"v{i}").ToList();
        Assert.Equal(OwnFieldKind.Text, ImportFields.GuessKind([.. many, .. many], DateOrder.DayFirst, Today));
        var ten = Enumerable.Range(0, 10).Select(i => $"v{i}").ToList();
        Assert.Equal(OwnFieldKind.Choice, ImportFields.GuessKind([.. ten, .. ten], DateOrder.DayFirst, Today));
        // Numbers win over choice
        Assert.Equal(OwnFieldKind.Number, ImportFields.GuessKind(["1", "1", "2"], DateOrder.DayFirst, Today));
    }

    // Guess

    [Fact]
    public void A_heading_that_matches_no_field_becomes_a_new_field_with_a_guessed_kind()
    {
        var guess = Guess("Name,Clone name,Size\nBasil,A,1\nMint,B,2\n");

        Assert.Equal([ImportField.Name, ImportField.OwnField, ImportField.OwnField], guess.Matches);
        Assert.Null(guess.Targets[0]);
        Assert.Equal(new ImportOwnField("Clone name", OwnFieldKind.Text, null), guess.Targets[1]);
        Assert.Equal(new ImportOwnField("Size", OwnFieldKind.Number, null), guess.Targets[2]);
        Assert.Equal(OwnField.IdFor("Clone name"), guess.Targets[1]!.PreviewId);
    }

    [Fact]
    public void A_live_field_with_the_same_name_is_used_ignoring_case()
    {
        var sun = Live("Sun", OwnFieldKind.Choice, "Full");

        var guess = Guess("Name,sun\nBasil,Full\n", sun);

        Assert.Equal(ImportField.OwnField, guess.Matches[1]);
        Assert.Equal(new ImportOwnField("Sun", OwnFieldKind.Choice, sun.Id), guess.Targets[1]);
    }

    [Fact]
    public void A_removed_field_is_not_reused()
    {
        var gone = Live("Sun", OwnFieldKind.Choice, "Full");
        gone.DeletedAt = Now;

        var guess = Guess("Name,Sun\nBasil,Full\n", gone);

        Assert.True(guess.Targets[1]!.IsNew);
    }

    [Fact]
    public void Blank_and_repeated_headings_and_ones_that_repeat_a_known_field_stay_a_line_in_About_it()
    {
        var guess = Guess("Name,,Clone,clone,Notes,Comments,Column 7\nBasil,a,b,c,d,e,f\n");

        Assert.Equal(
            [ImportField.Name, ImportField.AboutLine, ImportField.OwnField, ImportField.AboutLine, ImportField.AboutIt, ImportField.AboutLine, ImportField.AboutLine],
            guess.Matches);
        Assert.Null(guess.Targets[1]);
        Assert.Null(guess.Targets[3]);
        Assert.Null(guess.Targets[5]);
    }

    [Fact]
    public void A_long_heading_is_cut_to_forty_characters()
    {
        var heading = new string('x', 55);

        var guess = Guess($"Name,{heading}\nBasil,a\n");

        Assert.Equal(40, guess.Targets[1]!.Name.Length);
    }

    [Fact]
    public void Our_own_export_maps_its_columns_and_makes_fields_of_the_extra_ones()
    {
        var clone = new OwnField { Name = "Clone name" };
        var size = new OwnField { Name = "Size", Kind = OwnFieldKind.Number };
        var plant = new Plant { Nickname = "Basil", CreatedAt = Now };
        plant.OwnFields[clone.Id] = "Clone 7";
        plant.OwnFields[size.Id] = "12";
        var csv = CsvExport.Plants([plant], Places.None, [clone, size]);

        var guess = Guess(csv);

        Assert.Equal(ImportField.Raised, guess.Matches[12]);
        Assert.Equal(ImportField.Skip, guess.Matches[18]);
        Assert.Equal([ImportField.OwnField, ImportField.OwnField], guess.Matches.Skip(19));
        Assert.Equal("Clone name", guess.Targets[19]!.Name);
        Assert.Equal(new ImportOwnField("Size", OwnFieldKind.Number, null), guess.Targets[20]);
        Assert.All(guess.Targets.Take(19), t => Assert.Null(t));
    }

    // Preview

    [Fact]
    public void Preview_stores_a_value_per_kind_under_the_id_of_the_field()
    {
        var csv = "Name,Clone,Size,Seen,Sun\nBasil,Pink,\"12,5 \",03/04/2026,Full\nMint,Blue,150 kr,2025-12-24,full\nSage,Red,2,5/6/2025,Part\n";
        var guess = Guess(csv);
        Assert.Equal(OwnFieldKind.Choice, guess.Targets[4]!.Kind);
        Assert.Equal(OwnFieldKind.Date, guess.Targets[3]!.Kind);

        // "150 kr" isn't a number to the guess, so the person picks Number, and it still reads
        var targets = guess.Targets.ToList();
        targets[2] = new ImportOwnField("Size", OwnFieldKind.Number, null);
        var rows = Preview(csv, new ImportGuess(guess.Matches, targets));

        var (clone, size, seen, sun) = (OwnField.IdFor("Clone"), OwnField.IdFor("Size"), OwnField.IdFor("Seen"), OwnField.IdFor("Sun"));
        Assert.Equal("Pink", rows[0].Draft.OwnFields[clone]);
        Assert.Equal("12.5", rows[0].Draft.OwnFields[size]);
        Assert.Equal("150", rows[1].Draft.OwnFields[size]);
        Assert.Equal("2026-04-03", rows[0].Draft.OwnFields[seen]);
        Assert.Equal("2025-12-24", rows[1].Draft.OwnFields[seen]);
        Assert.Equal("Full", rows[0].Draft.OwnFields[sun]);
        Assert.Empty(rows[0].Problems);
        Assert.Null(rows[0].Draft.Notes);
    }

    [Fact]
    public void Preview_sends_a_value_it_cannot_read_to_About_it_with_a_problem()
    {
        var csv = "Name,Size,Seen,Clone\nBasil,big,sometime,ok\nMint,1,May 2025,ok\nSage,2,2025-05-01," + new string('x', 201) + "\n";
        var guess = Guess(csv);
        var targets = guess.Targets.ToList();
        targets[1] = new ImportOwnField("Size", OwnFieldKind.Number, null);
        targets[2] = new ImportOwnField("Seen", OwnFieldKind.Date, null);

        var rows = Preview(csv, new ImportGuess(guess.Matches, targets));

        Assert.Empty(rows[0].Draft.OwnFields.Keys.Intersect([OwnField.IdFor("Size"), OwnField.IdFor("Seen")]));
        Assert.Contains("Size: big", rows[0].Draft.Notes);
        Assert.Contains("Seen: sometime", rows[0].Draft.Notes);
        Assert.Contains(rows[0].Problems, p => p == "Couldn't read \"big\" as a number for Size, kept in About it");
        Assert.Contains(rows[0].Problems, p => p == "Couldn't read \"sometime\" as a date for Seen, kept in About it");
        Assert.Contains(rows[1].Problems, p => p == "\"May 2025\" needs a whole day for Seen, kept in About it");
        Assert.Contains(rows[2].Problems, p => p.StartsWith("\"xxx") && p.EndsWith("is too long for Clone, kept in About it"));
        Assert.False(rows[2].Draft.OwnFields.ContainsKey(OwnField.IdFor("Clone")));
    }

    [Fact]
    public void Preview_puts_a_column_without_a_target_in_About_it()
    {
        var sheet = SheetReader.Read("Name,Clone\nBasil,Pink\n");

        var row = SpreadsheetImport.Preview(sheet, [ImportField.Name, ImportField.OwnField], DateOrder.DayFirst, PlantDictionary.Empty, [], Today).Single();

        Assert.Empty(row.Draft.OwnFields);
        Assert.Equal("Clone: Pink", row.Draft.Notes);
    }

    // ImportAsync

    [Fact]
    public async Task Import_makes_the_new_fields_with_the_choices_from_the_rows()
    {
        var csv = "Name,Sun,Size\nBasil,Full,1\nMint,part,2\nSage,Full,3\nRue,,4\n";
        var guess = Guess(csv);
        Assert.Equal(OwnFieldKind.Choice, guess.Targets[1]!.Kind);

        var result = await service.ImportAsync(Preview(csv, guess), Label, targets: guess.Targets);

        var sun = Assert.Single(fields.Fields.Values, f => f.Name == "Sun");
        Assert.Equal(["Full", "part"], sun.Choices);
        Assert.Equal(OwnFieldKind.Choice, sun.Kind);
        Assert.Equal(2, result.FieldsMade.Count);
        Assert.Contains(sun.Id, result.FieldsMade);
        Assert.Equal(4, result.Added);
        Assert.Equal("Full", plants.Plants[result.PlantIds[0]].OwnFields[sun.Id]);
        var size = Assert.Single(fields.Fields.Values, f => f.Name == "Size");
        Assert.Equal("4", plants.Plants[result.PlantIds[3]].OwnFields[size.Id]);
    }

    [Fact]
    public async Task Import_moves_the_values_to_the_real_id_when_a_renamed_field_holds_the_id()
    {
        // A field that was called "Clone" and is now "Lineage" still holds the id made from "Clone"
        var renamed = Live("Clone", OwnFieldKind.Text);
        renamed.Name = "Lineage";
        fields.Fields[renamed.Id] = renamed;
        var csv = "Name,Clone\nBasil,Pink\n";
        var guess = Guess(csv);
        Assert.True(guess.Targets[1]!.IsNew);

        var result = await service.ImportAsync(Preview(csv, guess), Label, targets: guess.Targets);

        var made = Assert.Single(fields.Fields.Values, f => f.Name == "Clone");
        Assert.NotEqual(renamed.Id, made.Id);
        var plant = plants.Plants[result.PlantIds.Single()];
        Assert.Equal("Pink", plant.OwnFields[made.Id]);
        Assert.False(plant.OwnFields.ContainsKey(renamed.Id));
        Assert.Equal([made.Id], result.FieldsMade);
    }

    [Fact]
    public async Task Import_adds_missing_choices_to_a_choice_field_that_is_there()
    {
        var sun = await fieldService.AddAsync("Sun", OwnFieldKind.Choice, ["Full"]);
        var csv = "Name,Sun\nBasil,full\nMint,Part\nSage,Shade\nRue,part\n";
        var guess = Guess(csv, sun);

        var result = await service.ImportAsync(Preview(csv, guess), Label, targets: guess.Targets);

        Assert.Equal(["Full", "Part", "Shade"], fields.Fields[sun.Id].Choices);
        Assert.Empty(result.FieldsMade);
    }

    [Fact]
    public async Task Import_stops_adding_choices_at_fifty()
    {
        var sun = await fieldService.AddAsync("Sun", OwnFieldKind.Choice, Enumerable.Range(0, 49).Select(i => $"c{i}"));
        var csv = "Name,Sun\nBasil,new1\nMint,new2\n";
        var guess = Guess(csv, sun);

        await service.ImportAsync(Preview(csv, guess), Label, targets: guess.Targets);

        Assert.Equal(50, fields.Fields[sun.Id].Choices.Count);
        Assert.Contains("new1", fields.Fields[sun.Id].Choices);
        Assert.DoesNotContain("new2", fields.Fields[sun.Id].Choices);
    }

    [Fact]
    public async Task Undo_removes_the_fields_it_made_but_not_the_ones_there_before_or_their_new_choices()
    {
        var sun = await fieldService.AddAsync("Sun", OwnFieldKind.Choice, ["Full"]);
        var csv = "Name,Sun,Clone\nBasil,Part,Pink\n";
        var guess = Guess(csv, sun);

        var result = await service.ImportAsync(Preview(csv, guess), Label, targets: guess.Targets);
        var clone = OwnField.IdFor("Clone");
        Assert.Equal([clone], result.FieldsMade);
        Assert.False(fields.Fields[clone].IsDeleted);

        await service.UndoAsync(result);

        Assert.True(fields.Fields[clone].IsDeleted);
        Assert.False(fields.Fields[sun.Id].IsDeleted);
        Assert.Contains("Part", fields.Fields[sun.Id].Choices);
        Assert.Empty(await plants.GetAllAsync());
    }

    [Fact]
    public async Task A_removed_field_that_comes_back_counts_as_made()
    {
        var old = await fieldService.AddAsync("Clone", OwnFieldKind.Text);
        await fieldService.RemoveAsync(old.Id);
        var csv = "Name,Clone\nBasil,Pink\n";
        var guess = Guess(csv);

        var result = await service.ImportAsync(Preview(csv, guess), Label, targets: guess.Targets);

        Assert.Equal([old.Id], result.FieldsMade);
        Assert.False(fields.Fields[old.Id].IsDeleted);
        await service.UndoAsync(result);
        Assert.True(fields.Fields[old.Id].IsDeleted);
    }

    [Fact]
    public async Task Import_without_targets_works_as_before()
    {
        var sheet = SheetReader.Read("Name\nBasil\n");
        var rows = SpreadsheetImport.Preview(sheet, [ImportField.Name], DateOrder.DayFirst, PlantDictionary.Empty, [], Today);

        var result = await service.ImportAsync(rows, Label);

        Assert.Equal(1, result.Added);
        Assert.Empty(result.FieldsMade);
        Assert.Empty(fields.Fields);
    }
}
