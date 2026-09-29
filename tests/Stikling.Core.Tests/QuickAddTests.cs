using Stikling.Core.Names;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class QuickAddTests
{
    private static readonly PlantDictionary Dictionary = new(new PlantNameData
    {
        Genera =
        [
            new GenusNames
            {
                Name = "Alocasia",
                Species = ["baginda", "zebrina"],
                Cultivars = [new("Polly")]
            },
            new GenusNames
            {
                Name = "Monstera",
                Species = ["adansonii", "deliciosa"],
                Cultivars = [new("Thai Constellation", "deliciosa")]
            }
        ]
    });

    private static IReadOnlyList<Models.Plant> Parse(string text) => QuickAdd.Parse(text, Dictionary);

    [Fact]
    public void A_genus_alone()
    {
        var plant = Assert.Single(Parse("Alocasia"));
        Assert.Equal("Alocasia", plant.Genus);
        Assert.Null(plant.Species);
        Assert.Null(plant.Notes);
        Assert.Null(plant.Nickname);
    }

    [Fact]
    public void Genus_and_species()
    {
        var plant = Assert.Single(Parse("Alocasia zebrina"));
        Assert.Equal(("Alocasia", "zebrina"), (plant.Genus, plant.Species));
    }

    [Theory]
    [InlineData("Monstera deliciosa 'Thai Constellation'")]
    [InlineData("Monstera deliciosa \"Thai Constellation\"")]
    [InlineData("Monstera deliciosa Thai Constellation")]
    [InlineData("Monstera 'Thai Constellation'")]
    public void A_cultivar_with_or_without_quotes(string text)
    {
        var plant = Assert.Single(Parse(text));
        Assert.Equal(("Monstera", "deliciosa", "Thai Constellation"), (plant.Genus, plant.Species, plant.Cultivar));
        Assert.Null(plant.Notes);
    }

    [Fact]
    public void The_rest_of_the_item_goes_into_the_notes()
    {
        var plant = Assert.Single(Parse("Alocasia zebrina from the swap"));
        Assert.Equal("zebrina", plant.Species);
        Assert.Equal("from the swap", plant.Notes);

        var second = Assert.Single(Parse("Monstera large leaves"));
        Assert.Null(second.Species);
        Assert.Equal("large leaves", second.Notes);
    }

    [Fact]
    public void An_unknown_name_becomes_the_nickname()
    {
        var plant = Assert.Single(Parse("Big kitchen basil"));
        Assert.Equal("Big kitchen basil", plant.Nickname);
        Assert.Null(plant.Genus);
        Assert.Null(plant.Notes);
    }

    [Fact]
    public void Commas_semicolons_and_new_lines_separate_plants()
    {
        var plants = Parse("Alocasia zebrina, Monstera deliciosa\nAlocasia baginda; Basil");
        Assert.Equal(4, plants.Count);
        Assert.Equal(["zebrina", "deliciosa", "baginda", null], plants.Select(p => p.Species));
        Assert.Equal("Basil", plants[3].Nickname);
    }

    [Fact]
    public void Blank_items_are_skipped()
    {
        Assert.Empty(Parse(""));
        Assert.Empty(Parse(" , \n\n ; "));
        Assert.Equal(2, Parse("\n Alocasia ,, \n Monstera \n").Count);
    }

    [Fact]
    public void Casing_follows_the_dictionary()
    {
        var plant = Assert.Single(Parse("monstera DELICIOSA thai constellation"));
        Assert.Equal(("Monstera", "deliciosa", "Thai Constellation"), (plant.Genus, plant.Species, plant.Cultivar));
    }

    [Fact]
    public void The_date_is_set_on_each_plant()
    {
        var day = Models.LooseDate.Of(new DateOnly(2026, 9, 29));
        Assert.All(QuickAdd.Parse("Alocasia, Basil", Dictionary, day), p => Assert.Equal(day, p.AcquiredOn));
    }
}
