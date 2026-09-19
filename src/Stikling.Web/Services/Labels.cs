using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Web.Services;

/// <summary>Human-readable text for enum values and dates shown in the UI.</summary>
public static class Labels
{
    public static string For(PlantStatus status) => status switch
    {
        PlantStatus.Active => "In collection",
        PlantStatus.Died => "Died",
        PlantStatus.GivenAway => "Given away",
        PlantStatus.Sold => "Sold",
        _ => status.ToString()
    };

    public static string For(PlantOrigin origin) => origin switch
    {
        PlantOrigin.Purchased => "Purchased",
        PlantOrigin.Propagated => "Propagated",
        PlantOrigin.GrownFromSeed => "Grown from seed",
        PlantOrigin.Gift => "Gift",
        PlantOrigin.Swap => "Swap",
        PlantOrigin.Unknown => "Unknown",
        _ => origin.ToString()
    };

    public static string For(GrowingMedium medium) => medium switch
    {
        GrowingMedium.Leca => "LECA",
        GrowingMedium.Pon => "PON",
        GrowingMedium.Sphagnum => "Sphagnum moss",
        _ => medium.ToString()
    };

    // Invariant culture gives "19 Sep 2026" (en-GB would write "Sept")
    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}
