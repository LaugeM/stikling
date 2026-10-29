using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Flowering;

/// <summary>
/// The words for flowerings, kept out of the pages so they can be tested. They only say when,
/// never what to do.
/// </summary>
public static class FloweringText
{
    public static string StageLabel(FloweringStage stage) => stage switch
    {
        FloweringStage.Buds => Strings.Pests.StageBuds,
        FloweringStage.InFlower => Strings.Pests.StageInFlower,
        _ => Strings.Pests.StageDone
    };

    /// <summary>The line shown after a stage is saved.</summary>
    public static string Marked(FloweringStage stage) => stage switch
    {
        FloweringStage.Buds => Strings.Pests.MarkedBuds,
        FloweringStage.InFlower => Strings.Pests.MarkedInFlower,
        _ => Strings.Pests.MarkedDone
    };

    /// <summary>"January" to "December".</summary>
    public static string MonthName(int month) =>
        new DateOnly(2000, month, 1).ToString("MMMM", TextCulture.ForDates);

    /// <summary>The badge on a plant in flower or in bud.</summary>
    public static string Badge(FloweringStage stage) => stage == FloweringStage.Buds ? Strings.Pests.BadgeInBud : Strings.Pests.BadgeInFlower;

    /// <summary>
    /// A date as it reads next to a year: a whole day in that year drops the year ("3 Mar"), and
    /// anything else keeps it ("3 Mar 2025", "May 2025").
    /// </summary>
    public static string Date(LooseDate date, int contextYear) =>
        date.Precision == DatePrecision.Day && date.Year == contextYear
            ? date.Start.ToString(Strings.Pests.DayMonthFormat, TextCulture.ForDates)
            : Written(date);

    /// <summary>"In bud since 3 Mar" or "In flower since 10 Apr", for a flowering that is still going.</summary>
    public static string Since(FloweringStage stage, LooseDate since, int contextYear) =>
        string.Format(
            CultureInfo.CurrentCulture,
            stage == FloweringStage.Buds ? Strings.Pests.SinceInBud : Strings.Pests.SinceInFlower,
            Date(since, contextYear));

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
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Pests.DescribeSpike, Date(buds, year)));

        if (period.InFlower is { } flowers)
        {
            parts.Add(period.Done is { } end
                ? string.Format(CultureInfo.CurrentCulture, Strings.Pests.DescribeInFlowerTo, Date(flowers, year), Date(end, year))
                : string.Format(CultureInfo.CurrentCulture, Strings.Pests.DescribeInFlowerFrom, Date(flowers, year)));
        }
        else if (period.Done is { } done)
        {
            parts.Add(string.Format(
                CultureInfo.CurrentCulture,
                period.Buds is null ? Strings.Pests.DescribeDoneFlowering : Strings.Pests.DescribeDone,
                Date(done, year)));
        }

        var text = string.Join(", ", parts);
        return char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
    }

    /// <summary>The small line under Flowering in the More menu.</summary>
    public static string MenuHint(FloweringNow? now, int contextYear) =>
        now is null
            ? Strings.Pests.MenuHintNone
            : Since(now.Stage, now.Stage == FloweringStage.Buds ? now.Period.Buds! : now.Period.InFlower!, contextYear);

    /// <summary>"in 2024", "in 2024 and 2025", "in 2023, 2024 and 2025".</summary>
    public static string InYears(IEnumerable<int> years)
    {
        var list = years.Order().Select(y => y.ToString(CultureInfo.InvariantCulture)).ToList();
        return list.Count <= 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.Pests.InYearsOne, string.Concat(list))
            : string.Format(CultureInfo.CurrentCulture, Strings.Pests.InYearsMany, string.Join(", ", list.SkipLast(1)), list[^1]);
    }

    /// <summary>"2024", "June 2024" or "12 Jun 2024", the way <see cref="LooseDate.Text"/> reads but in the app's language.</summary>
    private static string Written(LooseDate date) => date.Precision switch
    {
        DatePrecision.Year => date.Year.ToString("D4", CultureInfo.InvariantCulture),
        DatePrecision.Month => date.Start.ToString(Strings.Pests.MonthYearFormat, TextCulture.ForDates),
        _ => date.Start.ToString(Strings.Pests.DayMonthYearFormat, TextCulture.ForDates)
    };
}
