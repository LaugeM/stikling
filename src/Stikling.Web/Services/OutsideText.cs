using System.Globalization;
using Stikling.Core.Guides;

namespace Stikling.Web.Services;

/// <summary>The text of the pages on other sites in the language of the app, see <see cref="OutsidePages"/>.</summary>
public static class OutsideText
{
    /// <summary>The two-letter code of the language of the app.</summary>
    public static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public static string Text(string key) =>
        Stikling.Web.Resources.OutsidePages.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Title(OutsidePage page) => Text(page.TitleKey);

    public static string Line(OutsidePage page) => Text(page.LineKey);

    /// <summary>
    /// The site's name, followed by the language of the page when it differs from the app's: "RHS, in English"
    /// in Danish. A Danish page found in Danish says only the site.
    /// </summary>
    public static string Site(OutsidePage page)
    {
        var site = page.SiteFor(Language);
        if (page.IsDanishPage(Language))
            return site;
        return Language == "en" ? site : $"{site}, {Text("InEnglish")}";
    }
}
