using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantTableTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private static Plant Named(string nickname, Action<Plant>? set = null)
    {
        var plant = new Plant { Nickname = nickname };
        set?.Invoke(plant);
        return plant;
    }

    private static CareLog Log(Plant plant, CareKind kind, int daysAgo, Action<CareLog>? set = null)
    {
        var log = new CareLog { PlantId = plant.Id, Kind = kind, OccurredOn = Today.AddDays(-daysAgo) };
        set?.Invoke(log);
        return log;
    }

    private static List<PlantRow> Rows(IEnumerable<Plant> plants, params CareLog[] logs) =>
        PlantTable.Rows(plants, logs, Today, _ => null, _ => null);

    private static string[] Names(IEnumerable<PlantRow> rows) => rows.Select(r => r.Plant.DisplayName).ToArray();

    [Fact]
    public void Parse_gives_the_defaults_when_nothing_is_stored()
    {
        Assert.Equal(PlantTable.Defaults, PlantTable.Parse(null));
        Assert.Equal(PlantTable.Defaults, PlantTable.Parse(""));
        Assert.Equal(PlantTable.Defaults, PlantTable.Parse("Nonsense, 3"));
    }

    [Fact]
    public void Write_and_parse_round_trip_in_column_order()
    {
        var text = PlantTable.Write([PlantColumn.Price, PlantColumn.Name, PlantColumn.Room]);

        Assert.Equal("Name,Room,Price", text);
        Assert.Equal([PlantColumn.Name, PlantColumn.Room, PlantColumn.Price], PlantTable.Parse(text));
    }

    [Fact]
    public void Parse_drops_unknown_names_and_always_keeps_name()
    {
        Assert.Equal([PlantColumn.Name, PlantColumn.Fed], PlantTable.Parse("Fed, Colour"));
        Assert.Equal([PlantColumn.Name], PlantTable.Parse("Name"));
        Assert.Equal("Name,Room", PlantTable.Write([PlantColumn.Room]));
    }

    [Fact]
    public void Sort_is_stored_as_column_and_direction()
    {
        Assert.Equal("Watered desc", PlantTable.WriteSort(new(PlantColumn.Watered, true)));
        Assert.Equal(new PlantTableSort(PlantColumn.Watered, true), PlantTable.ParseSort("Watered desc"));
        Assert.Equal(new PlantTableSort(PlantColumn.Room), PlantTable.ParseSort("Room"));
        Assert.Equal(new PlantTableSort(), PlantTable.ParseSort("Colour desc"));
        Assert.Equal(new PlantTableSort(), PlantTable.ParseSort("Room up"));
        Assert.Equal(new PlantTableSort(), PlantTable.ParseSort(null));
    }

    [Fact]
    public void Tapping_a_heading_sorts_by_it_and_tapping_again_turns_it_round()
    {
        var byName = new PlantTableSort();

        var watered = PlantTable.Tap(byName, PlantColumn.Watered);
        Assert.Equal(new PlantTableSort(PlantColumn.Watered, true), watered);
        Assert.Equal(new PlantTableSort(PlantColumn.Watered, false), PlantTable.Tap(watered, PlantColumn.Watered));
        Assert.Equal(new PlantTableSort(PlantColumn.Room, false), PlantTable.Tap(watered, PlantColumn.Room));
        Assert.Equal(new PlantTableSort(PlantColumn.Name, true), PlantTable.Tap(byName, PlantColumn.Name));
    }

    [Fact]
    public void Sort_ascending_and_descending_puts_blanks_last_both_ways()
    {
        var fern = Named("Fern", p => p.Source = "Market");
        var aloe = Named("Aloe", p => p.Source = "Anna");
        var blank = Named("Basil");
        var rows = Rows([fern, blank, aloe]);

        Assert.Equal(["Aloe", "Fern", "Basil"], Names(PlantTable.Sort(rows, new(PlantColumn.Source))));
        Assert.Equal(["Fern", "Aloe", "Basil"], Names(PlantTable.Sort(rows, new(PlantColumn.Source, true))));
    }

    [Fact]
    public void Sort_by_date_ascending_is_oldest_first_and_ties_fall_back_on_the_name()
    {
        var a = Named("Ardisia", p => p.AcquiredOn = LooseDate.Of(2024, 6));
        var b = Named("Begonia", p => p.AcquiredOn = LooseDate.Of(2023));
        var c = Named("Calathea", p => p.AcquiredOn = LooseDate.Of(2024, 6));
        var rows = Rows([c, a, b]);

        Assert.Equal(["Begonia", "Ardisia", "Calathea"], Names(PlantTable.Sort(rows, new(PlantColumn.Got))));
        Assert.Equal(["Ardisia", "Calathea", "Begonia"], Names(PlantTable.Sort(rows, new(PlantColumn.Got, true))));
    }

    [Fact]
    public void Sort_by_price_is_numeric_and_by_text_ignores_case()
    {
        var cheap = Named("cheap", p => p.PricePaid = 9);
        var dear = Named("Dear", p => p.PricePaid = 100);

        Assert.Equal(["cheap", "Dear"], Names(PlantTable.Sort(Rows([dear, cheap]), new(PlantColumn.Price))));
        Assert.Equal(["cheap", "Dear"], Names(PlantTable.Sort(Rows([dear, cheap]), new(PlantColumn.Name))));
    }

    [Fact]
    public void Watered_takes_topped_up_when_it_is_newer()
    {
        var plant = Named("Leca fern");
        var row = Rows([plant], Log(plant, CareKind.Watered, 9), Log(plant, CareKind.ToppedUp, 2))[0];

        Assert.Equal(Today.AddDays(-2), row.Watered?.On);
        Assert.Equal(2, row.Watered?.DaysAgo);

        var older = Rows([plant], Log(plant, CareKind.Watered, 1), Log(plant, CareKind.ToppedUp, 5))[0];
        Assert.Equal(Today.AddDays(-1), older.Watered?.On);
    }

    [Fact]
    public void Rows_ignore_deleted_logs_and_read_fed_repotted_and_moisture()
    {
        var plant = Named("Monstera");
        var other = Named("Other");
        var row = Rows([plant, other],
            Log(plant, CareKind.Watered, 0, l => l.DeletedAt = DateTimeOffset.UnixEpoch),
            Log(plant, CareKind.Watered, 6),
            Log(plant, CareKind.Fertilised, 20),
            Log(plant, CareKind.Repotted, 100),
            Log(plant, CareKind.MoistureReading, 8, l => l.Moisture = 4),
            Log(plant, CareKind.MoistureReading, 3, l => l.Moisture = 7),
            Log(other, CareKind.Fertilised, 1))[0];

        Assert.Equal(6, row.Watered?.DaysAgo);
        Assert.Equal(20, row.Fed?.DaysAgo);
        Assert.Equal(100, row.Repotted?.DaysAgo);
        Assert.Equal(7, row.Moisture?.Value);
    }

    [Fact]
    public void Sort_by_watered_newest_first_when_descending_with_never_watered_last()
    {
        var recent = Named("Recent");
        var old = Named("Old");
        var never = Named("Never");
        var rows = Rows([never, old, recent], Log(old, CareKind.Watered, 10), Log(recent, CareKind.Watered, 1));

        Assert.Equal(["Recent", "Old", "Never"], Names(PlantTable.Sort(rows, new(PlantColumn.Watered, true))));
        Assert.Equal(["Old", "Recent", "Never"], Names(PlantTable.Sort(rows, new(PlantColumn.Watered))));
    }

    [Fact]
    public void Name_shows_the_display_name_unless_the_cultivar_column_is_on()
    {
        var thai = new Plant { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation" };
        var onlyCultivar = new Plant { Cultivar = "Thai Constellation" };
        var nicknamed = Named("Big one", p => { p.Genus = "Monstera"; p.Cultivar = "Albo"; });

        Assert.Equal("Monstera deliciosa 'Thai Constellation'", PlantTable.NameFor(thai, false));
        Assert.Equal("Monstera deliciosa", PlantTable.NameFor(thai, true));
        Assert.Equal("Thai Constellation", PlantTable.NameFor(onlyCultivar, true));
        Assert.Equal("Big one", PlantTable.NameFor(nicknamed, true));
        Assert.Equal("Unnamed plant", PlantTable.NameFor(new Plant(), true));
    }
}
