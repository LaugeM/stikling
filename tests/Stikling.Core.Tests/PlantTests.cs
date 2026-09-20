using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class PlantTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

    [Theory]
    [InlineData("monstera", "Deliciosa", "Thai Constellation", "Monstera deliciosa 'Thai Constellation'")]
    [InlineData("ALOCASIA", null, null, "Alocasia")]
    [InlineData(" ficus ", " elastica ", null, "Ficus elastica")]
    [InlineData(null, null, "'Dark Form'", "'Dark Form'")]
    public void BotanicalName_is_formatted(string? genus, string? species, string? cultivar, string expected)
    {
        var plant = new Plant { Genus = genus, Species = species, Cultivar = cultivar };

        Assert.Equal(expected, plant.BotanicalName);
    }

    [Fact]
    public void BotanicalName_is_null_without_any_name_parts()
    {
        Assert.Null(new Plant().BotanicalName);
    }

    [Fact]
    public void DisplayName_prefers_nickname()
    {
        var plant = new Plant { Nickname = " Big Monstera ", Genus = "Monstera", Species = "deliciosa" };

        Assert.Equal("Big Monstera", plant.DisplayName);
    }

    [Fact]
    public void DisplayName_falls_back_to_botanical_name_then_placeholder()
    {
        Assert.Equal("Pachira aquatica", new Plant { Genus = "Pachira", Species = "aquatica" }.DisplayName);
        Assert.Equal("Unnamed plant", new Plant().DisplayName);
    }

    [Fact]
    public void Validate_requires_nickname_or_genus()
    {
        Assert.Contains("Give the plant a nickname or a genus.", new Plant { Species = "deliciosa" }.Validate(Today));
        Assert.Empty(new Plant { Nickname = "Basil" }.Validate(Today));
        Assert.Empty(new Plant { Genus = "Ocimum" }.Validate(Today));
    }

    [Fact]
    public void Validate_rejects_plant_as_its_own_parent()
    {
        var plant = new Plant { Nickname = "Coleus" };
        plant.ParentPlantId = plant.Id;

        Assert.Contains("A plant can't be its own parent.", plant.Validate(Today));
    }

    [Fact]
    public void New_plants_get_unique_ids_and_sensible_defaults()
    {
        var a = new Plant();
        var b = new Plant();

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(PlantStatus.Active, a.Status);
        Assert.Equal(GrowingMedium.Soil, a.Medium);
        Assert.False(a.IsDeleted);
    }

    [Fact]
    public void Validate_rejects_a_date_in_the_future()
    {
        var plant = new Plant { Nickname = "Coleus", AcquiredOn = LooseDate.Of(Today.AddDays(1)) };

        Assert.Contains("The date you got it can't be in the future.", plant.Validate(Today));
    }

    [Fact]
    public void This_year_is_fine_even_before_the_year_is_over()
    {
        var plant = new Plant { Nickname = "Coleus", AcquiredOn = LooseDate.Of(Today.Year) };

        Assert.Empty(plant.Validate(Today));
    }
}
