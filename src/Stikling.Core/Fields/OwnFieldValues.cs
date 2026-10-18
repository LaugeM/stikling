using System.Globalization;
using Stikling.Core.Models;

namespace Stikling.Core.Fields;

/// <summary>
/// The text a plant stores for a field of its own, by kind. A number is written with a point and no
/// extra zeros, a date as yyyy-MM-dd, and text and choices as they are typed.
/// </summary>
public static class OwnFieldValues
{
    public const int MaxTextLength = 200;

    /// <summary>
    /// The text to store for what was entered, or null for nothing (empty input). Sets
    /// <paramref name="error"/> and returns null when the input doesn't fit the kind.
    /// </summary>
    public static string? Normalize(OwnFieldKind kind, string? input, out string? error)
    {
        error = null;
        var text = (input ?? "").Trim();
        if (text.Length == 0)
            return null;

        switch (kind)
        {
            case OwnFieldKind.Number:
                if (TryReadNumber(text, out var number))
                    return number.ToString("0.####", CultureInfo.InvariantCulture);
                error = "That isn't a number.";
                return null;
            case OwnFieldKind.Date:
                if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                error = "That isn't a date.";
                return null;
            default:
                if (text.Length > MaxTextLength)
                {
                    error = $"Keep it under {MaxTextLength} characters.";
                    return null;
                }
                return text;
        }
    }

    /// <summary>The value as shown on the plant's page. A value that doesn't fit its kind shows as it was written.</summary>
    public static string Display(OwnFieldKind kind, string stored) =>
        kind == OwnFieldKind.Date && TryReadDate(stored, out var date)
            ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : stored;

    /// <summary>True when the stored text reads as this kind. Text and choices always do.</summary>
    public static bool Fits(OwnFieldKind kind, string stored) => kind switch
    {
        OwnFieldKind.Number => TryReadNumber(stored.Trim(), out _),
        OwnFieldKind.Date => TryReadDate(stored, out _),
        _ => true
    };

    private static bool TryReadDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryReadNumber(string text, out decimal number)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        // "12,5" and "12.5" both mean twelve and a half, so a comma is read as a decimal point
        var plain = text.Replace(',', '.');
        return decimal.TryParse(plain, styles, CultureInfo.InvariantCulture, out number);
    }
}
