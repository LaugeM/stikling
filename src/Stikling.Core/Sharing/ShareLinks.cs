using Stikling.Core.Models;

namespace Stikling.Core.Sharing;

/// <summary>
/// A share link as the app sees it: the public address of the page for one plant or propagation,
/// and what that page leaves out or says in its own words.
/// </summary>
/// <param name="SubjectType">Plant or Propagation.</param>
/// <param name="Url">The full public address, like https://share.stikling.app/{token}.</param>
/// <param name="ShowNotes">Whether the text of notes is on the page. The photos of a note show either way.</param>
/// <param name="Name">A name for the page, or null to use the app's own words.</param>
/// <param name="Line">A sentence under the name, or null to use the app's own words.</param>
/// <param name="LeftOutPhotoIds">Photos that are kept off the page.</param>
public sealed record ShareLinkInfo(
    Guid Id,
    SubjectType SubjectType,
    Guid SubjectId,
    string Url,
    bool ShowNotes,
    string? Name,
    string? Line,
    IReadOnlyList<Guid> LeftOutPhotoIds,
    DateTimeOffset CreatedAt);

/// <summary>Turns on sharing for one plant or propagation.</summary>
/// <param name="TimeZone">The device's IANA time zone id, so the page counts days the way the device does.</param>
/// <param name="ShowNotes">Whether the text of notes is on the page from the start. Ignored when the subject already has a link.</param>
public sealed record CreateShareLinkRequest(SubjectType SubjectType, Guid SubjectId, string? TimeZone, bool ShowNotes = true);

/// <summary>Replaces the settings of a link whole, so anything left out here goes back to its default.</summary>
public sealed record UpdateShareLinkRequest(
    bool ShowNotes,
    string? Name,
    string? Line,
    IReadOnlyList<Guid> LeftOutPhotoIds,
    string? TimeZone);

/// <summary>The limits the server and the app both follow, so they always agree.</summary>
public static class ShareLinkRules
{
    /// <summary>The longest name for a page, the same as the field on the Share sheet.</summary>
    public const int MaxNameLength = 80;

    /// <summary>The longest line under the name, the same as the field on the Share sheet.</summary>
    public const int MaxLineLength = 200;

    /// <summary>The most links turned on at once in one collection.</summary>
    public const int MaxActivePerCollection = 50;

    /// <summary>The most links one person can create in a day.</summary>
    public const int MaxCreatedPerPersonPerDay = 20;

    /// <summary>The most photos one link can leave out.</summary>
    public const int MaxLeftOutPhotos = 2000;

    /// <summary>The text without spaces around it, or null when nothing is left.</summary>
    public static string? Clean(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
