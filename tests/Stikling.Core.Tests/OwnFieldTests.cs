using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Fields;
using Stikling.Core.Import;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class OwnFieldTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly Names.PlantDictionary Dictionary = new(new Names.PlantNameData());

    private readonly FakeOwnFieldRepository fields = new();
    private readonly FakePlantRepository plants = new();
    private readonly OwnFieldService service;

    public OwnFieldTests() => service = new OwnFieldService(fields, plants);

    private Plant AddPlant(string name, params (Guid Field, string Value)[] values)
    {
        var plant = new Plant { Nickname = name, CreatedAt = Now };
        foreach (var (field, value) in values)
            plant.OwnFields[field] = value;
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private string? ValueOn(Plant plant, Guid field) =>
        plants.Plants[plant.Id].OwnFields.TryGetValue(field, out var value) ? value : null;

    // Adding

    [Fact]
    public async Task The_same_name_twice_is_one_field()
    {
        var first = await service.AddAsync("Clone name", OwnFieldKind.Text);
        var second = await service.AddAsync(" clone name ", OwnFieldKind.Number);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fields.Fields);
        Assert.Equal(OwnFieldKind.Text, second.Kind);
        Assert.Equal(OwnField.IdFor("Clone name"), first.Id);
    }

    [Fact]
    public async Task A_name_needs_to_be_there_and_not_too_long()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync("  ", OwnFieldKind.Text));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(new string('a', 41), OwnFieldKind.Text));
        Assert.Equal("A field name can be at most 40 characters.", error.Message);
    }

    [Fact]
    public async Task Choices_are_trimmed_and_empty_ones_and_repeats_are_dropped()
    {
        var field = await service.AddAsync("Sun", OwnFieldKind.Choice, [" Yes ", "", "no", "NO", "Maybe"]);

        Assert.Equal(["Yes", "no", "Maybe"], field.Choices);
    }

    [Fact]
    public void Validate_checks_the_choices()
    {
        Assert.Contains("Each choice can only be there once.", new OwnField { Name = "A", Choices = ["x", "X"] }.Validate());
        Assert.NotEmpty(new OwnField { Name = "A", Choices = [" "] }.Validate());
        Assert.NotEmpty(new OwnField { Name = "A", Choices = [new string('a', 41)] }.Validate());
        Assert.NotEmpty(new OwnField { Name = "A", Choices = [.. Enumerable.Range(0, 51).Select(i => $"c{i}")] }.Validate());
        Assert.Empty(new OwnField { Name = "A", Choices = ["x", "y"] }.Validate());
    }

    [Fact]
    public async Task Adding_a_removed_name_brings_it_back_and_the_values_on_plants_show_again()
    {
        var field = await service.AddAsync("Awards", OwnFieldKind.Text);
        var plant = AddPlant("Big", (field.Id, "Best in show"));
        await service.RemoveAsync(field.Id);

        Assert.Empty(await service.GetAllAsync());
        Assert.Equal("Best in show", ValueOn(plant, field.Id));

        var back = await service.AddAsync("Awards", OwnFieldKind.Text);

        Assert.Equal(field.Id, back.Id);
        Assert.False(back.IsDeleted);
        Assert.Equal(1, await service.UseCountAsync(back.Id));
    }

    [Fact]
    public async Task A_renamed_field_that_holds_the_id_means_a_new_id_for_the_new_one()
    {
        var old = await service.AddAsync("Clone", OwnFieldKind.Text);
        await service.RenameAsync(old.Id, "Clone name");

        var again = await service.AddAsync("Clone", OwnFieldKind.Text);

        Assert.NotEqual(old.Id, again.Id);
        Assert.Equal(2, (await service.GetAllAsync()).Count);
    }

    [Fact]
    public async Task A_field_renamed_then_removed_comes_back_under_its_last_name_not_its_first()
    {
        var field = await service.AddAsync("Clone name", OwnFieldKind.Text);
        var plant = AddPlant("Big", (field.Id, "Pink"));
        await service.RenameAsync(field.Id, "Cultivar code");
        await service.RemoveAsync(field.Id);

        var first = await service.AddAsync("Clone name", OwnFieldKind.Text);
        Assert.NotEqual(field.Id, first.Id);
        Assert.Equal(0, await service.UseCountAsync(first.Id));

        var last = await service.AddAsync("Cultivar code", OwnFieldKind.Text);
        Assert.Equal(field.Id, last.Id);
        Assert.Equal("Pink", ValueOn(plant, last.Id));
    }

    [Fact]
    public async Task A_removed_field_that_lost_its_name_in_a_sync_comes_back_by_its_id()
    {
        var field = await service.AddAsync("Awards", OwnFieldKind.Text);
        AddPlant("Big", (field.Id, "Best in show"));
        await service.RemoveAsync(field.Id);
        fields.Fields[field.Id].Name = ""; // the server keeps only the id and dates of a deleted record

        var back = await service.AddAsync("Awards", OwnFieldKind.Text);

        Assert.Equal(field.Id, back.Id);
        Assert.Equal("Awards", back.Name);
        Assert.Equal(1, await service.UseCountAsync(back.Id));
    }

    // Renaming

    [Fact]
    public async Task Rename_keeps_the_id_and_refuses_a_name_another_field_has()
    {
        var clone = await service.AddAsync("Clone", OwnFieldKind.Text);
        await service.AddAsync("Awards", OwnFieldKind.Text);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenameAsync(clone.Id, "awards"));
        Assert.Equal("You already have a field called awards.", error.Message);

        await service.RenameAsync(clone.Id, "Clone name");
        Assert.Equal("Clone name", (await fields.GetAsync(clone.Id))!.Name);
    }

    // Choices

    [Fact]
    public async Task Renaming_a_choice_rewrites_the_plants_that_have_it()
    {
        var field = await service.AddAsync("Sun", OwnFieldKind.Choice, ["Yes", "No"]);
        var a = AddPlant("A", (field.Id, "yes"));
        var b = AddPlant("B", (field.Id, "No"));

        var changed = await service.RenameChoiceAsync(field.Id, "Yes", "Full sun");

        Assert.Equal(1, changed);
        Assert.Equal("Full sun", ValueOn(a, field.Id));
        Assert.Equal("No", ValueOn(b, field.Id));
        Assert.Equal(["Full sun", "No"], (await fields.GetAsync(field.Id))!.Choices);
    }

    [Fact]
    public async Task Renaming_a_choice_to_an_existing_one_merges_them()
    {
        var field = await service.AddAsync("Sun", OwnFieldKind.Choice, ["Yes", "Sometimes", "No"]);
        var a = AddPlant("A", (field.Id, "Sometimes"));

        await service.RenameChoiceAsync(field.Id, "Sometimes", "yes");

        Assert.Equal(["Yes", "No"], (await fields.GetAsync(field.Id))!.Choices);
        Assert.Equal("Yes", ValueOn(a, field.Id));
    }

    [Fact]
    public async Task Removing_a_choice_keeps_the_value_on_the_plants()
    {
        var field = await service.AddAsync("Sun", OwnFieldKind.Choice, ["Yes", "No"]);
        var plant = AddPlant("A", (field.Id, "No"));

        await service.RemoveChoiceAsync(field.Id, "no");

        Assert.Equal(["Yes"], (await fields.GetAsync(field.Id))!.Choices);
        Assert.Equal("No", ValueOn(plant, field.Id));
    }

    [Fact]
    public async Task Adding_a_choice_leaves_one_that_is_there_alone()
    {
        var field = await service.AddAsync("Sun", OwnFieldKind.Choice, ["Yes"]);

        await service.AddChoiceAsync(field.Id, "yes");
        await service.AddChoiceAsync(field.Id, " No ");

        Assert.Equal(["Yes", "No"], (await fields.GetAsync(field.Id))!.Choices);
    }

    // Kind

    [Fact]
    public async Task Becoming_a_choice_takes_the_values_in_use_as_the_choices()
    {
        var field = await service.AddAsync("Sun", OwnFieldKind.Text);
        AddPlant("A", (field.Id, "shade"));
        AddPlant("B", (field.Id, "Shade"));
        AddPlant("C", (field.Id, "bright"));
        AddPlant("D");

        await service.SetKindAsync(field.Id, OwnFieldKind.Choice);

        var saved = (await fields.GetAsync(field.Id))!;
        Assert.Equal(OwnFieldKind.Choice, saved.Kind);
        Assert.Equal(["bright", "shade"], saved.Choices);
        Assert.Equal(3, await service.UseCountAsync(field.Id));
    }

    [Fact]
    public async Task Changing_the_kind_counts_the_values_that_do_not_fit_and_changes_none_of_them()
    {
        var field = await service.AddAsync("Height", OwnFieldKind.Text);
        var a = AddPlant("A", (field.Id, "12.5"));
        var b = AddPlant("B", (field.Id, "tall"));

        Assert.Equal(1, await service.NotFittingCountAsync(field.Id, OwnFieldKind.Number));
        Assert.Equal(2, await service.NotFittingCountAsync(field.Id, OwnFieldKind.Date));
        await service.SetKindAsync(field.Id, OwnFieldKind.Number);

        Assert.Equal("12.5", ValueOn(a, field.Id));
        Assert.Equal("tall", ValueOn(b, field.Id));
    }

    [Fact]
    public async Task Removing_a_field_keeps_the_values_on_the_plants()
    {
        var field = await service.AddAsync("Awards", OwnFieldKind.Text);
        var plant = AddPlant("A", (field.Id, "Gold"));

        await service.RemoveAsync(field.Id);

        Assert.Equal("Gold", ValueOn(plant, field.Id));
        Assert.True(fields.Fields[field.Id].IsDeleted);
    }

    // Values

    [Fact]
    public async Task Setting_a_value_sets_clears_and_tidies_it_for_the_kind()
    {
        var height = await service.AddAsync("Height", OwnFieldKind.Number);
        var a = AddPlant("A");
        var b = AddPlant("B", (height.Id, "20"));

        Assert.Equal(2, await service.SetValueAsync([a.Id, b.Id], height.Id, " 12,50 "));
        Assert.Equal("12.5", ValueOn(a, height.Id));
        Assert.Equal("12.5", ValueOn(b, height.Id));

        Assert.Equal(0, await service.SetValueAsync([a.Id, b.Id], height.Id, "12.5"));

        Assert.Equal(2, await service.SetValueAsync([a.Id, b.Id], height.Id, " "));
        Assert.False(plants.Plants[a.Id].OwnFields.ContainsKey(height.Id));
        Assert.False(plants.Plants[b.Id].OwnFields.ContainsKey(height.Id));
    }

    [Fact]
    public async Task Setting_a_value_that_is_not_a_number_is_refused_and_nothing_changes()
    {
        var height = await service.AddAsync("Height", OwnFieldKind.Number);
        var plant = AddPlant("A");
        var saves = plants.Saves;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetValueAsync([plant.Id], height.Id, "tall"));

        Assert.Equal("That isn't a number.", error.Message);
        Assert.Equal(saves, plants.Saves);
        Assert.Empty(plants.Plants[plant.Id].OwnFields);
    }

    [Fact]
    public async Task Setting_a_value_leaves_the_other_plants_and_the_other_fields_alone()
    {
        var a = await service.AddAsync("A", OwnFieldKind.Text);
        var b = await service.AddAsync("B", OwnFieldKind.Text);
        var one = AddPlant("One", (b.Id, "keep"));
        var two = AddPlant("Two");

        await service.SetValueAsync([one.Id], a.Id, "x");

        Assert.Equal("x", ValueOn(one, a.Id));
        Assert.Equal("keep", ValueOn(one, b.Id));
        Assert.Null(ValueOn(two, a.Id));
    }

    // Values by kind

    [Theory]
    [InlineData(OwnFieldKind.Text, "  hello  ", "hello")]
    [InlineData(OwnFieldKind.Choice, " Yes ", "Yes")]
    [InlineData(OwnFieldKind.Number, "12,5", "12.5")]
    [InlineData(OwnFieldKind.Number, "12.50", "12.5")]
    [InlineData(OwnFieldKind.Number, "7", "7")]
    [InlineData(OwnFieldKind.Number, "-3.25", "-3.25")]
    [InlineData(OwnFieldKind.Date, "2026-10-05", "2026-10-05")]
    public void Normalize_gives_the_stored_text(OwnFieldKind kind, string input, string expected)
    {
        Assert.Equal(expected, OwnFieldValues.Normalize(kind, input, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(OwnFieldKind.Text)]
    [InlineData(OwnFieldKind.Number)]
    [InlineData(OwnFieldKind.Date)]
    [InlineData(OwnFieldKind.Choice)]
    public void Nothing_typed_is_nothing_stored(OwnFieldKind kind)
    {
        Assert.Null(OwnFieldValues.Normalize(kind, "   ", out var error));
        Assert.Null(error);
        Assert.Null(OwnFieldValues.Normalize(kind, null, out error));
        Assert.Null(error);
    }

    [Fact]
    public void Normalize_says_what_is_wrong()
    {
        Assert.Null(OwnFieldValues.Normalize(OwnFieldKind.Number, "tall", out var error));
        Assert.Equal("That isn't a number.", error);
        Assert.Null(OwnFieldValues.Normalize(OwnFieldKind.Date, "5 Oct", out error));
        Assert.Equal("That isn't a date.", error);
        Assert.Null(OwnFieldValues.Normalize(OwnFieldKind.Text, new string('a', 201), out error));
        Assert.Equal("Keep it under 200 characters.", error);
        Assert.Equal(new string('a', 200), OwnFieldValues.Normalize(OwnFieldKind.Text, new string('a', 200), out error));
    }

    [Fact]
    public void A_date_is_shown_as_the_plant_page_shows_a_day_and_a_value_that_does_not_fit_as_written()
    {
        Assert.Equal("5 Oct 2026", OwnFieldValues.Display(OwnFieldKind.Date, "2026-10-05"));
        Assert.Equal("last spring", OwnFieldValues.Display(OwnFieldKind.Date, "last spring"));
        Assert.Equal("tall", OwnFieldValues.Display(OwnFieldKind.Number, "tall"));
        Assert.Equal("12.5", OwnFieldValues.Display(OwnFieldKind.Number, "12.5"));
        Assert.True(OwnFieldValues.Fits(OwnFieldKind.Number, "12.5"));
        Assert.False(OwnFieldValues.Fits(OwnFieldKind.Number, "tall"));
        Assert.False(OwnFieldValues.Fits(OwnFieldKind.Date, "last spring"));
        Assert.True(OwnFieldValues.Fits(OwnFieldKind.Text, "anything"));
    }

    // The plant

    [Theory]
    [InlineData(PlantOrigin.GrownFromSeed, RaisedAs.WildCollected, RaisedAs.Seed)]
    [InlineData(PlantOrigin.Propagated, RaisedAs.WildCollected, RaisedAs.Cutting)]
    [InlineData(PlantOrigin.Purchased, RaisedAs.TissueCulture, RaisedAs.TissueCulture)]
    [InlineData(PlantOrigin.Gift, RaisedAs.NurseryGrown, RaisedAs.NurseryGrown)]
    [InlineData(PlantOrigin.Swap, RaisedAs.Cutting, RaisedAs.Cutting)]
    [InlineData(PlantOrigin.Unknown, RaisedAs.WildCollected, RaisedAs.WildCollected)]
    public void Raised_follows_the_origin_for_seed_and_cuttings_and_the_stored_value_otherwise(PlantOrigin origin, RaisedAs stored, RaisedAs expected)
    {
        var plant = new Plant { Nickname = "P", Origin = origin, RaisedAs = stored };

        Assert.Equal(expected, plant.Raised);
        Assert.Equal(stored, plant.RaisedAs);
    }

    [Fact]
    public void Raised_is_empty_when_nothing_is_said_for_a_bought_plant_and_still_set_for_seed()
    {
        Assert.Null(new Plant { Nickname = "P", Origin = PlantOrigin.Purchased }.Raised);
        Assert.Equal(RaisedAs.Seed, new Plant { Nickname = "P", Origin = PlantOrigin.GrownFromSeed }.Raised);
    }

    [Fact]
    public void Copy_and_Duplicate_take_how_it_was_raised_and_the_own_fields_into_new_dictionaries()
    {
        var id = Guid.NewGuid();
        var plant = new Plant { Nickname = "P", RaisedAs = RaisedAs.TissueCulture };
        plant.OwnFields[id] = "Clone 7";

        var copy = plant.Copy();
        var duplicate = plant.Duplicate();

        Assert.Equal(RaisedAs.TissueCulture, copy.RaisedAs);
        Assert.Equal(RaisedAs.TissueCulture, duplicate.RaisedAs);
        Assert.Equal("Clone 7", copy.OwnFields[id]);
        Assert.Equal("Clone 7", duplicate.OwnFields[id]);

        copy.OwnFields[id] = "changed";
        duplicate.OwnFields.Remove(id);
        Assert.Equal("Clone 7", plant.OwnFields[id]);
    }

    [Fact]
    public void An_older_plant_without_the_new_properties_reads_as_empty_and_an_unknown_value_reads_as_not_said()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var old = JsonSerializer.Deserialize<Plant>("""{"nickname":"P"}""", options)!;
        Assert.Null(old.RaisedAs);
        Assert.Empty(old.OwnFields);

        var later = JsonSerializer.Deserialize<Plant>("""{"nickname":"P","raisedAs":"Grafted"}""", options)!;
        Assert.Null(later.RaisedAs);
    }

    [Fact]
    public void The_values_of_own_fields_and_how_it_was_raised_survive_a_backup()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var field = new OwnField { Name = "Sun", Kind = OwnFieldKind.Choice, Choices = ["Yes", "No"], UpdatedAt = Now };
        var plant = new Plant { Nickname = "P", RaisedAs = RaisedAs.WildCollected, UpdatedAt = Now };
        plant.OwnFields[field.Id] = "Yes";

        var json = JsonSerializer.Serialize(new BackupData { OwnFields = [field], Plants = [plant] }, options);
        Assert.Contains("\"kind\":\"Choice\"", json);
        Assert.Contains("\"for\":\"Plants\"", json);
        Assert.Contains("\"raisedAs\":\"WildCollected\"", json);

        var back = JsonSerializer.Deserialize<BackupData>(json, options)!;

        var restored = Assert.Single(back.OwnFields);
        Assert.Equal((field.Id, "Sun", OwnFieldKind.Choice), (restored.Id, restored.Name, restored.Kind));
        Assert.Equal(["Yes", "No"], restored.Choices);
        Assert.Equal("Yes", back.Plants[0].OwnFields[field.Id]);
        Assert.Equal(RaisedAs.WildCollected, back.Plants[0].RaisedAs);
        Assert.Equal(1, BackupMerge.Merge<OwnField>([], back.OwnFields).Added);
        Assert.Equal(0, BackupMerge.Merge([field], back.OwnFields).Added);
    }

    [Fact]
    public void A_kind_or_subject_this_version_does_not_know_reads_as_the_first_value()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var field = JsonSerializer.Deserialize<OwnField>("""{"name":"X","kind":"Rating","for":"Pots"}""", options)!;

        Assert.Equal(OwnFieldKind.Text, field.Kind);
        Assert.Equal(OwnFieldSubject.Plants, field.For);
    }

    // The spreadsheet

    [Fact]
    public void The_export_has_a_column_for_how_it_was_raised_and_one_for_each_field_after_Added()
    {
        var field = new OwnField { Name = "Clone name" };
        var empty = new OwnField { Name = "Awards" };
        var plant = new Plant { Nickname = "Basil", Origin = PlantOrigin.Purchased, RaisedAs = RaisedAs.TissueCulture, CreatedAt = Now };
        plant.OwnFields[field.Id] = "Clone, 7";
        var seed = new Plant { Nickname = "Seedling", Origin = PlantOrigin.GrownFromSeed, CreatedAt = Now };

        var lines = CsvExport.Plants([plant, seed], Places.None, [field, empty]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.EndsWith(",Added,Clone name,Awards", lines[0]);
        Assert.Contains(",Purchased,TissueCulture,", lines[1]);
        Assert.EndsWith(",\"Clone, 7\",", lines[1]);
        Assert.Contains(",GrownFromSeed,Seed,", lines[2]);
        Assert.EndsWith(",,", lines[2]);
    }

    [Fact]
    public void An_export_with_field_columns_reads_back_and_the_extra_columns_go_to_About_it()
    {
        var field = new OwnField { Name = "Clone name" };
        var plant = new Plant { Nickname = "Basil", Origin = PlantOrigin.Purchased, RaisedAs = RaisedAs.WildCollected, CreatedAt = Now };
        plant.OwnFields[field.Id] = "Clone 7";

        var sheet = SheetReader.Read(CsvExport.Plants([plant], Places.None, [field]));
        var row = SpreadsheetImport.Preview(sheet, ImportFields.Guess(sheet.Headings), DateOrder.DayFirst,
            Dictionary, [], DateOnly.FromDateTime(Now.UtcDateTime)).Single();

        Assert.Equal(RaisedAs.WildCollected, row.Draft.RaisedAs);
        Assert.Contains("Clone name: Clone 7", row.Draft.Notes);
    }

    [Theory]
    [InlineData("nursery", RaisedAs.NurseryGrown)]
    [InlineData("Nursery grown", RaisedAs.NurseryGrown)]
    [InlineData("NurseryGrown", RaisedAs.NurseryGrown)]
    [InlineData("TC", RaisedAs.TissueCulture)]
    [InlineData("tissue culture", RaisedAs.TissueCulture)]
    [InlineData("Tissue", RaisedAs.TissueCulture)]
    [InlineData("tissueculture", RaisedAs.TissueCulture)]
    [InlineData("vævskultur", RaisedAs.TissueCulture)]
    [InlineData("Seed", RaisedAs.Seed)]
    [InlineData("from seed", RaisedAs.Seed)]
    [InlineData("seed grown", RaisedAs.Seed)]
    [InlineData("frø", RaisedAs.Seed)]
    [InlineData("cutting", RaisedAs.Cutting)]
    [InlineData("Rooted cutting", RaisedAs.Cutting)]
    [InlineData("division", RaisedAs.Cutting)]
    [InlineData("From a cutting or division", RaisedAs.Cutting)]
    [InlineData("stikling", RaisedAs.Cutting)]
    [InlineData("deling", RaisedAs.Cutting)]
    [InlineData("wild", RaisedAs.WildCollected)]
    [InlineData("Wild collected", RaisedAs.WildCollected)]
    [InlineData("WC", RaisedAs.WildCollected)]
    [InlineData("wildcollected", RaisedAs.WildCollected)]
    [InlineData("vildtindsamlet", RaisedAs.WildCollected)]
    public void Raised_words(string text, RaisedAs expected) => Assert.Equal(expected, ImportValues.ReadRaised(text));

    [Fact]
    public void Raised_text_that_is_not_known_is_kept_in_About_it_and_the_headings_are_guessed()
    {
        Assert.Null(ImportValues.ReadRaised("grafted"));

        var sheet = SheetReader.Read("Name,Raised as,Propagation type\nBasil,TC,grafted\nMint,wild,\n");
        var guess = ImportFields.Guess(sheet.Headings);
        Assert.Equal(ImportField.Raised, guess[1]);
        Assert.Equal(ImportField.AboutLine, guess[2]);

        var rows = SpreadsheetImport.Preview(sheet, guess, DateOrder.DayFirst, Dictionary, [], DateOnly.FromDateTime(Now.UtcDateTime));
        Assert.Equal(RaisedAs.TissueCulture, rows[0].Draft.RaisedAs);
        Assert.Equal(RaisedAs.WildCollected, rows[1].Draft.RaisedAs);

        var odd = SheetReader.Read("Name,Raised\nBasil,grafted\n");
        var row = SpreadsheetImport.Preview(odd, ImportFields.Guess(odd.Headings), DateOrder.DayFirst, Dictionary, [], DateOnly.FromDateTime(Now.UtcDateTime)).Single();
        Assert.Null(row.Draft.RaisedAs);
        Assert.Contains("Raised: grafted", row.Draft.Notes);
        Assert.Contains(row.Problems, m => m.Contains("Didn't know \"grafted\" as how it was raised, kept in About it"));
    }
}
