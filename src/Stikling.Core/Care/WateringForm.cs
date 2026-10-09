using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>
/// How a plant gets its water, which decides how the reminder reads the log. Worked out from the
/// plant each time and never stored.
/// </summary>
public enum WateringForm
{
    /// <summary>Watered from above, and left to dry out in between.</summary>
    TopWatered,

    /// <summary>Takes water up from a reservoir, which is topped up.</summary>
    Reservoir,

    /// <summary>Sits in water.</summary>
    InWater
}

public static class WateringForms
{
    /// <summary>The form for a plant as it is now.</summary>
    /// <param name="pot">Finds a pot by id, to see whether it waters itself. Without it, pots count as ordinary.</param>
    public static WateringForm Of(Plant plant, Func<Guid, Pot?>? pot = null)
    {
        if (plant.Medium == GrowingMedium.Water)
            return WateringForm.InWater;
        if (plant.Medium is GrowingMedium.Leca or GrowingMedium.Pon or GrowingMedium.CormRiser)
            return WateringForm.Reservoir;
        if (plant.WaterInOuterPot || SelfWatering(plant.InnerPotId) || SelfWatering(plant.OuterPotId))
            return WateringForm.Reservoir;
        return WateringForm.TopWatered;

        bool SelfWatering(Guid? id) => id is { } potId && pot?.Invoke(potId)?.SelfWatering is not null;
    }
}
