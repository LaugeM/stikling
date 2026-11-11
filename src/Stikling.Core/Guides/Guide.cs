namespace Stikling.Core.Guides;

/// <summary>
/// A page a guide is written from. The title and the maker are shown as they are, in every language,
/// and the link opens the page in the browser. docs/dev/guides.md says what each guide takes from each page.
/// </summary>
/// <param name="Id">Short name used in docs/dev/guides.md.</param>
/// <param name="Title">The title of the page, with the author and the year where the page has them.</param>
/// <param name="Publisher">Who published it.</param>
/// <param name="Url">The address of the page.</param>
public sealed record GuideSource(string Id, string Title, string Publisher, string Url);

/// <summary>
/// One part of a guide. Every text is the name of a key in Stikling.Web's Resources/Guides.resx,
/// so a guide has the same structure in every language. Nothing here is stored.
/// </summary>
public abstract record GuideBlock;

/// <summary>A paragraph.</summary>
public sealed record GuideText(string Key) : GuideBlock;

/// <summary>A heading over blocks.</summary>
public sealed record GuideSection(string TitleKey, IReadOnlyList<GuideBlock> Blocks) : GuideBlock;

/// <summary>A list, numbered for steps.</summary>
public sealed record GuideList(IReadOnlyList<string> Keys, bool Numbered = false) : GuideBlock;

/// <summary>A label with its text after it, like "How long" and what it says.</summary>
public sealed record GuideField(string LabelKey, string TextKey) : GuideBlock;

/// <summary>Ingredients in a table, a name and what it does.</summary>
public sealed record GuideTable(IReadOnlyList<(string NameKey, string DoesKey)> Rows) : GuideBlock;

/// <summary>Pests in a table, each name a link to its guide, with what gives it away.</summary>
public sealed record GuideLinkTable(IReadOnlyList<(string GuideId, string TextKey)> Rows) : GuideBlock;

/// <summary>
/// A photo in a guide. The file is in wwwroot/img/guides. The author, licence and address are shown as they are,
/// in every language. A licence that asks for changes to be marked is why <paramref name="Cropped"/> exists.
/// </summary>
public sealed record GuidePhoto(string File, string CaptionKey, int Width, int Height, string Author, string Licence, string SourceUrl, bool Cropped = false)
{
    /// <summary>The page of the licence, which CC BY and CC BY-SA ask a credit to link to.</summary>
    public string LicenceUrl => LicenceUrlFor(Licence);

    /// <summary>
    /// The Creative Commons page for a licence written the way Wikimedia Commons writes it,
    /// like "CC0", "CC BY-SA 4.0" or "CC BY 3.0 US".
    /// </summary>
    public static string LicenceUrlFor(string licence)
    {
        var parts = licence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["CC0"])
            return "https://creativecommons.org/publicdomain/zero/1.0/";
        if (parts is not ["CC", var kind, var version, ..] || parts.Length > 4)
            throw new ArgumentException($"Not a Creative Commons licence: {licence}", nameof(licence));
        var port = parts.Length == 4 ? parts[3].ToLowerInvariant() + "/" : "";
        return $"https://creativecommons.org/licenses/{kind.ToLowerInvariant()}/{version}/{port}";
    }
}

/// <summary>Photos shown side by side when there is room, with a caption and a credit under each.</summary>
public sealed record GuidePhotos(IReadOnlyList<GuidePhoto> Photos) : GuideBlock;

/// <summary>A paragraph that ends with a link to another guide.</summary>
public sealed record GuideSeeAlso(string TextKey, string GuideId) : GuideBlock;

/// <summary>
/// The problems of a plant group guide: a paragraph, then "Guides:" and a link to the guide of each pest it names.
/// </summary>
/// <param name="TextKey">The paragraph.</param>
/// <param name="GuideIds">The pest guides the links open, in the order shown.</param>
public sealed record GuidePestLinks(string TextKey, IReadOnlyList<string> GuideIds) : GuideBlock;

