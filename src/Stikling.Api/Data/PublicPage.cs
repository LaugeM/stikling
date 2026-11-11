using Stikling.Core.Sharing;

namespace Stikling.Api.Data;

/// <summary>
/// The public page of a whole collection, reached by its token and, once it has one, by its name.
/// It is only kept on the server and is not a synced record. A page that is turned off keeps its row,
/// so its token answers "turned off" for good, and turning it on again makes a new row with a new token.
/// A collection has one page that is on at a time.
/// </summary>
public class PublicPage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The secret in the address: 16 random bytes, as base64url without padding.</summary>
    public required string Token { get; set; }

    public Guid CollectionId { get; set; }

    /// <summary>Who turned it on. Null when that person has since been deleted from a collection that lives on.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When it was turned off, or null while it is on.</summary>
    public DateTimeOffset? TurnedOffAt { get; set; }

    /// <summary>The page's own name, lower case without the @, or null. Unique among pages that have one. A name given up is kept as a hash in <see cref="PublicPageName"/>.</summary>
    public string? Name { get; set; }

    /// <summary>A title, or null to use the app's own words.</summary>
    public string? Title { get; set; }

    /// <summary>A sentence under the title, or null.</summary>
    public string? Line { get; set; }

    public PublicPageLayout Layout { get; set; } = PublicPageLayout.Shelves;
    public PublicPageOrder Order { get; set; } = PublicPageOrder.Newest;

    public bool ShowPropagations { get; set; } = true;
    public bool ShowSwap { get; set; }
    public bool ShowWishlist { get; set; }
    public bool ShowStories { get; set; }

    /// <summary>Whether search engines may list the page.</summary>
    public bool Searchable { get; set; }

    /// <summary>en or da, the language the page is written in.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Whether the page says its owner is open to swap requests. Only shown with a contact.</summary>
    public bool SwapRequests { get; set; }

    /// <summary>An https link or an email address that swap requests go to, or null.</summary>
    public string? SwapContact { get; set; }

    /// <summary>The device's IANA time zone, so the page counts days the way the device does.</summary>
    public string? TimeZone { get; set; }

    /// <summary>When the name was last claimed, for the daily limit. Stored as a JSON list and only the last 24 hours are kept.</summary>
    public List<DateTimeOffset> NameClaims { get; set; } = [];
}
