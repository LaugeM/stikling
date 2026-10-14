using System.Globalization;
using System.Text.RegularExpressions;
using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Import;

/// <summary>Which comes first in a date like 03/04/2024.</summary>
public enum DateOrder
{
    DayFirst,
    MonthFirst
}

/// <summary>
/// Reading the text in a spreadsheet cell as a value. Each method gives null when it can't make
/// sense of the text, so the caller can keep it in About it instead of guessing.
/// </summary>
public static partial class ImportValues
{
    private static readonly Dictionary<string, PlantStatus> Statuses = Words<PlantStatus>(
        [("In collection", PlantStatus.Active)],
        ("active", PlantStatus.Active), ("alive", PlantStatus.Active), ("i samling", PlantStatus.Active), ("levende", PlantStatus.Active),
        ("dead", PlantStatus.Died), ("død", PlantStatus.Died), ("døde", PlantStatus.Died),
        ("gave away", PlantStatus.GivenAway), ("givet væk", PlantStatus.GivenAway), ("bortgivet", PlantStatus.GivenAway),
        ("solgt", PlantStatus.Sold));

    private static readonly Dictionary<string, PlantOrigin> Origins = Words<PlantOrigin>(
        [],
        ("bought", PlantOrigin.Purchased), ("shop", PlantOrigin.Purchased), ("købt", PlantOrigin.Purchased),
        ("cutting", PlantOrigin.Propagated), ("stikling", PlantOrigin.Propagated), ("formeret", PlantOrigin.Propagated),
        ("seed", PlantOrigin.GrownFromSeed), ("frø", PlantOrigin.GrownFromSeed),
        ("gave", PlantOrigin.Gift), ("present", PlantOrigin.Gift),
        ("traded", PlantOrigin.Swap), ("trade", PlantOrigin.Swap), ("byttet", PlantOrigin.Swap), ("bytte", PlantOrigin.Swap),
        ("ukendt", PlantOrigin.Unknown));

    private static readonly Dictionary<string, GrowingMedium> Media = Words<GrowingMedium>(
        [("Orchid bark", GrowingMedium.OrchidBark), ("LECA", GrowingMedium.Leca), ("PON", GrowingMedium.Pon),
         ("Sphagnum moss", GrowingMedium.Sphagnum), ("Corm riser", GrowingMedium.CormRiser)],
        ("potting mix", GrowingMedium.Soil), ("jord", GrowingMedium.Soil), ("muld", GrowingMedium.Soil),
        ("bark", GrowingMedium.OrchidBark),
        ("clay balls", GrowingMedium.Leca), ("lecakugler", GrowingMedium.Leca),
        ("moss", GrowingMedium.Sphagnum), ("mos", GrowingMedium.Sphagnum),
        ("vand", GrowingMedium.Water), ("andet", GrowingMedium.Other));

    // The enum names, the labels the app shows (which live in the web project) and the words people use
    private static Dictionary<string, T> Words<T>((string, T)[] labels, params (string, T)[] more) where T : struct, Enum
    {
        var words = new Dictionary<string, T>();
        foreach (var value in Enum.GetValues<T>())
            words[Key(value.ToString())] = value;
        foreach (var (text, value) in labels.Concat(more))
            words[Key(text)] = value;
        return words;
    }

    // Case, spaces, hyphens and underscores don't count
    private static string Key(string text) =>
        new([.. text.ToLowerInvariant().Where(c => c is not (' ' or '-' or '_'))]);

    public static PlantStatus? ReadStatus(string? text) =>
        Statuses.TryGetValue(Key(text ?? ""), out var value) ? value : null;

    public static PlantOrigin? ReadOrigin(string? text) =>
        Origins.TryGetValue(Key(text ?? ""), out var value) ? value : null;

    public static GrowingMedium? ReadMedium(string? text) =>
        Media.TryGetValue(Key(text ?? ""), out var value) ? value : null;

    /// <summary>
    /// A number with a currency or other text around it: "150 kr", "12,50", "1.234,50", "1,234.50".
    /// With both a comma and a point, the last one is the decimal separator. With one kind, a single
    /// one followed by 1 or 2 digits is the decimal separator, and any other is a thousands separator.
    /// A negative price can't be read.
    /// </summary>
    public static decimal? ReadPrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var start = text.IndexOfAny("0123456789".ToCharArray());
        if (start < 0)
            return null;
        var end = text.LastIndexOfAny("0123456789".ToCharArray());

