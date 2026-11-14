using System.Globalization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Text;

/// <summary>
/// Shows weights in grams or ounces and converts what is typed. Weights are always stored in grams, so
/// only what is shown and typed changes. The unit follows the length setting: ounces when lengths are
/// in inches, see <see cref="Lengths"/>.
/// </summary>
public static class Weights
{
    private const decimal GramsPerOunce = 28.349523125m;

    /// <summary>The most a pot or a harvest can weigh, in grams.</summary>
    public const int MaxGrams = 100000;

    /// <summary>The unit of a weight stored in grams: "g" or "oz".</summary>
    public static string UnitName => Lengths.IsInches ? Strings.Weighing.UnitOunce : Strings.Weighing.UnitGram;

    // Grams is for showing, with the decimal sign of the language. Never save it.

    /// <summary>A weight stored in grams with its unit, like "1,320 g" or "46.6 oz".</summary>
    public static string Grams(decimal grams) =>
        Lengths.IsInches
            ? (grams / GramsPerOunce).ToString("#,##0.0", CultureInfo.CurrentCulture) + " " + UnitName
            : grams.ToString("#,##0.##", CultureInfo.CurrentCulture) + " " + UnitName;

    /// <summary>The number for an input box showing a weight stored in grams.</summary>
    public static decimal ToInput(decimal grams) => Lengths.IsInches ? Math.Round(grams / GramsPerOunce, 1) : grams;

    /// <summary>The grams to store for a number typed in an input box. Ounces are rounded to a tenth of a gram.</summary>
    public static decimal FromInput(decimal typed) => Lengths.IsInches ? Math.Round(typed * GramsPerOunce, 1) : typed;

    /// <summary>The whole grams to store for a number typed in an input box, for a weight kept as a whole number.</summary>
    public static int WholeFromInput(decimal typed) => (int)Math.Round(FromInput(typed), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Reads what was typed in an input box, with a point or a comma as the decimal sign. Null for an
    /// empty box, and false for text that isn't a number.
    /// </summary>
    public static bool TryParse(string? text, out decimal? grams)
    {
        grams = null;
        var trimmed = text?.Trim() ?? "";
        if (trimmed.Length == 0)
            return true;
        if (!decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var typed))
            return false;
        grams = FromInput(typed);
        return true;
    }
}
