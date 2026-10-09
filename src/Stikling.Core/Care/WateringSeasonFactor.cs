using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>
/// How much the plants in the collection speed up or slow down in a month compared with their own
/// usual pace, so a plant with little history can borrow from the others. Only plants without a
/// grow light belong in it, since a light takes the seasons away.
/// </summary>
public static class WateringSeasonFactor
{
    private const int GapsPerPlant = 6;
    private const int RatiosNeeded = 8;
    private const int PlantsNeeded = 2;

    /// <summary>
    /// Above 1 means the gaps run longer than usual that month. Null unless at least
    /// <see cref="RatiosNeeded"/> gaps from at least <see cref="PlantsNeeded"/> plants say something,
    /// where a plant only counts once it has six gaps to find its own usual from.
    /// </summary>
    /// <param name="unlitPlants">Each plant with its gaps, from <see cref="WateringGuess.Gaps(Plant, IEnumerable{CareLog})"/>.</param>
    public static double? For(
        IEnumerable<(Plant plant, IReadOnlyList<(DateOnly start, int days)> gaps)> unlitPlants,
        int month)
    {
        var ratios = new List<double>();
        var plants = 0;

        foreach (var (_, gaps) in unlitPlants)
        {
            if (gaps.Count < GapsPerPlant)
                continue;

            var usual = gaps.Select(g => g.days).Order().ToList()[gaps.Count / 2];
            var these = gaps.Where(g => g.start.Month == month).Select(g => (double)g.days / usual).ToList();
            if (these.Count == 0)
                continue;

            ratios.AddRange(these);
            plants++;
        }

        if (ratios.Count < RatiosNeeded || plants < PlantsNeeded)
            return null;

        ratios.Sort();
        var middle = ratios.Count / 2;
        return ratios.Count % 2 == 1 ? ratios[middle] : (ratios[middle - 1] + ratios[middle]) / 2;
    }
}
