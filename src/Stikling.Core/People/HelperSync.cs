using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.People;

/// <summary>
/// What a helper may send to a collection. The API checks it on every upload (<c>PeopleRules.HelperProblem</c>),
/// and the device follows the same rules before sending, so it doesn't try what the server will refuse.
/// </summary>
public static class HelperSync
{
    /// <summary>The kinds of record a helper may send.</summary>
    public static readonly IReadOnlyList<string> Kinds =
        [RecordKinds.CareLogs.Name, RecordKinds.PropagationCareLogs.Name, SyncKinds.Photos, RecordKinds.Timeline.Name];

    // The API sends these texts, and the device knows a refusal by them. They never change.
    public const string HelperKindProblem = "A helper can't change this kind of record.";
    public const string HelperOwnershipProblem = "Only the person who added it can change it.";

    /// <summary>Whether the server turned a record away for one of the two helper reasons, which sending it again won't change.</summary>
    public static bool IsHelperReason(string? reason) =>
        reason is HelperKindProblem or HelperOwnershipProblem;

    /// <summary>
    /// Why a helper can't send a record of this kind, or null when its kind is fine. Only some kinds
    /// are allowed, and a timeline entry has to be a note.
    /// </summary>
    public static string? KindProblem(string kind, JsonElement data)
    {
        if (!Kinds.Contains(kind, StringComparer.Ordinal))
            return HelperKindProblem;

        // A deletion may arrive as only an id and dates, with no kind left to look at
        var deleted = data.TryGetProperty("deletedAt", out var at) && at.ValueKind != JsonValueKind.Null;
        var hasKind = data.TryGetProperty("kind", out var entryKind);
        if (kind == RecordKinds.Timeline.Name && (hasKind || !deleted)
            && !(hasKind && entryKind.ValueKind == JsonValueKind.String && entryKind.GetString() == nameof(TimelineKind.Note)))
            return HelperKindProblem;

        // The app reads names without looking at case, so a second "Kind" could say something else than the one checked
        if (kind == RecordKinds.Timeline.Name
            && data.EnumerateObject().Count(p => string.Equals(p.Name, "kind", StringComparison.OrdinalIgnoreCase)) > 1)
            return HelperKindProblem;

        return null;
    }

    /// <summary>
    /// Whether this person's role lets them send a pending change from this device. An editor sends
    /// everything. A helper sends the allowed kinds, and only what they added themselves: a record
    /// with no createdBy isn't theirs. A viewer sends nothing.
    /// </summary>
    public static bool MaySend(string kind, JsonElement data, MemberAccess me)
    {
        if (me.CanEdit)
            return true;
        if (!me.CanLog || data.ValueKind != JsonValueKind.Object || KindProblem(kind, data) is not null)
            return false;

        return data.TryGetProperty("createdBy", out var by)
            && by.ValueKind == JsonValueKind.String
            && by.TryGetGuid(out var who)
            && who == me.PersonId;
    }
}
