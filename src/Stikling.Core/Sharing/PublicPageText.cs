using System.Globalization;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Sharing;

/// <summary>
/// The words of the public collection page that the API writes into its HTML. The text lives in
/// Core's <c>PublicPage</c> resource files, which are internal, so the API reads it through here.
/// The page is written in the language of the thread that calls these.
/// </summary>
public static class PublicPageText
{
    public static string Description => Strings.PublicPage.Description;
    public static string NothingYet => Strings.PublicPage.NothingYet;
    public static string Previous => Strings.PublicPage.Previous;
    public static string Next => Strings.PublicPage.Next;
    public static string FooterAbout => Strings.PublicPage.FooterAbout;
    public static string StartYourOwn => Strings.PublicPage.StartYourOwn;
    public static string ReportPage => Strings.PublicPage.ReportPage;
    public static string ReportSubject => Strings.PublicPage.ReportSubject;

    /// <summary>"Ann's plants on Stikling".</summary>
    public static string PageTitle(string title) => Format(Strings.PublicPage.PageTitle, title);

    /// <summary>"Cuttings · Ann's plants on Stikling".</summary>
    public static string PartPageTitle(PublicPartKind kind, string title) =>
        Format(Strings.PublicPage.PartPageTitle, PublicCollectionPage.PartName(kind), title);

    /// <summary>"Back to Ann's plants".</summary>
    public static string BackTo(string title) => Format(Strings.PublicPage.BackTo, title);

    /// <summary>"See all 84".</summary>
    public static string SeeAll(int total) => Format(Strings.PublicPage.SeeAll, total);

    /// <summary>"See all 84 plants".</summary>
    public static string SeeAllPlants(int total) =>
        Format(Strings.PublicPage.SeeAll, Plural.Of(total, Strings.PublicPage.Plants_One, Strings.PublicPage.Plants_Other));

    /// <summary>"Page 2 of 5".</summary>
    public static string PageOf(int page, int count) => Format(Strings.PublicPage.PageOf, page, count);

    /// <summary>"Last updated 4 Oct 2026."</summary>
    public static string LastUpdated(DateOnly day) => Format(Strings.PublicPage.LastUpdated, Date(day));

    /// <summary>"4 Oct 2026", in the culture of the page.</summary>
    public static string Date(DateOnly day) => TextCulture.Date(day, Strings.PublicPage.DateFormat);

    /// <summary>"3 photos".</summary>
    public static string Photos(int count) => Plural.Of(count, Strings.PublicPage.Photo_One, Strings.PublicPage.Photo_Other);

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
