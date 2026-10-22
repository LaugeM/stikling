using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantGroupsTests
{
    [Fact]
    public void Plants_next_to_each_other_with_the_same_key_form_a_group()
    {
        Plant a = new() { Genus = "Hoya" };
        Plant b = new() { Genus = "hoya" };
        Plant c = new() { Genus = "Ficus" };

        var groups = PlantGroups.Split([a, b, c], p => p.Genus);

        Assert.Equal(2, groups.Count);
        Assert.Equal("Hoya", groups[0].Name);
        Assert.Equal([a, b], groups[0].Plants);
        Assert.Equal([c], groups[1].Plants);
    }

    [Fact]
    public void A_key_that_comes_back_later_starts_a_new_group()
    {
        var groups = PlantGroups.Split([new Plant { Genus = "A" }, new Plant { Genus = "B" }, new Plant { Genus = "A" }], p => p.Genus);

        Assert.Equal(3, groups.Count);
    }

    [Fact]
    public void Plants_with_no_key_are_a_group_of_their_own()
    {
        var groups = PlantGroups.Split([new Plant { Genus = "Hoya" }, new Plant { Nickname = "X" }, new Plant { Nickname = "Y" }], p => p.Genus);

        Assert.Null(groups[1].Name);
        Assert.Equal(2, groups[1].Plants.Count);
    }

    [Fact]
    public void A_group_counts_how_many_plants_each_pot_holds()
    {
        var groups = PlantGroups.Split([new Plant { Genus = "Hoya", Count = 3 }, new Plant { Genus = "Hoya" }], p => p.Genus);

        Assert.Equal(4, groups[0].Count);
    }

    [Fact]
    public void Nothing_makes_no_groups()
    {
        Assert.Empty(PlantGroups.Split([], p => p.Genus));
    }

    [Theory]
    [InlineData("Monstera", 'M')]
    [InlineData("  zebra", 'Z')]
    [InlineData("Écheveria", 'E')]
    [InlineData("Ålborg", 'A')]
    [InlineData("1st cutting", '#')]
    [InlineData("Øresund", '#')]
    [InlineData("'Thai'", '#')]
    public void A_name_goes_under_its_first_letter_without_accents(string name, char letter)
    {
        Assert.Equal(letter, PlantGroups.LetterOf(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void No_name_has_no_letter(string? name)
    {
        Assert.Null(PlantGroups.LetterOf(name));
    }
}
