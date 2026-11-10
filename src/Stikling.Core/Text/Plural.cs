using System.Globalization;
using System.Text.RegularExpressions;
using Stikling.Core.Models;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Text;

/// <summary>
/// Picks between the singular and the plural of a counted text and puts the number in. The texts
/// come from the resource files as a pair, like <c>Plant_One</c> and <c>Plant_Other</c>, with
/// <c>{0}</c> where the number goes. English and Danish both have just these two forms.
/// </summary>
public static class Plural
{
    public static string Of(int count, string one, string other) =>
        F(count == 1 ? one : other, count);
}

/// <summary>The one place that decides which culture dates and times are written in.</summary>
public static class TextCulture
{
    /// <summary>
    /// English uses the invariant culture, so it reads "19 Sep 2026" where en-GB would write "Sept".
    /// Any other language writes its dates the way its own culture does.
    /// </summary>
    public static CultureInfo ForDates =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

    /// <summary>
    /// How dates are written. Set from the person's settings in the web project only, so the server keeps
    /// the default, the language's own pattern.
    /// </summary>
    public static DateStyle Style { get; set; } = DateStyle.DayFirst;

    /// <summary>The clock times are shown on. Set the same way as <see cref="Style"/>.</summary>
    public static ClockStyle Clock { get; set; } = ClockStyle.TwentyFourHour;

    /// <summary>Writes a date with a pattern from the resource files, in the chosen date style.</summary>
    public static string Date(DateOnly date, string pattern) => date.ToString(Styled(pattern), ForDates);

    private static readonly Regex WeekdayToken = new("d{3,4}", RegexOptions.Compiled);
    private static readonly Regex DayToken = new("(?<!d)d{1,2}(?!d)", RegexOptions.Compiled);
    private static readonly Regex MonthToken = new("M{3,4}", RegexOptions.Compiled);
    private static readonly Regex YearToken = new("y+", RegexOptions.Compiled);
    private static readonly Regex Quoted = new("'[^']*'", RegexOptions.Compiled);

    /// <summary>
    /// Takes a date pattern from the resource files and returns the pattern for the chosen date style.
    /// Day first is the pattern as it is. A pattern without a day and a month, like "MMMM yyyy", is left
    /// alone in every style, and so is one with a full month name and no year in year first, since
    /// "2026-11-05" has no room for "Thursday 5 November".
    /// </summary>
    public static string Styled(string pattern) => Styled(pattern, Style);

    public static string Styled(string pattern, DateStyle style)
    {
        if (style == DateStyle.DayFirst)
            return pattern;

        var bare = Quoted.Replace(pattern, m => new string(' ', m.Length));
        if (!DayToken.IsMatch(bare))
            return pattern;

        var month = MonthToken.Match(bare);
        if (!month.Success)
            return pattern;

        var weekday = WeekdayToken.Match(bare);
        var hasYear = YearToken.IsMatch(bare);

        if (style == DateStyle.MonthFirst)
        {
            var text = (weekday.Success ? weekday.Value + ", " : "") + month.Value + " d" + (hasYear ? ", yyyy" : "");
            return text;
        }

        var fullMonth = month.Value.Length == 4;
        if (fullMonth && !hasYear)
            return pattern;
        var core = hasYear ? "yyyy-MM-dd" : "MM-dd";
        return weekday.Success ? weekday.Value + " " + core : core;
    }

    /// <summary>The time of day in the chosen clock, like "14:30" or "2:30 PM".</summary>
    public static string TimeOf(TimeOnly time) =>
        time.ToString(Clock == ClockStyle.TwelveHour ? "h:mm tt" : "HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The local time of day of a moment in the chosen clock.</summary>
    public static string TimeOf(DateTimeOffset local) => TimeOf(TimeOnly.FromDateTime(local.DateTime));
}
