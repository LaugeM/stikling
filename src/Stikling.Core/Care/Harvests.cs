using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Care;

/// <summary>What a plant gave this year: how many harvests, and the amounts of the ones that had one.</summary>
/// <param name="Count">All the harvests this year, with an amount or not.</param>
/// <param name="Grams">The grams of the harvests counted in grams.</param>
/// <param name="Pieces">The pieces of the harvests counted in pieces.</param>
public sealed record HarvestYear(int Count, decimal Grams, decimal Pieces)
{
    /// <summary>"This year: 140 g and 12 pieces over 6 harvests". The parts that are zero are left out.</summary>
    public string Text()
    {
        if (Count == 0)
            return "";
        var amounts = Harvests.Join(Grams, Pieces);
        return amounts is null
            ? Plural.Of(Count, Strings.Weighing.HarvestYearCount_One, Strings.Weighing.HarvestYearCount_Other)
            : F(Count, Strings.Weighing.HarvestYearTotal_One, Strings.Weighing.HarvestYearTotal_Other, amounts, Count);
    }
}

/// <summary>Amounts harvested, as text and added up.</summary>
public static class Harvests
{
    /// <summary>"40 g" or "6 pieces" for one amount. Grams follow the weight unit in use.</summary>
    public static string Amount(decimal amount, HarvestUnit unit) =>
        unit == HarvestUnit.Grams
            ? Weights.Grams(amount)
            : F(amount == 1 ? Strings.Weighing.HarvestPieces_One : Strings.Weighing.HarvestPieces_Other,
                amount.ToString("#,##0.##", CultureInfo.CurrentCulture));

    /// <summary>"140 g and 12 pieces", the part that isn't zero when only one is. Null when both are.</summary>
    public static string? Join(decimal grams, decimal pieces) =>
        (grams > 0, pieces > 0) switch
        {
            (true, true) => F(Strings.Weighing.HarvestAnd, Amount(grams, HarvestUnit.Grams), Amount(pieces, HarvestUnit.Pieces)),
            (true, false) => Amount(grams, HarvestUnit.Grams),
            (false, true) => Amount(pieces, HarvestUnit.Pieces),
            _ => null
        };

    /// <summary>The entry's amount as text, or null for a harvest with none or an entry that isn't a harvest.</summary>
    public static string? AmountOf(CareLog entry) =>
        entry is { Kind: CareKind.Harvested, OwnKindId: null, HarvestAmount: { } amount, HarvestUnit: { } unit }
            ? Amount(amount, unit)
            : null;

    /// <summary>The unit of the plant's newest harvest that has one, for the care form to start on. Grams when it has none.</summary>
    public static HarvestUnit LastUnit(IEnumerable<CareLog> logs, Guid plantId) =>
        CareSummary.For(logs, plantId)
            .FirstOrDefault(l => l is { Kind: CareKind.Harvested, HarvestUnit: not null })?.HarvestUnit ?? HarvestUnit.Grams;

    /// <summary>What the plant gave in the calendar year of <paramref name="today"/>.</summary>
    public static HarvestYear ThisYear(IEnumerable<CareLog> logs, Guid plantId, DateOnly today)
    {
        var harvests = logs
            .Where(l => !l.IsDeleted && l.PlantId == plantId && l is { Kind: CareKind.Harvested, OwnKindId: null }
                && l.OccurredOn.Year == today.Year && l.OccurredOn <= today)
            .ToList();
        return new HarvestYear(
            harvests.Count,
            harvests.Where(l => l.HarvestUnit == HarvestUnit.Grams).Sum(l => l.HarvestAmount ?? 0),
            harvests.Where(l => l.HarvestUnit == HarvestUnit.Pieces).Sum(l => l.HarvestAmount ?? 0));
    }
}
