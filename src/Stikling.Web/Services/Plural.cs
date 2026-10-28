using System.Globalization;

namespace Stikling.Web.Services;

/// <summary>
/// Picks between the singular and the plural of a counted text and puts the number in. The texts
/// come from the resource files as a pair, like <c>Plant_One</c> and <c>Plant_Other</c>, with
/// <c>{0}</c> where the number goes. English and Danish both have just these two forms.
/// </summary>
public static class Plural
{
    public static string Of(int count, string one, string other) =>
        string.Format(CultureInfo.CurrentCulture, count == 1 ? one : other, count);
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
}