/// <summary>The end of the semi-hydro guide: start tracking the move for the plant it was opened from.</summary>
public sealed record GuideTrack : GuideBlock;

/// <summary>One way of doing it, for guides where growers do it in different ways. None is marked best.</summary>
/// <param name="Id">Used in the link that opens the guide on this method.</param>
/// <param name="DetailKey">A line under the name, like examples, or null.</param>
public sealed record GuideMethod(string Id, string TitleKey, IReadOnlyList<GuideBlock> Blocks, string? DetailKey = null);

/// <summary>The ways of doing it. On a phone one shows at a time, and a wide screen shows them side by side in this order.</summary>
public sealed record GuideMethods(string LabelKey, IReadOnlyList<GuideMethod> Methods, string? NoteKey = null) : GuideBlock;

/// <summary>A short how-to that is part of the app.</summary>
/// <param name="Id">The name in the address, /guides/{Id}.</param>
/// <param name="Group">Which heading it is listed under, one of the constants in <see cref="GuideBook"/>.</param>
/// <param name="TitleKey">Also the name in the list of guides.</param>
/// <param name="RowKey">The line under the name in the list.</param>
/// <param name="LeadKey">The first paragraph.</param>
/// <param name="WhereKey">Says why the sources are thin, shown under the first paragraph, or null.</param>
/// <param name="LinkKey">The text of the links that open this guide from the place it is about.</param>
public sealed record Guide(
    string Id,
    string Group,
    string TitleKey,
    string RowKey,
    string LeadKey,
    string? WhereKey,
    string LinkKey,
    IReadOnlyList<GuideBlock> Blocks,
    IReadOnlyList<GuideSource> Sources)
{
    /// <summary>Every key of text this guide uses, so a test can check each has an English and a Danish text.</summary>
    public IEnumerable<string> Keys
    {
        get
        {
            yield return TitleKey;
            yield return RowKey;
            yield return LeadKey;
            yield return LinkKey;
            if (WhereKey is not null)
                yield return WhereKey;
            foreach (var key in Blocks.SelectMany(Flatten).SelectMany(KeysOf))
                yield return key;
        }
    }

    private static IEnumerable<GuideBlock> Flatten(GuideBlock block) => block switch
    {
        GuideSection section => section.Blocks.SelectMany(Flatten).Prepend(block),
        GuideMethods methods => methods.Methods.SelectMany(m => m.Blocks).SelectMany(Flatten).Prepend(block),
        _ => [block]
    };

    private static IEnumerable<string> KeysOf(GuideBlock block)
    {
        switch (block)
        {
            case GuideText text:
                yield return text.Key;
                break;
            case GuideSection section:
                yield return section.TitleKey;
                break;
            case GuideList list:
                foreach (var key in list.Keys)
                    yield return key;
                break;
            case GuideField field:
                yield return field.LabelKey;
                yield return field.TextKey;
                break;
            case GuideTable table:
                foreach (var (name, does) in table.Rows)
                {
                    yield return name;
                    yield return does;
                }
                break;
            case GuideLinkTable linkTable:
                foreach (var (_, textKey) in linkTable.Rows)
                    yield return textKey;
                break;
            case GuidePhotos photos:
                foreach (var photo in photos.Photos)
                    yield return photo.CaptionKey;
                break;
            case GuideSeeAlso seeAlso:
                yield return seeAlso.TextKey;
                break;
            case GuidePestLinks pestLinks:
                yield return pestLinks.TextKey;
                yield return "GuidesFor";
                break;
            case GuideMethods methods:
                yield return methods.LabelKey;
                if (methods.NoteKey is not null)
                    yield return methods.NoteKey;
                foreach (var method in methods.Methods)
                {
                    yield return method.TitleKey;
                    if (method.DetailKey is not null)
                        yield return method.DetailKey;
                }
                break;
        }
    }
}
