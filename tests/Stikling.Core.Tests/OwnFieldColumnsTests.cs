using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

// Own fields and How it was raised as table columns, filter picks and Set a field in Select mode
public class OwnFieldColumnsTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static OwnField Field(string name, OwnFieldKind kind, params string[] choices) =>
        new() { Name = name, Kind = kind, Choices = [.. choices] };

    private static Plant Plant(string name, Action<Plant>? set = null)
    {
        var plant = new Plant { Nickname = name, CreatedAt = Now };
        set?.Invoke(plant);
        return plant;
    }

    private static Plant With(string name, OwnField field, string? value, Action<Plant>? set = null) =>
        Plant(name, p =>
        {
            if (value is not null)
                p.OwnFields[field.Id] = value;
            set?.Invoke(p);
        });

    private static string[] SortedBy(OwnField field, bool descending, params Plant[] plants)
    {
        var rows = PlantTable.Rows(plants, [], Today, _ => null, _ => null);
        return [.. PlantTable.Sort(rows, new PlantTableSort(TableColumn.Field(field.Id), descending), fields: [field]).Select(r => r.Plant.DisplayName)];
    }

    // Columns

    [Fact]
    public void Field_columns_are_written_as_field_and_the_id_after_the_built_in_ones()
    {
        var clone = Field("Clone name", OwnFieldKind.Text);
        var award = Field("Awards", OwnFieldKind.Text);

        var text = PlantTable.Write([TableColumn.Field(clone.Id), PlantColumn.Room, TableColumn.Field(award.Id)]);

        Assert.Equal($"Name,Room,field:{clone.Id:D},field:{award.Id:D}", text);
        // On the way back the fields come after the built-in ones, by name
        Assert.Equal(
            [(TableColumn)PlantColumn.Name, PlantColumn.Room, TableColumn.Field(award.Id), TableColumn.Field(clone.Id)],
            PlantTable.Parse(text, [clone, award]));
    }

    [Fact]
    public void A_field_that_is_not_live_is_dropped_and_a_field_on_its_own_still_counts()
    {
        var clone = Field("Clone name", OwnFieldKind.Text);
        var gone = Field("Gone", OwnFieldKind.Text);
        gone.DeletedAt = Now;
        var stored = $"Name,field:{clone.Id:D},field:{gone.Id:D},field:{Guid.NewGuid():D},field:nonsense";

        Assert.Equal([(TableColumn)PlantColumn.Name, TableColumn.Field(clone.Id)], PlantTable.Parse(stored, [clone, gone]));
        // Only a removed field stored: nothing known gives the defaults
        Assert.Equal(PlantTable.Defaults, PlantTable.Parse($"field:{gone.Id:D}", [clone]));
        Assert.Equal([(TableColumn)PlantColumn.Name], PlantTable.Parse("Name", [clone]));
        Assert.Equal([(TableColumn)PlantColumn.Name, TableColumn.Field(clone.Id)], PlantTable.Parse($"field:{clone.Id:D}", [clone]));
    }

    [Fact]
    public void The_sort_on_a_field_is_stored_and_read_and_a_removed_field_reads_as_name()
    {
        var clone = Field("Clone name", OwnFieldKind.Text);
        var sort = new PlantTableSort(TableColumn.Field(clone.Id), true);

        var text = PlantTable.WriteSort(sort);

        Assert.Equal($"field:{clone.Id:D} desc", text);
        Assert.Equal(sort, PlantTable.ParseSort(text, [clone]));
        Assert.Equal(new PlantTableSort(TableColumn.Field(clone.Id)), PlantTable.ParseSort($"field:{clone.Id:D}", [clone]));
        Assert.Equal(new PlantTableSort(), PlantTable.ParseSort(text, []));
        Assert.Equal(new PlantTableSort(), PlantTable.ParseSort($"field:{clone.Id:D} up", [clone]));
    }

    [Fact]
    public void Header_of_a_field_column_is_the_field_name()
    {
        var clone = Field("Clone name", OwnFieldKind.Text);

        Assert.Equal("Clone name", PlantTable.Header(TableColumn.Field(clone.Id), [clone]));
        Assert.Equal("Raised", PlantTable.Header(PlantColumn.Raised, [clone]));
    }

    [Fact]
    public void Field_columns_start_ascending_and_the_raised_column_is_right_after_origin()
    {
        var number = Field("Size", OwnFieldKind.Number);
        var plants = new[] { With("A", number, "1") };
        var rows = PlantTable.Rows(plants, [], Today, _ => null, _ => null);

        var tapped = PlantTable.Tap(new PlantTableSort(), TableColumn.Field(number.Id));

        Assert.False(tapped.Descending);
        Assert.False(PlantTable.StartsDescending(TableColumn.Field(number.Id)));
        Assert.True(PlantTable.StartsDescending(PlantColumn.Got));
        Assert.Equal(PlantColumn.Raised, Enum.GetValues<PlantColumn>()[Array.IndexOf(Enum.GetValues<PlantColumn>(), PlantColumn.Origin) + 1]);
        Assert.Single(rows);
    }

    // Sorting

    [Fact]
    public void Sorting_by_a_number_field_reads_the_number_and_a_value_that_does_not_fit_goes_last()
    {
        var size = Field("Size", OwnFieldKind.Number);
        var plants = new[]
        {
            With("Ten", size, "10"), With("Two", size, "2"), With("Odd", size, "big"),
            With("Blank", size, null), With("Half", size, "1.5")
        };

        Assert.Equal(["Half", "Two", "Ten", "Blank", "Odd"], SortedBy(size, false, plants));
        Assert.Equal(["Ten", "Two", "Half", "Blank", "Odd"], SortedBy(size, true, plants));
    }

    [Fact]
    public void Sorting_by_a_date_field_reads_the_date()
    {
        var bloomed = Field("Bloomed", OwnFieldKind.Date);
        var plants = new[]
        {
            With("Late", bloomed, "2026-05-01"), With("Early", bloomed, "2025-12-24"),
            With("None", bloomed, null), With("Odd", bloomed, "May")
        };

        Assert.Equal(["Early", "Late", "None", "Odd"], SortedBy(bloomed, false, plants));
        Assert.Equal(["Late", "Early", "None", "Odd"], SortedBy(bloomed, true, plants));
    }

    [Fact]
    public void Sorting_by_a_text_field_goes_a_to_z_ignoring_case()
    {
        var clone = Field("Clone name", OwnFieldKind.Text);
        var plants = new[] { With("One", clone, "pink"), With("Two", clone, "Blue"), With("Three", clone, null) };

        Assert.Equal(["Two", "One", "Three"], SortedBy(clone, false, plants));
    }

    [Fact]
    public void Sorting_by_a_choice_field_follows_the_list_with_unlisted_values_after_it_and_blanks_last()
    {
        var sun = Field("Sun", OwnFieldKind.Choice, "Shade", "Part", "Full");
        var plants = new[]
        {
            With("Full", sun, "Full"), With("Shade", sun, "shade"), With("Blank", sun, null),
            With("Zed", sun, "Zebra"), With("Ant", sun, "Ant"), With("Part", sun, "Part")
        };

        Assert.Equal(["Shade", "Part", "Full", "Ant", "Zed", "Blank"], SortedBy(sun, false, plants));
        Assert.Equal(["Zed", "Ant", "Full", "Part", "Shade", "Blank"], SortedBy(sun, true, plants));
    }

    [Fact]
    public void The_raised_column_sorts_by_its_label_with_blanks_last_and_follows_the_origin()
    {
        var plants = new[]
        {
            Plant("Seedling", p => p.Origin = PlantOrigin.GrownFromSeed),
            Plant("Tissue", p => { p.Origin = PlantOrigin.Purchased; p.RaisedAs = RaisedAs.TissueCulture; }),
            Plant("Unsaid", p => p.Origin = PlantOrigin.Purchased),
            Plant("Wild", p => { p.Origin = PlantOrigin.Gift; p.RaisedAs = RaisedAs.WildCollected; })
        };
        var rows = PlantTable.Rows(plants, [], Today, _ => null, _ => null);

        var sorted = PlantTable.Sort(rows, new PlantTableSort(PlantColumn.Raised), labelOf: e => e is RaisedAs r ? r.ToString() : e.ToString());

        Assert.Equal(["Seedling", "Tissue", "Wild", "Unsaid"], sorted.Select(r => r.Plant.DisplayName));
    }

    // Filter picks

    private static IReadOnlyList<Plant> Pick(IEnumerable<Plant> plants, params FieldPick[] picks) =>
        [.. new PlantFilter(Picks: picks).Apply(plants)];

    [Fact]
    public void A_pick_on_an_own_field_matches_the_value_trimmed_and_ignoring_case()
    {
        var sun = Field("Sun", OwnFieldKind.Choice, "Full", "Part");
        var full = With("Full", sun, " full ");
        var part = With("Part", sun, "Part");
        var none = With("None", sun, null);

        Assert.Equal([full], Pick([full, part, none], new FieldPick(sun.Id, "Full")));
    }

    [Fact]
    public void Not_set_matches_a_plant_with_no_value_or_a_blank_one()
    {
        var sun = Field("Sun", OwnFieldKind.Choice, "Full");
        var full = With("Full", sun, "Full");
        var none = With("None", sun, null);
        var blank = With("Blank", sun, "  ");

        Assert.Equal(["Blank", "None"], Pick([full, none, blank], new FieldPick(sun.Id, null)).Select(p => p.DisplayName));
    }

    [Fact]
    public void A_raised_pick_matches_the_effective_value()
    {
        var seed = Plant("Seed", p => { p.Origin = PlantOrigin.GrownFromSeed; p.RaisedAs = RaisedAs.WildCollected; });
        var tissue = Plant("Tissue", p => { p.Origin = PlantOrigin.Purchased; p.RaisedAs = RaisedAs.TissueCulture; });
        var unsaid = Plant("Unsaid", p => p.Origin = PlantOrigin.Purchased);

        Assert.Equal([seed], Pick([seed, tissue, unsaid], new FieldPick(null, nameof(RaisedAs.Seed))));
        Assert.Equal([tissue], Pick([seed, tissue, unsaid], new FieldPick(null, nameof(RaisedAs.TissueCulture))));
        Assert.Equal([unsaid], Pick([seed, tissue, unsaid], new FieldPick(null, null)));
    }

    [Fact]
    public void Two_picks_together_narrow_the_list()
    {
        var sun = Field("Sun", OwnFieldKind.Choice, "Full", "Part");
        var both = With("Both", sun, "Full", p => { p.Origin = PlantOrigin.Purchased; p.RaisedAs = RaisedAs.Cutting; });
        var onlySun = With("OnlySun", sun, "Full", p => p.Origin = PlantOrigin.Purchased);
        var onlyRaised = With("OnlyRaised", sun, "Part", p => { p.Origin = PlantOrigin.Purchased; p.RaisedAs = RaisedAs.Cutting; });

        var found = Pick([both, onlySun, onlyRaised], new FieldPick(sun.Id, "Full"), new FieldPick(null, nameof(RaisedAs.Cutting)));

        Assert.Equal([both], found);
    }

    // Set How it was raised on several plants

    [Fact]
    public async Task SetRaisedAsync_skips_plants_grown_from_seed_or_propagated_and_the_ones_already_so()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var bought = Plant("Bought", p => p.Origin = PlantOrigin.Purchased);
        var already = Plant("Already", p => { p.Origin = PlantOrigin.Gift; p.RaisedAs = RaisedAs.TissueCulture; });
        var seed = Plant("Seed", p => p.Origin = PlantOrigin.GrownFromSeed);
        var cutting = Plant("Cutting", p => p.Origin = PlantOrigin.Propagated);
        foreach (var plant in new[] { bought, already, seed, cutting })
            plants.Plants[plant.Id] = plant;

        var changed = await service.SetRaisedAsync([bought, already, seed, cutting], RaisedAs.TissueCulture);

        Assert.Equal(1, changed);
        Assert.Equal(RaisedAs.TissueCulture, plants.Plants[bought.Id].RaisedAs);
        Assert.Null(plants.Plants[seed.Id].RaisedAs);
        Assert.Null(plants.Plants[cutting.Id].RaisedAs);
        Assert.Equal(RaisedAs.Seed, plants.Plants[seed.Id].Raised);

        Assert.Equal(2, await service.SetRaisedAsync([bought, already, seed], null));
        Assert.Null(plants.Plants[bought.Id].RaisedAs);
        Assert.Null(plants.Plants[already.Id].RaisedAs);
    }
}
