using System.Globalization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Text;

/// <summary>
/// A number as an ordinal, like "3rd" or, in Danish, "3.". Which ending an English number takes is
/// decided here; the text of each ending comes from the resource files.
/// </summary>
public static class Ordinal
{
    public static string Of(int number)
    {
        var lastTwo = number % 100;
        var format = lastTwo is >= 11 and <= 13 ? Strings.Shared.OrdinalTh
            : (number % 10) switch
            {
                1 => Strings.Shared.OrdinalSt,
                2 => Strings.Shared.OrdinalNd,
                3 => Strings.Shared.OrdinalRd,
                _ => Strings.Shared.OrdinalTh
            };
        return string.Format(CultureInfo.CurrentCulture, format, number);
    }
}
