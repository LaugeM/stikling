using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>What the care file offers a plant: only the fields the plant has nothing in.</summary>
public static class CareSuggestion
{
    /// <summary>The fields Use would set. Empty means there is nothing to offer.</summary>
    public static IReadOnlyList<LikesField> Fills(Plant plant, PlantCare care)
    {
        var fills = new List<LikesField>();
        if (care.Light is not null && plant.LikesLight is null) fills.Add(LikesField.Light);
        if (care.Water is not null && plant.LikesWater is null) fills.Add(LikesField.Water);
        if (care.Humidity is not null && plant.LikesHumidity is null) fills.Add(LikesField.Humidity);
        if (care.Season is not null && plant.GrowingSeason is null) fills.Add(LikesField.Season);

        // A way of resting only goes with the season it was written for
        var season = plant.GrowingSeason ?? care.Season;
        if (care.Rest is not null && plant.RestHow is null && season == care.Season
            && season is GrowingSeason.Summer or GrowingSeason.Winter)
            fills.Add(LikesField.Rest);
        return fills;
    }

    /// <summary>Sets exactly the fields <see cref="Fills"/> lists and leaves the rest as it was.</summary>
    public static void Apply(Plant plant, PlantCare care)
    {
        foreach (var field in Fills(plant, care))
        {
            switch (field)
            {
                case LikesField.Light: plant.LikesLight = care.Light; break;
                case LikesField.Water: plant.LikesWater = care.Water; break;
                case LikesField.Humidity: plant.LikesHumidity = care.Humidity; break;
                case LikesField.Season: plant.GrowingSeason = care.Season; break;
                case LikesField.Rest: plant.RestHow = care.Rest; break;
            }
        }
        CareLikes.Normalize(plant);
    }
}
