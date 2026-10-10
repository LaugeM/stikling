using Stikling.Core.Models;

namespace Stikling.Api.Data;

/// <summary>
/// A public page for one plant or propagation, reached by its token. It is only kept on the server
/// and is not a synced record. A link that is turned off keeps its row, so its token answers
/// "turned off" for good, and turning sharing on again makes a new row with a new token.
/// </summary>
public class ShareLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The secret in the address: 16 random bytes, as base64url without padding.</summary>
    public required string Token { get; set; }

    public Guid CollectionId { get; set; }

    public SubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }

    /// <summary>Who turned it on. Null when that person has since been deleted from a collection that lives on.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When it was turned off, or null while it is on.</summary>
    public DateTimeOffset? TurnedOffAt { get; set; }

    public bool ShowNotes { get; set; } = true;

    /// <summary>A name for the page, or null to use the app's own words.</summary>
    public string? Name { get; set; }

    /// <summary>A sentence under the name, or null to use the app's own words.</summary>
    public string? Line { get; set; }

    /// <summary>Photos kept off the page. Stored as a JSON list.</summary>
    public List<Guid> LeftOutPhotoIds { get; set; } = [];

    /// <summary>The device's IANA time zone, so the page counts days the way the device does.</summary>
    public string? TimeZone { get; set; }
}
