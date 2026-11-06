using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Care;

public static class WaterChecks
{
    /// <summary>
    /// How the Water plants screen asks: what was chosen, or when nothing was, the meter once any
    /// meter reading has been logged and feeling the soil before that.
    /// </summary>
    public static WaterCheck Effective(WaterCheck? chosen, IEnumerable<CareLog> logs) =>
        chosen ?? (logs.Any(l => !l.IsDeleted && l.Kind == CareKind.MoistureReading) ? WaterCheck.Meter : WaterCheck.Soil);

    /// <summary>
    /// The lines on how to check a plant by the way it is watered, one for each way among the plants
    /// and in a fixed order. A plant with no way set adds nothing. They only say how to check, never to water.
    /// </summary>
    public static IReadOnlyList<string> ByMethod(IEnumerable<Plant> plants)
    {
        var methods = plants.Select(p => p.WaterBy).OfType<WaterMethod>().ToHashSet();
        return [.. new[] { WaterMethod.Bottom, WaterMethod.Top, WaterMethod.Soak }
            .Where(methods.Contains)
            .Select(m => m switch
            {
                WaterMethod.Top => Strings.Care.CheckTopWatered,
                WaterMethod.Soak => Strings.Care.CheckSoaked,
                _ => Strings.Care.CheckBottomWatered
            })];
    }
}
