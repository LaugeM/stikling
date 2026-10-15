using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class CareSuggestionTests
{
    private static readonly PlantCare Everything = new()
    {
        Name = "Test",
        Light = LightLevel.BrightIndirect,
        Water = WaterNeed.DryOut,
        Humidity = HumidityNeed.High,
        Season = GrowingSeason.Summer,
        Rest = RestKind.Drier
    };

    [Fact]
    public void Everything_empty_is_all_offered_in_order()
    {
        Assert.Equal(
            [LikesField.Light, LikesField.Water, LikesField.Humidity, LikesField.Season, LikesField.Rest],
            CareSuggestion.Fills(new Plant(), Everything));
    }

    [Fact]
    public void Only_the_empty_fields_are_offered()
    {
        var plant = new Plant { LikesLight = LightLevel.Low, LikesHumidity = HumidityNeed.Low };

        Assert.Equal([LikesField.Water, LikesField.Season, LikesField.Rest], CareSuggestion.Fills(plant, Everything));
    }

    [Fact]
    public void Nothing_is_offered_when_everything_is_set()
    {
        var plant = new Plant
        {
            LikesLight = LightLevel.Low, LikesWater = WaterNeed.EvenlyMoist, LikesHumidity = HumidityNeed.Low,
            GrowingSeason = GrowingSeason.Summer, RestHow = RestKind.Cooler
        };

        Assert.Empty(CareSuggestion.Fills(plant, Everything));
    }

    [Fact]
    public void A_value_the_care_does_not_have_is_not_offered()
    {
        Assert.Empty(CareSuggestion.Fills(new Plant(), new PlantCare { Name = "Test" }));
    }

    [Fact]
    public void Rest_is_offered_when_the_plant_already_has_the_matching_season()
    {
        var plant = new Plant { GrowingSeason = GrowingSeason.Summer };

        Assert.Equal([LikesField.Light, LikesField.Water, LikesField.Humidity, LikesField.Rest], CareSuggestion.Fills(plant, Everything));
    }

    [Fact]
    public void Rest_is_not_offered_when_the_plants_season_differs()
    {
        var plant = new Plant { GrowingSeason = GrowingSeason.Winter };

        Assert.DoesNotContain(LikesField.Rest, CareSuggestion.Fills(plant, Everything));
        Assert.DoesNotContain(LikesField.Season, CareSuggestion.Fills(plant, Everything));
    }

    [Fact]
    public void Rest_is_not_offered_when_the_care_has_no_season()
    {
        var care = new PlantCare { Name = "Test", Rest = RestKind.Drier, Light = LightLevel.Low };

        Assert.Equal([LikesField.Light], CareSuggestion.Fills(new Plant { GrowingSeason = GrowingSeason.Summer }, care));
    }

    [Fact]
    public void Apply_fills_the_empty_fields_and_leaves_the_rest()
    {
        var plant = new Plant { LikesLight = LightLevel.Low, LikesWater = null };

        CareSuggestion.Apply(plant, Everything);

        Assert.Equal(LightLevel.Low, plant.LikesLight);
        Assert.Equal(WaterNeed.DryOut, plant.LikesWater);
        Assert.Equal(HumidityNeed.High, plant.LikesHumidity);
        Assert.Equal(GrowingSeason.Summer, plant.GrowingSeason);
        Assert.Equal(RestKind.Drier, plant.RestHow);
    }

    [Fact]
    public void Apply_leaves_a_different_season_and_its_rest_alone()
    {
        var plant = new Plant { GrowingSeason = GrowingSeason.NoRest };

        CareSuggestion.Apply(plant, Everything);

        Assert.Equal(GrowingSeason.NoRest, plant.GrowingSeason);
        Assert.Null(plant.RestHow);
    }
}
