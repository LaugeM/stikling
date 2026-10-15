using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>The words about toxicity people see. A plant nothing is known about never reads as safe.</summary>
public static class ToxicityText
{
    public static string Pets(PlantCare? care) => (care?.Cats, care?.Dogs) switch
    {
        (Toxicity.Toxic, Toxicity.Toxic) => "Toxic to cats and dogs",
        (Toxicity.NonToxic, Toxicity.NonToxic) => "Not toxic to cats and dogs",
        (Toxicity.Toxic, Toxicity.NonToxic) => "Toxic to cats, not to dogs",
        (Toxicity.NonToxic, Toxicity.Toxic) => "Toxic to dogs, not to cats",
        (Toxicity.Toxic, null) => "Toxic to cats",
        (Toxicity.NonToxic, null) => "Not toxic to cats",
        (null, Toxicity.Toxic) => "Toxic to dogs",
        (null, Toxicity.NonToxic) => "Not toxic to dogs",
        _ => "Nothing known about pets"
    };

    public static string? People(PlantCare? care) => care?.People switch
    {
        Toxicity.Toxic => "Toxic to people",
        Toxicity.NonToxic => "Not toxic to people",
        _ => null
    };
}
