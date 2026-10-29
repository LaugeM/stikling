using Strings = Stikling.Web.Resources;

namespace Stikling.Web.StaticPages;

/// <summary>
/// The text of the pages the deploy makes outside the app, in the current language: the front page's head tags,
/// the text around the app in index.html and the titles and descriptions of Help, Privacy and the deletion page.
/// tools/static-pages/render.cs writes it to text.json for each language, and prepare-pages.py puts it into the pages.
/// </summary>
public static class StaticPageText
{
    public static IReadOnlyDictionary<string, string> Current() => new Dictionary<string, string>
    {
        ["title"] = Strings.FrontPage.PageTitle,
        ["description"] = Strings.FrontPage.MetaDescription,
        ["ogImageAlt"] = Strings.FrontPage.OgImageAlt,
        ["shellOpening"] = Strings.FrontPage.ShellOpening,
        ["shellStarting"] = Strings.FrontPage.ShellStarting,
        ["shellError"] = Strings.FrontPage.ShellError,
        ["shellReload"] = Strings.FrontPage.ShellReload,
        ["shellClose"] = Strings.FrontPage.ShellClose,
        ["noJavaScript"] = Strings.FrontPage.NoJavaScript,
        ["deletionTitle"] = Strings.FrontPage.StaticDeletionTitle,
        ["deletionDescription"] = Strings.FrontPage.StaticDeletionDescription,
        ["helpTitle"] = Strings.FrontPage.StaticHelpTitle,
        ["helpDescription"] = Strings.FrontPage.StaticHelpDescription,
        ["privacyTitle"] = Strings.FrontPage.StaticPrivacyTitle,
        ["privacyDescription"] = Strings.FrontPage.StaticPrivacyDescription
    };
}
