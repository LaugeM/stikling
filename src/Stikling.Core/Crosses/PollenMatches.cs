using Stikling.Core.Flowering;
using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Crosses;

/// <summary>A plant coming into flower with the stored pollen of its genus from other plants.</summary>
/// <param name="Since">The day the flowering started, which is part of the key for putting the card off for this flowering.</param>
public sealed record FlowerMatch(Plant Plant, FloweringStage Stage, DateOnly Since, IReadOnlyList<PollenView> Pollen)
{
    public string PutOffKey => PutOffs.Pollen(Plant.Id, Since);
}

/// <summary>
/// Finds the plants in the collection whose newest flowering is open (buds or in flower, not done)
/// together with the pollen that isn't used up from other plants of the same genus.
/// </summary>
public static class PollenMatches
{
    public static IReadOnlyList<FlowerMatch> For(
        IEnumerable<Plant> plants,
        IEnumerable<FloweringMark> marks,
        IEnumerable<PollenView> pollen,
        PutOffs putOffs,
        DateOnly today,
        IEnumerable<Cross>? crosses = null)
    {
        var stored = pollen.Where(p => !p.Pollen.IsDeleted && !p.Pollen.IsUsedUp).ToList();
        if (stored.Count == 0)
            return [];

        var markList = marks.ToList();
        var crossList = crosses?.Where(c => !c.IsDeleted).ToList() ?? [];
        var result = new List<FlowerMatch>();
        foreach (var plant in plants.Where(p => !p.IsDeleted && p.InCollection && !string.IsNullOrWhiteSpace(p.Genus)))
        {
            var flowerings = Flowerings.Of(markList, plant.Id, today);
            if (FloweringNow.Of(flowerings, today) is not { Stale: false } now)
                continue;
            if (now.Stage == FloweringStage.Done)
                continue;

            var since = now.Period.Start.Start;
            // A cross already recorded with this plant as pod parent in this flowering is what the card is for
            if (crossList.Any(c => c.PodParentPlantId == plant.Id && c.PollinatedOn >= since))
                continue;

            var match = new FlowerMatch(plant, now.Stage, since,
                [.. stored.Where(p => p.Pollen.FromPlantId != plant.Id && SameGenus(p.Pollen.Genus ?? p.From.Plant?.Genus, plant.Genus))
                          .OrderByDescending(p => p.Pollen.CollectedOn)]);
            if (match.Pollen.Count == 0 || putOffs.IsPutOff(match.PutOffKey, today))
                continue;
            result.Add(match);
        }

        return [.. result.OrderBy(m => m.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static bool SameGenus(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b?.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
