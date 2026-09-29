using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>What a kind of pot says about what usually grows in it.</summary>
public static class PotKinds
{
    /// <summary>
    /// The medium a pot suggests, or null when it says nothing. A self-watering pot suggests LECA,
    /// since that is how semi-hydro is done. Pots have no separate net pot kind.
    /// </summary>
    public static GrowingMedium? SuggestedMedium(Pot? pot) =>
        pot?.SelfWatering is not null ? GrowingMedium.Leca : null;

    /// <summary>
    /// The medium to have once a pot is picked. What the person chose themselves stays, and so does
    /// the current one when the pot has no suggestion.
    /// </summary>
    public static GrowingMedium MediumAfterPicking(Pot? pot, GrowingMedium current, bool chosenByPerson) =>
        chosenByPerson ? current : SuggestedMedium(pot) ?? current;
}
