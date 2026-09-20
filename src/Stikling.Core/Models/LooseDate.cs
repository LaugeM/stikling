using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>How much of a <see cref="LooseDate"/> is known.</summary>
public enum DatePrecision
{
    Year,
    Month,
    Day
}

/// <summary>
/// A date you only half remember: a whole day, a month, or just a year. You don't always know
/// exactly when you got a plant, and a picker that insists on a day makes you invent one.
///
/// Stored as the text you'd write yourself ("2024", "2024-06", "2024-06-12"), for the same
/// reason the enums are stored as strings: a backup stays readable. Keeping it in one value
/// rather than a date plus a precision flag means the two can never disagree.
/// </summary>
[JsonConverter(typeof(LooseDateJsonConverter))]
public sealed record LooseDate
{
    private LooseDate(int year, int? month, int? day)
    {
        Year = year;
        Month = month;
        Day = day;
    }

    public int Year { get; }
    public int? Month { get; }
    public int? Day { get; }

    public DatePrecision Precision =>
        Day is not null ? DatePrecision.Day
        : Month is not null ? DatePrecision.Month
        : DatePrecision.Year;

    /// <summary>The first day of what's known, for sorting and for placing it on the timeline.</summary>
    public DateOnly Start => new(Year, Month ?? 1, Day ?? 1);

    /// <summary>Just a year.</summary>
    public static LooseDate Of(int year)
    {
        _ = new DateOnly(year, 1, 1); // rejects a year outside the calendar
        return new LooseDate(year, null, null);
    }

    /// <summary>A month, without the day.</summary>
    public static LooseDate Of(int year, int month)
    {
        _ = new DateOnly(year, month, 1);
        return new LooseDate(year, month, null);
    }

    /// <summary>A whole day, the way every other date in the app is kept.</summary>
    public static LooseDate Of(DateOnly date) => new(date.Year, date.Month, date.Day);

    /// <summary>
    /// The same date told to a different precision. Going coarser drops what's no longer shown,
    /// so what's saved always matches what's on screen; going finer falls back to the 1st.
    /// </summary>
    public LooseDate To(DatePrecision precision) => precision switch
    {
        DatePrecision.Year => Of(Year),
        DatePrecision.Month => Of(Year, Month ?? 1),
        _ => Of(Start)
    };

    /// <summary>How it reads in the app: "2024", "June 2024" or "12 Jun 2024".</summary>
    public string Text() => Precision switch
    {
        DatePrecision.Year => Year.ToString("D4", CultureInfo.InvariantCulture),
        DatePrecision.Month => Start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
        _ => Start.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
    };

    /// <summary>How it's stored.</summary>
    public override string ToString() => Precision switch
    {
        DatePrecision.Year => Year.ToString("D4", CultureInfo.InvariantCulture),
        DatePrecision.Month => Start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    public static bool TryParse(string? text, out LooseDate? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        switch (trimmed.Length)
        {
            case 4 when int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var year):
                date = Of(year);
                return true;
            case 7 when Exact(trimmed, "yyyy-MM") is { } month:
                date = Of(month.Year, month.Month);
                return true;
            case 10 when Exact(trimmed, "yyyy-MM-dd") is { } day:
                date = Of(day);
                return true;
            default:
                return false;
        }
    }

    private static DateOnly? Exact(string text, string format) =>
        DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}

public sealed class LooseDateJsonConverter : JsonConverter<LooseDate>
{
    public override LooseDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return LooseDate.TryParse(text, out var date) && date is not null
            ? date
            : throw new JsonException($"Expected a date like 2024, 2024-06 or 2024-06-12, but got '{text}'.");
    }

    public override void Write(Utf8JsonWriter writer, LooseDate value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
