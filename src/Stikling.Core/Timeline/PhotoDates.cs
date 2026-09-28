using System.Globalization;
using System.Text.RegularExpressions;
using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>Where a photo's date came from.</summary>
public enum PhotoDateSource
{
    /// <summary>The date the camera saved in the photo.</summary>
    Camera,

    /// <summary>A date in the file name, e.g. IMG-20230514-WA0003.jpg.</summary>
    FileName,

    /// <summary>The file's own date. On a phone that is often when it was copied or picked, not taken.</summary>
    File
}

/// <summary>
/// Dates a photo by when it was taken rather than when it was added, so photos from the gallery
/// land on the right day in a plant's history.
/// </summary>
public static partial class PhotoDates
{
    /// <summary>
    /// The best date there is for a photo: the one the camera saved in it, then a date in the file
    /// name, then the file's own date. A date in the future means a wrong clock, so it is passed over.
    /// </summary>
    /// <param name="cameraDate">The EXIF date as the camera wrote it, e.g. "2023:05:14 10:22:31".</param>
    public static (DateTimeOffset TakenAt, PhotoDateSource Source) Pick(
        string? cameraDate, string? fileName, DateTimeOffset fileDate, TimeProvider time)
    {
        var now = time.GetUtcNow();

        if (FromCamera(cameraDate) is { } taken && time.MomentAt(taken) <= now)
            return (time.MomentAt(taken), PhotoDateSource.Camera);

        if (FromFileName(fileName) is var (day, timeOfDay))
        {
            var moment = timeOfDay is { } t ? time.MomentAt(day.ToDateTime(t)) : time.MomentOn(day);
            if (moment <= now)
                return (moment, PhotoDateSource.FileName);
        }

        return (fileDate > now ? now : fileDate, PhotoDateSource.File);
    }

    /// <summary>Reads an EXIF date, which uses colons in the date part too.</summary>
    public static DateTime? FromCamera(string? text) =>
        DateTime.TryParseExact(text?.Trim(), ["yyyy:MM:dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var taken)
            ? taken
            : null;

    /// <summary>
    /// The date in a file name, and the time when there is one. Covers the names phones and
    /// apps give photos: IMG_20230514_102231.jpg, PXL_20230514_102231123.jpg,
    /// IMG-20230514-WA0003.jpg, Screenshot_2023-05-14-10-22-31.png and the like.
    /// </summary>
    public static (DateOnly Day, TimeOnly? Time)? FromFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        foreach (Match match in DateInName().Matches(fileName))
        {
            if (!TryDay(match, out var day))
                continue;

            var time = match.Groups["hour"].Success && TryTime(match, out var t) ? t : (TimeOnly?)null;
            return (day, time);
        }

        return null;
    }

    private static bool TryDay(Match match, out DateOnly day)
    {
        var year = int.Parse(match.Groups["year"].Value);
        var month = int.Parse(match.Groups["month"].Value);
        var dayOfMonth = int.Parse(match.Groups["day"].Value);
        var valid = month is >= 1 and <= 12 && dayOfMonth >= 1 && dayOfMonth <= DateTime.DaysInMonth(year, month);
        day = valid ? new DateOnly(year, month, dayOfMonth) : default;
        return valid;
    }

    private static bool TryTime(Match match, out TimeOnly time)
    {
        var hour = int.Parse(match.Groups["hour"].Value);
        var minute = int.Parse(match.Groups["minute"].Value);
        var second = int.Parse(match.Groups["second"].Value);
        var valid = hour < 24 && minute < 60 && second < 60;
        time = valid ? new TimeOnly(hour, minute, second) : default;
        return valid;
    }

    // A year from 2000 on, then month and day with the same separator (or none) between them.
    // A time may follow. Without one, the date can't run on into more digits, so a long number
    // that happens to start with 20 isn't read as a date.
    [GeneratedRegex("""
        (?<!\d)(?<year>20\d{2})(?<sep>[-_.]?)(?<month>\d{2})\k<sep>(?<day>\d{2})
        (?: (?:[-_\sT]|\sat\s) (?<hour>\d{2})[-_.:]?(?<minute>\d{2})[-_.:]?(?<second>\d{2}) | (?!\d) )
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex DateInName();
}
