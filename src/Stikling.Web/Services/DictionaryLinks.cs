using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Text;
using Strings = Stikling.Web.Resources;

namespace Stikling.Web.Services;

/// <summary>One name found in the dictionary, with the page it opens.</summary>
public sealed record DictionaryHit(NameSuggestion Name, string Href);

/// <summary>Addresses of the plant dictionary pages, the names of its groups, and finding in it by what is typed.</summary>
public static class DictionaryLinks
{
    public const string Index = "dictionary";

    private static string Escape(string text) => Uri.EscapeDataString(text);

    public static string Group(DictionaryGroup group) => $"dictionary/group/{group}";

    public static string Family(string name) => $"dictionary/family/{Escape(name)}";

    public static string Genus(string genus) => $"dictionary/{Escape(genus)}";

    /// <param name="species">The part after the genus, as in the file.</param>
    public static string Species(string genus, string species) => $"dictionary/{Escape(genus)}/{Escape(species)}";

    public static string Cultivar(string genus, string name) => $"dictionary/cultivar/{Escape(genus)}/{Escape(name)}";

    /// <summary>The page for the most specific part of the name that the dictionary has, or null when the genus isn't in it.</summary>
    public static string? MostSpecific(DictionaryBrowser browser, string? genus, string? species, string? cultivar)
    {
        if (PlantNames.Genus(genus) is not { } g || browser.Genus(g) is null)
            return null;
        if (PlantNames.Cultivar(cultivar) is { } c && browser.Cultivar(g, c) is not null)
            return Cultivar(g, c);
        if (PlantNames.Species(species) is { } s && browser.Species(g, s) is not null)
            return Species(g, s);
        return Genus(g);
    }

    /// <summary>The form for a new plant, or a new wish, with the names filled in.</summary>
    public static string AddHref(string page, string genus, string? species, string? cultivar)
    {
        var href = $"{page}?genus={Escape(genus)}";
        if (!string.IsNullOrWhiteSpace(species))
            href += $"&species={Escape(species)}";
        if (!string.IsNullOrWhiteSpace(cultivar))
            href += $"&cultivar={Escape(cultivar)}";
        return href;
    }

    public static string Label(DictionaryGroup group, CultureInfo? culture = null) => Strings.Dictionary.ResourceManager.GetString($"Group{group}", culture ?? CultureInfo.CurrentUICulture) ?? group.ToString();

    /// <summary>The groups whose name has the typed text in it, in the language of the app or in English.</summary>
    public static IReadOnlyList<GroupSummary> GroupsMatching(DictionaryBrowser browser, string text) =>
        text.Length == 0
            ? []
            : [.. browser.Groups.Where(g =>
                Label(g.Group).Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || Label(g.Group, CultureInfo.InvariantCulture).Contains(text, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The families whose name starts with the typed text.</summary>
    public static IReadOnlyList<FamilySummary> FamiliesMatching(DictionaryBrowser browser, string text) =>
        text.Length == 0 ? [] : [.. browser.Families.Where(f => f.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase))];

    /// <summary>
    /// What the name search finds that the dictionary has a page for. Names that are only on your own
    /// plants and not in the dictionary are left out, since there is no page to open.
    /// </summary>
    public static IReadOnlyList<DictionaryHit> NamesMatching(NameSuggester suggester, DictionaryBrowser browser, string text, int max)
    {
        var hits = new List<DictionaryHit>();
        foreach (var name in suggester.Search(text, max))
        {
            if (MostSpecific(browser, name.Genus, name.Species, name.Cultivar) is { } href)
                hits.Add(new DictionaryHit(name, href));
        }
        return hits;
    }

    /// <summary>A sentence from the resource files with links and italic names in it. The arguments are made by <see cref="Anchor"/> and <see cref="Italic"/>.</summary>
    public static MarkupString Html(string format, params string[] markup) =>
        (MarkupString)string.Format(CultureInfo.CurrentCulture, format, markup.Cast<object>().ToArray());

    public static string Anchor(string href, string text, bool italic = false) =>
        $"<a class=\"link-action\" href=\"{WebUtility.HtmlEncode(href)}\">{(italic ? Italic(text) : WebUtility.HtmlEncode(text))}</a>";

    public static string Italic(string text) => $"<i>{WebUtility.HtmlEncode(text)}</i>";

    public static string CountsLine(int genera, int species) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Dictionary.CountsLine,
            Plural.Of(genera, Strings.Dictionary.Genus_One, Strings.Dictionary.Genus_Other),
            Plural.Of(species, Strings.Dictionary.Species_One, Strings.Dictionary.Species_Other));
}
