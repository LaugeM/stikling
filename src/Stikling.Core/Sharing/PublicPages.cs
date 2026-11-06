using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Stikling.Core.Sharing;

/// <summary>How the front of the public collection page is laid out.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PublicPageLayout>))]
public enum PublicPageLayout
{
    /// <summary>A row for each part, swiped sideways.</summary>
    Shelves,

    /// <summary>Everything in one column.</summary>
    LongPage,

    /// <summary>Photos edge to edge, with the names on them.</summary>
    PhotoWall
}

/// <summary>The order the plants, cuttings and wishes come in on the public collection page.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PublicPageOrder>))]
public enum PublicPageOrder
{
    /// <summary>The newest first, by when it was added.</summary>
    Newest,

    /// <summary>By name, A to Z.</summary>
    Name
}

/// <summary>
/// The public page of a whole collection as the app sees it.
/// </summary>
/// <param name="Url">The random address, like https://share.stikling.app/{token}.</param>
/// <param name="NamedUrl">The address with the page's own name, like https://share.stikling.app/@name, or null without a name.</param>
/// <param name="Name">The page's own name without the @, or null.</param>
/// <param name="Title">The title, or null to use the app's own words.</param>
/// <param name="Line">A sentence under the title, or null.</param>
/// <param name="Searchable">Whether search engines may list the page. Off unless the person turns it on.</param>
/// <param name="Language">en or da, the language the page is written in.</param>
public sealed record PublicPageInfo(
    Guid Id,
    string Url,
    string? NamedUrl,
    string? Name,
    string? Title,
    string? Line,
    PublicPageLayout Layout,
    PublicPageOrder Order,
    bool ShowPropagations,
    bool ShowSwap,
    bool ShowWishlist,
    bool ShowStories,
    bool Searchable,
    string Language,
    DateTimeOffset CreatedAt);

/// <summary>Turns on the public page of a collection.</summary>
/// <param name="TimeZone">The device's IANA time zone id, so the page counts days the way the device does.</param>
/// <param name="Language">The app's language, which the page is written in.</param>
public sealed record TurnOnPublicPageRequest(string? TimeZone, string? Language);

/// <summary>Replaces the settings of the page whole, so anything left out here goes back to its default.</summary>
public sealed record UpdatePublicPageRequest(
    string? Title,
    string? Line,
    PublicPageLayout Layout,
    PublicPageOrder Order,
    bool ShowPropagations,
    bool ShowSwap,
    bool ShowWishlist,
    bool ShowStories,
    bool Searchable,
    string? Language,
    string? TimeZone);

/// <summary>Gives the page a name, or null to give its name up.</summary>
public sealed record SetPublicPageNameRequest(string? Name);

/// <summary>Why a name can't be used.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NameProblem>))]
public enum NameProblem
{
    TooShort,
    TooLong,
    Characters,
    Hyphens,
    Reserved
}

/// <summary>The limits the server and the app both follow, so they always agree.</summary>
public static class PublicPageRules
{
    public const int MaxTitleLength = 80;
    public const int MaxLineLength = 200;
    public const int MinNameLength = 3;
    public const int MaxNameLength = 30;

    /// <summary>The most times one page can change its name in a day.</summary>
    public const int MaxNamesPerPagePerDay = 3;

    /// <summary>The most public pages one person can turn on in a day.</summary>
    public const int MaxTurnedOnPerPersonPerDay = 5;

    /// <summary>How many tiles a part page holds.</summary>
    public const int TilesPerPage = 48;

    /// <summary>How many wishlist lines a part page holds.</summary>
    public const int LinesPerPage = 100;

    /// <summary>How many tiles a part has on the front page in the Shelves layout, and for the other parts in the long page.</summary>
    public const int ShelfTiles = 12;

    /// <summary>How many plants the front page carries in the long page layout.</summary>
    public const int LongPageTiles = 24;

    private static readonly HashSet<string> Reserved = new(
        "admin administrator support help about api app www share s static assets photos plants cuttings swap wishlist sitemap robots login signin sign-in signup sign-up account settings privacy terms security official team staff moderator mod root system null undefined contact abuse report mail email status blog news docs dev test"
            .Split(' '),
        StringComparer.Ordinal);

    /// <summary>The name trimmed, in lower case and without a leading @, or null when nothing is left.</summary>
    public static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim().ToLowerInvariant();
        if (trimmed is not null && trimmed.StartsWith('@'))
            trimmed = trimmed[1..].Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Null when the (normalized) name can be used, else what is wrong with it.</summary>
    public static NameProblem? CheckName(string normalized)
    {
        if (normalized.Length < MinNameLength)
            return NameProblem.TooShort;
        if (normalized.Length > MaxNameLength)
            return NameProblem.TooLong;
        if (!normalized.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            return NameProblem.Characters;
        if (normalized.StartsWith('-') || normalized.EndsWith('-') || normalized.Contains("--", StringComparison.Ordinal))
            return NameProblem.Hyphens;
        if (normalized.StartsWith("stikling", StringComparison.Ordinal) || Reserved.Contains(normalized))
            return NameProblem.Reserved;
        return null;
    }

    /// <summary>
    /// What is kept of a name that was given up: SHA-256 of it as 64 lowercase hex characters, so the
    /// name can't be taken again while nothing points back at who had it.
    /// </summary>
    public static string NameHash(string normalized) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
}

/// <summary>The languages a public page can be written in.</summary>
public static class PublicPageLanguages
{
    /// <summary>da for anything that starts with da, else en.</summary>
    public static string Normalize(string? language) =>
        language?.Trim().StartsWith("da", StringComparison.OrdinalIgnoreCase) == true ? "da" : "en";
}