        var before = text[..start].TrimEnd();
        if (before.EndsWith('-') || before.EndsWith('−'))
            return null;

        // Only digits and separators (and spaces as thousands separators) may sit between the first digit and the last
        var number = new string([.. text[start..(end + 1)].Where(c => c is not (' ' or ' ' or ' '))]);
        if (number.Any(c => !char.IsAsciiDigit(c) && c is not (',' or '.')))
            return null;

        var lastComma = number.LastIndexOf(',');
        var lastPoint = number.LastIndexOf('.');
        char? decimalSeparator;
        if (lastComma >= 0 && lastPoint >= 0)
            decimalSeparator = lastComma > lastPoint ? ',' : '.';
        else if (lastComma >= 0 || lastPoint >= 0)
        {
            var separator = lastComma >= 0 ? ',' : '.';
            var at = Math.Max(lastComma, lastPoint);
            var once = number.Count(c => c == separator) == 1;
            decimalSeparator = once && number.Length - at - 1 is 1 or 2 ? separator : null;
        }
        else
            decimalSeparator = null;

        var digits = decimalSeparator is { } d
            ? string.Concat(number[..number.LastIndexOf(d)].Where(char.IsAsciiDigit)) + "." + number[(number.LastIndexOf(d) + 1)..]
            : string.Concat(number.Where(char.IsAsciiDigit));
        if (digits.StartsWith('.'))
            digits = "0" + digits;

