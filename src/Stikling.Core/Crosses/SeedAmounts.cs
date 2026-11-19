using System.Globalization;
using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Crosses;

/// <summary>
/// An amount of seed as a person knows it: a number, seeds or grams, and whether it is only about
/// that. Nothing counts down from it, so a rough "about 100" never turns into a wrong number.
/// </summary>
public static class SeedAmounts
{
    public const decimal Max = 1_000_000m;

    public static IReadOnlyList<string> Errors(decimal? amount, SeedUnit? unit) =>
        amount is { } a && (a <= 0 || a > Max) ? [Strings.Crosses.ErrAmount] : [];

    /// <summary>"about 100 seeds" or "1 g". Null when there is no amount.</summary>
    public static string? Describe(decimal? amount, SeedUnit? unit, bool about)
    {
        if (amount is not { } a || a <= 0)
            return null;

        var number = a.ToString("0.##", CultureInfo.CurrentCulture);
        var text = (unit ?? SeedUnit.Seeds) == SeedUnit.Grams
            ? F(Strings.Crosses.AmountGrams, number)
            : F(a == 1 ? Strings.Crosses.AmountSeeds_One : Strings.Crosses.AmountSeeds_Other, number);
        return about ? F(Strings.Crosses.AmountAbout, text) : text;
    }

    public static string? Describe(SeedLot lot) => Describe(lot.Amount, lot.AmountUnit, lot.AmountIsAbout);

    public static string? Describe(Cross cross) => Describe(cross.SeedAmount, cross.SeedAmountUnit, cross.SeedAmountIsAbout);

    /// <summary>
    /// The number of seeds this amount stands for, when it is counted in seeds. Grams give nothing,
    /// since the weight of a seed isn't known.
    /// </summary>
    public static int? SeedCount(decimal? amount, SeedUnit? unit) =>
        amount is { } a && a >= 1 && (unit ?? SeedUnit.Seeds) == SeedUnit.Seeds && a <= int.MaxValue ? (int)Math.Round(a) : null;
}
