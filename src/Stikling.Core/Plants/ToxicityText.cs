using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Plants;

/// <summary>The words about toxicity people see. A plant nothing is known about never reads as safe.</summary>
public static class ToxicityText
{
    public static string Pets(PlantCare? care) => (care?.Cats, care?.Dogs) switch
    {
        (Toxicity.Toxic, Toxicity.Toxic) => Strings.Plants.PetsBoth,
        (Toxicity.NonToxic, Toxicity.NonToxic) => Strings.Plants.PetsNeither,
        (Toxicity.Toxic, Toxicity.NonToxic) => Strings.Plants.PetsCatsOnly,
        (Toxicity.NonToxic, Toxicity.Toxic) => Strings.Plants.PetsDogsOnly,
        (Toxicity.Toxic, null) => Strings.Plants.PetsCats,
        (Toxicity.NonToxic, null) => Strings.Plants.PetsNotCats,
        (null, Toxicity.Toxic) => Strings.Plants.PetsDogs,
        (null, Toxicity.NonToxic) => Strings.Plants.PetsNotDogs,
        _ => Strings.Plants.PetsUnknown
    };

    public static string? People(PlantCare? care) => care?.People switch
    {
        Toxicity.Toxic => Strings.Plants.PeopleToxic,
        Toxicity.NonToxic => Strings.Plants.PeopleNotToxic,
        _ => null
    };
}
