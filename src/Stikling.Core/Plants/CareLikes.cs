using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>How the light where a plant stands compares with the light it likes.</summary>
public enum LightFit
{
    Fits,
    Darker,
    Brighter
}

/// <summary>One of the needs a plant can say it has.</summary>
public enum LikesField
{
    Light,
    Water,
    Humidity,
    Season,
    Rest
}

/// <summary>
/// What a plant likes: its light, water, humidity and whether it rests. All optional, and none of
/// it is written to the plant's history.
/// </summary>
public static class CareLikes
{
    /// <summary>
    /// Whether the light where it stands is darker or brighter than it likes. Null when either is not set.
    /// </summary>
    public static LightFit? Fit(Plant plant)
    {
        if (plant.Light is not { } stands || plant.LikesLight is not { } likes)
            return null;
        return stands < likes ? LightFit.Darker : stands > likes ? LightFit.Brighter : LightFit.Fits;
    }

    public static bool HasAny(Plant plant) =>
        plant.LikesLight is not null || plant.LikesWater is not null || plant.LikesHumidity is not null
        || plant.GrowingSeason is not null || plant.RestHow is not null;

    /// <summary>The needs that differ between two versions of a plant.</summary>
    public static IReadOnlyList<LikesField> Changed(Plant before, Plant after)
    {
        var changed = new List<LikesField>();
        if (before.LikesLight != after.LikesLight) changed.Add(LikesField.Light);
        if (before.LikesWater != after.LikesWater) changed.Add(LikesField.Water);
        if (before.LikesHumidity != after.LikesHumidity) changed.Add(LikesField.Humidity);
        if (before.GrowingSeason != after.GrowingSeason) changed.Add(LikesField.Season);
        if (before.RestHow != after.RestHow) changed.Add(LikesField.Rest);
        return changed;
    }

    /// <summary>Sets the given needs on the other plant to what the first one has.</summary>
    public static void Copy(Plant from, Plant to, IEnumerable<LikesField> fields)
    {
        foreach (var field in fields)
        {
            switch (field)
            {
                case LikesField.Light: to.LikesLight = from.LikesLight; break;
                case LikesField.Water: to.LikesWater = from.LikesWater; break;
                case LikesField.Humidity: to.LikesHumidity = from.LikesHumidity; break;
                case LikesField.Season: to.GrowingSeason = from.GrowingSeason; break;
                case LikesField.Rest: to.RestHow = from.RestHow; break;
            }
        }
    }

    /// <summary>
    /// The other plants still here with the same genus and species, ignoring case and the cultivar.
    /// Empty when this plant has no genus or no species.
    /// </summary>
    public static IReadOnlyList<Plant> SameSpecies(Plant plant, IEnumerable<Plant> all)
    {
        var genus = plant.Genus?.Trim();
        var species = plant.KnownSpecies?.Trim();
        if (string.IsNullOrEmpty(genus) || string.IsNullOrEmpty(species))
            return [];

        return
        [
            .. all.Where(p => p.Id != plant.Id && !p.IsDeleted && p.InCollection
                && string.Equals(p.Genus?.Trim(), genus, StringComparison.CurrentCultureIgnoreCase)
                && string.Equals(p.KnownSpecies?.Trim(), species, StringComparison.CurrentCultureIgnoreCase))
        ];
    }

    /// <summary>A way of resting only means something for a plant that rests in summer or winter.</summary>
    public static void Normalize(Plant plant)
    {
        if (plant.GrowingSeason is not (GrowingSeason.Summer or GrowingSeason.Winter))
            plant.RestHow = null;
    }

    /// <summary>
    /// The plants of the same species that copying these fields would really change, after
    /// <see cref="Normalize"/>. A way of resting copied to a plant that doesn't rest is left out.
    /// </summary>
    public static IReadOnlyList<Plant> WouldChange(Plant from, IReadOnlyList<LikesField> fields, IEnumerable<Plant> sameSpecies) =>
        sameSpecies.Where(other =>
        {
            var copy = other.Copy();
            Copy(from, copy, fields);
            Normalize(copy);
            return Changed(other, copy).Count > 0;
        }).ToList();
}
