using System.Globalization;

namespace Stikling.Core.Text;

/// <summary>Writes the size of a file the way people read it, like "540 kB" or "1.2 MB".</summary>
public static class FileSizes
{
    private const long Kilo = 1024;
    private const long Mega = 1024 * 1024;

    /// <summary>
    /// Under 1 MB the size is whole kilobytes, and from there megabytes with one decimal, in the decimal
    /// separator of the language. Only for showing. The units are the same in every language.
    /// </summary>
    public static string Of(long bytes)
    {
        bytes = Math.Max(0, bytes);
        var kilobytes = (long)Math.Round(bytes / (double)Kilo, MidpointRounding.AwayFromZero);
        if (bytes > 0 && kilobytes == 0)
            kilobytes = 1;

        if (bytes < Mega && kilobytes < Kilo)
            return kilobytes.ToString("0", CultureInfo.CurrentCulture) + " kB";

        return (bytes / (double)Mega).ToString("0.0", CultureInfo.CurrentCulture) + " MB";
    }
}
