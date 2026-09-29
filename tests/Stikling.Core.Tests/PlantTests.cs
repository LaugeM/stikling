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

    [Fact]
    public void Validate_rejects_the_same_pot_inside_and_outside()
    {
        var pot = Guid.NewGuid();
        var plant = new Plant { Nickname = "Coleus", InnerPotId = pot, OuterPotId = pot };

        Assert.Contains("A plant can't have the same pot inside and outside.", plant.Validate(Today));
    }

    [Fact]
    public void Duplicate_copies_names_room_and_growing_setup()
    {
        var plant = new Plant
        {
            Genus = "Ocimum", Species = "basilicum", Cultivar = "Genovese",
            PlaceId = Guid.NewGuid(), Light = LightLevel.BrightIndirect, Origin = PlantOrigin.GrownFromSeed, ParentPlantId = Guid.NewGuid(),
            Source = "Garden centre",
            Medium = GrowingMedium.Leca, SoilMixId = Guid.NewGuid(), Tags = ["herbs"]
        };

        var copy = plant.Duplicate();

        Assert.NotEqual(plant.Id, copy.Id);
        Assert.Equal(plant.BotanicalName, copy.BotanicalName);
        Assert.Equal(plant.PlaceId, copy.PlaceId);
        Assert.Equal(LightLevel.BrightIndirect, copy.Light);
        Assert.Equal(plant.Origin, copy.Origin);
        Assert.Equal(plant.ParentPlantId, copy.ParentPlantId);
        Assert.Equal(plant.Source, copy.Source);
        Assert.Equal(plant.Medium, copy.Medium);
        Assert.Equal(plant.SoilMixId, copy.SoilMixId);
        Assert.Equal(["herbs"], copy.Tags);
        Assert.NotSame(plant.Tags, copy.Tags);
    }

    [Fact]
    public void Duplicate_leaves_what_belongs_to_the_one_plant()
    {
        var plant = new Plant
        {
            Nickname = "Kitchen basil", AcquiredOn = LooseDate.Of(Today), InnerPotId = Guid.NewGuid(), OuterPotId = Guid.NewGuid(),
            WaterInOuterPot = true, Notes = "Bolts early", Status = PlantStatus.GivenAway,
            QuarantinedSince = Today, DormantSince = Today, Attention = new Attention("Yellow leaves", Today),
            CoverPhotoId = Guid.NewGuid(), FromPropagationId = Guid.NewGuid(), Favourite = true
        };

        var copy = plant.Duplicate();

        Assert.Null(copy.Nickname);
        Assert.Null(copy.AcquiredOn);
        Assert.Null(copy.InnerPotId);
        Assert.Null(copy.OuterPotId);
        Assert.False(copy.WaterInOuterPot);
        Assert.Null(copy.Notes);
        Assert.Equal(PlantStatus.Active, copy.Status);
        Assert.Null(copy.QuarantinedSince);
        Assert.Null(copy.DormantSince);
        Assert.Null(copy.Attention);
        Assert.Null(copy.CoverPhotoId);
        Assert.Null(copy.FromPropagationId);
        Assert.False(copy.Favourite);
    }

    [Fact]
    public void Copy_keeps_favourite()
    {
        Assert.True(new Plant { Favourite = true }.Copy().Favourite);
    }

    [Fact]
    public void A_quarantine_shorter_than_a_day_is_refused()
    {
        var today = new DateOnly(2026, 9, 29);
        var plant = new Plant { Nickname = "A", QuarantinedSince = today, QuarantineDays = 0 };

        Assert.Contains("A quarantine has to last at least a day.", plant.Validate(today));
    }
}
