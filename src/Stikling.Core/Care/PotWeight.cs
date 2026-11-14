using Stikling.Core.Models;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Care;

/// <summary>
/// The words about what water a plant gets and what its pot weighs. A weight is a way to check a pot by
/// lifting it, so none of these lines says when to water.
/// </summary>
public static class PotWeight
{
    /// <summary>"Rain water", the name in a list.</summary>
    public static string Name(WaterType type) => type switch
    {
        WaterType.Demineralised => Strings.Weighing.WaterTypeDemineralised,
        WaterType.Rain => Strings.Weighing.WaterTypeRain,
        _ => Strings.Weighing.WaterTypeTap
    };

    /// <summary>"Gets rain water" for a plant's page. Null when it isn't said.</summary>
    public static string? Gets(WaterType? type) => type switch
    {
        null => null,
        WaterType.Demineralised => Strings.Weighing.WaterGetsDemineralised,
        WaterType.Rain => Strings.Weighing.WaterGetsRain,
        _ => Strings.Weighing.WaterGetsTap
    };

    /// <summary>A short note for the Water plants screen. Only water other than tap water is worth one.</summary>
    public static string? Note(WaterType? type) => type is WaterType.Demineralised or WaterType.Rain ? Name(type.Value) : null;

    /// <summary>"Use rain water" for the plant-sitter sheet. Tap water and not said give nothing.</summary>
    public static string? SitterLine(WaterType? type) => type switch
    {
        WaterType.Demineralised => Strings.Weighing.SitterUseDemineralised,
        WaterType.Rain => Strings.Weighing.SitterUseRain,
        _ => null
    };

    /// <summary>"Dry 840 g · just watered 1,320 g", or the part that is set. Null when neither is.</summary>
    public static string? Line(Plant plant) => (plant.DryWeightGrams, plant.WateredWeightGrams) switch
    {
        ({ } dry, { } wet) => F(Strings.Weighing.WeightBoth, Weights.Grams(dry), Weights.Grams(wet)),
        ({ } dry, null) => F(Strings.Weighing.WeightDry, Weights.Grams(dry)),
        (null, { } wet) => F(Strings.Weighing.WeightWatered, Weights.Grams(wet)),
        _ => null
    };

    /// <summary>"Dry at 840 g" for the Water plants screen. Null without a dry weight.</summary>
    public static string? DryAt(Plant plant) =>
        plant.DryWeightGrams is { } dry ? F(Strings.Weighing.WeightDryAt, Weights.Grams(dry)) : null;

    /// <summary>"To check by lifting: about 840 g when dry, 1,320 g just watered." for the sitter sheet. Null when not weighed.</summary>
    public static string? SitterWeight(Plant plant) => (plant.DryWeightGrams, plant.WateredWeightGrams) switch
    {
        ({ } dry, { } wet) => F(Strings.Weighing.SitterWeightBoth, Weights.Grams(dry), Weights.Grams(wet)),
        ({ } dry, null) => F(Strings.Weighing.SitterWeightDry, Weights.Grams(dry)),
        (null, { } wet) => F(Strings.Weighing.SitterWeightWatered, Weights.Grams(wet)),
        _ => null
    };

    /// <summary>The plant was repotted on a day after it was weighed, so the weights may no longer fit the pot.</summary>
    public static bool WeighedBeforeRepot(Plant plant, IEnumerable<CareLog> logs) =>
        plant.WeighedOn is { } weighed
        && logs.Any(l => !l.IsDeleted && l.PlantId == plant.Id && l.Kind == CareKind.Repotted && l.OccurredOn > weighed);
}
