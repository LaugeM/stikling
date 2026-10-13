using Stikling.Core.Models;

namespace Stikling.Core.Care;

public static class WaterChecks
{
    /// <summary>
    /// How the Water plants screen asks: what was chosen, or when nothing was, the meter once any
    /// meter reading has been logged and feeling the soil before that.
    /// </summary>
    public static WaterCheck Effective(WaterCheck? chosen, IEnumerable<CareLog> logs) =>
        chosen ?? (logs.Any(l => !l.IsDeleted && l.Kind == CareKind.MoistureReading) ? WaterCheck.Meter : WaterCheck.Soil);
}