        return decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) ? price : null;
    }

    /// <summary>Tags split on semicolons, commas and bars, tidied like the tags on the plant form.</summary>
    public static List<string> ReadTags(string? text) =>
        PlantTags.Normalize((text ?? "").Split([';', ',', '|'], StringSplitOptions.TrimEntries));

    // Months by name, in English and Danish, in full and as 3 or 4 letter short forms
    private static readonly Dictionary<string, int> MonthNames = BuildMonths();

    private static Dictionary<string, int> BuildMonths()
    {
        var months = new Dictionary<string, int>();
        string[][] names =
        [
            ["january", "januar", "jan"],
            ["february", "februar", "feb"],
            ["march", "marts", "mar"],
            ["april", "apr"],
            ["may", "maj"],
            ["june", "juni", "jun"],
            ["july", "juli", "jul"],
            ["august", "aug"],
            ["september", "sep", "sept"],
            ["october", "oktober", "oct", "okt"],
            ["november", "nov"],
            ["december", "dec"]
        ];
        for (var i = 0; i < names.Length; i++)
            foreach (var name in names[i])
                months[name] = i + 1;
        return months;
    }

    [GeneratedRegex(@"[ T]\d{1,2}:\d{2}.*$")]
    private static partial Regex TimeOfDay();

    [GeneratedRegex(@"^(\d{1,4})[/.\-](\d{1,2})[/.\-](\d{1,4})\.?$")]
    private static partial Regex ThreeParts();

    [GeneratedRegex(@"^(\d{1,4})[/.\-](\d{1,4})$")]
    private static partial Regex TwoParts();

    [GeneratedRegex(@"(?<=\d)(st|nd|rd|th)\b")]
    private static partial Regex Ordinal();

    private static string WithoutTime(string text) => TimeOfDay().Replace(text.Trim(), "").Trim();

    /// <summary>
    /// A date in the forms a spreadsheet gives: 2024, 2024-04, 2024-04-03, 3/4/2024 or 4/3/2024
    /// (by <paramref name="order"/>, with / . or - between), 04/2024, and month names in English or
    /// Danish ("April 2024", "3. maj 2024", "okt 2023"). A time after the date is ignored. A two
    /// digit year is 20yy, or 19yy when that would be after this year. Null when it isn't a date,
    /// like 31/02.
    /// </summary>
    public static LooseDate? ReadDate(string? text, DateOrder order, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var trimmed = WithoutTime(text);
            if (LooseDate.TryParse(trimmed, out var plain))
                return plain;

            if (ThreeParts().Match(trimmed) is { Success: true } three)
            {
                var (a, b, c) = (three.Groups[1].Value, three.Groups[2].Value, three.Groups[3].Value);
                if (a.Length == 4)
                    return Make(Number(a), Number(b), Number(c));
                var (day, month) = order == DateOrder.DayFirst ? (a, b) : (b, a);
                return Year(c, today) is { } year ? Make(year, Number(month), Number(day)) : null;
            }

            if (TwoParts().Match(trimmed) is { Success: true } two)
            {
                var (a, b) = (two.Groups[1].Value, two.Groups[2].Value);
                if (a.Length == 4 && b.Length <= 2)
                    return Make(Number(a), Number(b), null);
                if (b.Length == 4 && a.Length <= 2)
                    return Make(Number(b), Number(a), null);
                return null;
            }

            return ReadWithMonthName(trimmed, today);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static LooseDate? ReadWithMonthName(string text, DateOnly today)
    {
        var cleaned = Ordinal().Replace(text.ToLowerInvariant(), "");
        var tokens = string.Concat(cleaned.Select(c => c is '.' or ',' or '/' or '-' ? ' ' : c))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t is not ("of" or "den" or "the"))
            .ToList();

        var kinds = new List<int?>(); // the month number, or 0 for a number
        foreach (var token in tokens)
        {
            if (token.All(char.IsAsciiDigit))
                kinds.Add(0);
            else if (MonthNames.TryGetValue(token, out var month))
                kinds.Add(month);
            else
                return null;
        }

        switch (kinds)
        {
            // 3 april 2024, or 2024 april 3
            case [0, > 0, 0]:
                return tokens[0].Length == 4
                    ? Make(Number(tokens[0]), kinds[1]!.Value, Number(tokens[2]))
                    : Year(tokens[2], today) is { } y1 ? Make(y1, kinds[1]!.Value, Number(tokens[0])) : null;
            // april 3 2024
            case [> 0, 0, 0]:
                return Year(tokens[2], today) is { } y2 ? Make(y2, kinds[0]!.Value, Number(tokens[1])) : null;
            // april 2024
            case [> 0, 0] when tokens[1].Length == 4:
                return Make(Number(tokens[1]), kinds[0]!.Value, null);
            // 2024 april
            case [0, > 0] when tokens[0].Length == 4:
                return Make(Number(tokens[0]), kinds[1]!.Value, null);
            default:
                return null;
        }
    }

    private static int Number(string digits) => int.Parse(digits, CultureInfo.InvariantCulture);

    // Four digits as they are, two digits as 20yy or, when that is still to come, 19yy
    private static int? Year(string digits, DateOnly today) => digits.Length switch
    {
        4 => Number(digits),
        2 => (2000 + Number(digits) > today.Year ? 1900 : 2000) + Number(digits),
        _ => null
    };

    private static LooseDate? Make(int year, int month, int? day)
    {
        if (year < 1 || year > 9999 || month is < 1 or > 12)
            return null;
        if (day is null)
            return LooseDate.Of(year, month);
        return day >= 1 && day <= DateTime.DaysInMonth(year, month) ? LooseDate.Of(new DateOnly(year, month, day.Value)) : null;
    }

    /// <summary>
    /// The first two parts of a date written as 3/4/2024, when it is one. A date that starts with a
    /// four digit year is read the same either way and gives nothing.
    /// </summary>
    internal static (int First, int Second)? NumericParts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || ThreeParts().Match(WithoutTime(text)) is not { Success: true } match)
            return null;
        return match.Groups[1].Value.Length == 4 ? null : (Number(match.Groups[1].Value), Number(match.Groups[2].Value));
    }
}

/// <summary>Works out whether dates like 03/04/2024 are day first or month first.</summary>
public static class DateOrderGuess
{
    /// <summary>
    /// Looks at all the dates in the date columns. A first part over 12 means day first, a second part
    /// over 12 means month first. Null when they say neither, or both, so the person has to say.
    /// </summary>
    public static DateOrder? FromValues(IEnumerable<string> cells)
    {
        var dayFirst = false;
        var monthFirst = false;
        foreach (var cell in cells)
        {
            if (ImportValues.NumericParts(cell) is not var (first, second))
                continue;
            dayFirst |= first > 12;
            monthFirst |= second > 12;
        }

        return dayFirst == monthFirst ? null : dayFirst ? DateOrder.DayFirst : DateOrder.MonthFirst;
    }

    /// <summary>What the device's language suggests: month first for the US, day first anywhere else.</summary>
    public static DateOrder ForLanguages(IReadOnlyList<string> languages) =>
        languages.Count > 0 && languages[0].EndsWith("-US", StringComparison.OrdinalIgnoreCase)
            ? DateOrder.MonthFirst
            : DateOrder.DayFirst;
}
