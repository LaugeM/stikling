using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Core.Flowering;

/// <summary>
/// The words for flowerings, kept out of the pages so they can be tested. They only say when,
/// never what to do.
/// </summary>
public static class FloweringText
{
    public static string StageLabel(FloweringStage stage) => stage switch
    {
        FloweringStage.Buds => "Spike or buds",
        FloweringStage.InFlower => "In flower",
        _ => "Done"
    };

    /// <summary>The line shown after a stage is saved.</summary>
    public static string Marked(FloweringStage stage) => stage switch
    {
        FloweringStage.Buds => "Marked as spike or buds.",
        FloweringStage.InFlower => "Marked in flower.",
        _ => "Marked done flowering."
    };

    /// <summary>"January" to "December".</summary>
    public static string MonthName(int month) =>
        new DateOnly(2000, month, 1).ToString("MMMM", CultureInfo.InvariantCulture);

    /// <summary>The badge on a plant in flower or in bud.</summary>
    public static string Badge(FloweringStage stage) => stage == FloweringStage.Buds ? "In bud" : "In flower";

    /// <summary>
    /// A date as it reads next to a year: a whole day in that year drops the year ("3 Mar"), and
    /// anything else keeps it ("3 Mar 2025", "May 2025").
    /// </summary>
    public static string Date(LooseDate date, int contextYear) =>
        date.Precision == DatePrecision.Day && date.Year == contextYear
            ? date.Start.ToString("d MMM", CultureInfo.InvariantCulture)
            : date.Text();

    /// <summary>"In bud since 3 Mar" or "In flower since 10 Apr", for a flowering that is still going.</summary>
    public static string Since(FloweringStage stage, LooseDate since, int contextYear) =>
        $"{(stage == FloweringStage.Buds ? "In bud" : "In flower")} since {Date(since, contextYear)}";

    /// <summary>What a flowering reads as, without its year. Every date is named so nothing is guessed.</summary>
    public static string Describe(FloweringPeriod period)
    {
        var year = period.Start.Year;

        if (period.IsOpen)
            return period.InFlower is { } open
                ? Since(FloweringStage.InFlower, open, year)
                : Since(FloweringStage.Buds, period.Buds!, year);

        var parts = new List<string>();
        if (period.Buds is { } buds)
            parts.Add($"spike {Date(buds, year)}");

        if (period.InFlower is { } flowers)
        {
            parts.Add(period.Done is { } end
                ? $"in flower {Date(flowers, year)} to {Date(end, year)}"
                : $"in flower from {Date(flowers, year)}");
        }
        else if (period.Done is { } done)
        {
            parts.Add(period.Buds is null ? $"done flowering {Date(done, year)}" : $"done {Date(done, year)}");
        }

        var text = string.Join(", ", parts);
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>The small line under Flowering in the More menu.</summary>
    public static string MenuHint(FloweringNow? now, int contextYear) =>
        now is null
            ? "Spike, buds or flowers"
            : Since(now.Stage, now.Stage == FloweringStage.Buds ? now.Period.Buds! : now.Period.InFlower!, contextYear);

    /// <summary>"in 2024", "in 2024 and 2025", "in 2023, 2024 and 2025".</summary>
    public static string InYears(IEnumerable<int> years)
    {
        var list = years.Order().Select(y => y.ToString(CultureInfo.InvariantCulture)).ToList();
        return "in " + (list.Count <= 1 ? string.Concat(list) : $"{string.Join(", ", list.SkipLast(1))} and {list[^1]}");
    }
}
