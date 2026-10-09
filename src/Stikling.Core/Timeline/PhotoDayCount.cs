using Stikling.Core.Models;

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

    /// <summary>What goes after the date, " · day 24", or nothing when there is no day.</summary>
    public static string Suffix(DateOnly? start, DateOnly taken) =>
        Day(start, taken) is { } day ? $" · day {day}" : "";

    /// <summary>The day a plant counts from: when you got it, if that is a whole day.</summary>
    public static DateOnly? StartOf(Plant plant) =>
        plant.AcquiredOn is { Precision: DatePrecision.Day } acquired ? acquired.Start : null;

    /// <summary>The day a propagation counts from.</summary>
    public static DateOnly StartOf(Propagation propagation) => propagation.StartedOn;
}
