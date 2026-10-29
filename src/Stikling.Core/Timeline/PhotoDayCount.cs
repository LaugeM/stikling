using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Timeline;

/// <summary>
/// Which day of a plant's or propagation's life a photo was taken on. The start day is day 0,
/// like the "first root day" on a propagation.
/// </summary>
public static class PhotoDayCount
{
    /// <summary>The day number, or null without a start or for a photo from before it.</summary>
    public static int? Day(DateOnly? start, DateOnly taken) =>
        start is { } s && taken >= s ? taken.DayNumber - s.DayNumber : null;

    /// <summary>"day 24" for a photo of a propagation, or null without a day.</summary>
    public static string? DayLabel(DateOnly? start, DateOnly taken) =>
        Day(start, taken) is { } day ? string.Format(CultureInfo.CurrentCulture, Strings.Propagations.PhotoDay, day) : null;

    /// <summary>What goes after the date, " · day 24" or " · 1 yr 2 mo", or nothing when there is no text.</summary>
    public static string Suffix(string? age) => age is null ? "" : $" · {age}";

    /// <summary>The day a propagation counts from.</summary>
    public static DateOnly StartOf(Propagation propagation) => propagation.StartedOn;

    /// <summary>
    /// How long you had a plant when a photo was taken, counted from when you got it: "first day",
    /// "12 days", "3 mo", "1 yr", "1 yr 2 mo". A month acquired date counts from the 1st and says
    /// nothing under one whole month. Null without a usable date or for a photo from before it.
    /// </summary>
    public static string? PlantAge(Plant plant, DateOnly taken)
    {
        if (plant.AcquiredOn is not { } acquired || acquired.Precision == DatePrecision.Year)
            return null;
        var start = acquired.Start;
        if (taken < start)
            return null;

        // Whole calendar months: the month is complete once the day of the month has been reached.
        // From the 31st, a shorter month is complete on its 1st after the end (31 Jan to 1 Mar).
        var months = (taken.Year - start.Year) * 12 + taken.Month - start.Month;
        if (taken.Day < start.Day)
            months--;

        if (months < 1)
        {
            if (acquired.Precision != DatePrecision.Day)
                return null;
            var days = taken.DayNumber - start.DayNumber;
            return days == 0 ? Strings.Propagations.AgeFirstDay : Plural.Of(days, Strings.Propagations.AgeDays_One, Strings.Propagations.AgeDays_Other);
        }
        var years = months / 12;
        var rest = months % 12;
        return years == 0 ? string.Format(CultureInfo.CurrentCulture, Strings.Propagations.AgeMonths, rest)
            : rest == 0 ? string.Format(CultureInfo.CurrentCulture, Strings.Propagations.AgeYears, years)
            : string.Format(CultureInfo.CurrentCulture, Strings.Propagations.AgeYearsMonths, years, rest);
    }
}
