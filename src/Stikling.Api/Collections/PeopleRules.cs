using System.Text.Json;
using Stikling.Api.Data;
using Stikling.Core.Sync;

namespace Stikling.Api.Collections;

/// <summary>What a person may do in a collection, from the least to the most.</summary>
public enum CollectionRight
{
    /// <summary>Any member.</summary>
    View,

    /// <summary>A helper or an editor.</summary>
    Help,

    /// <summary>An editor.</summary>
    Edit,

    /// <summary>An editor who manages people, or the owner.</summary>
    Manage,

    /// <summary>The owner.</summary>
    Own,
}

/// <summary>What the policy found out about the caller's place in the collection.</summary>
public sealed record MemberRights(Guid PersonId, MemberRole Role, bool ManagesPeople, bool IsOwner)
{
    public bool Allows(CollectionRight right) => right switch
    {
        CollectionRight.View => true,
        CollectionRight.Help => Role.Allows(MemberRole.Helper),
        CollectionRight.Edit => Role.Allows(MemberRole.Editor),
        CollectionRight.Manage => IsOwner || (Role == MemberRole.Editor && ManagesPeople),
        CollectionRight.Own => IsOwner,
        _ => false,
    };
}

/// <summary>The people rules of a collection, as plain functions that are easy to test.</summary>
public static class PeopleRules
{
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);
    public const int MaxOpenInvites = 10;
    public const int MaxInvitesPerPersonPerDay = 20;
    public const int MaxMembers = 20;

    /// <summary>Viewer below helper below editor. The enum's numbers don't say so, since helper was added last.</summary>
    public static int Rank(MemberRole role) => role switch
    {
        MemberRole.Viewer => 0,
        MemberRole.Helper => 1,
        MemberRole.Editor => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    /// <summary>Managing people goes with the editor role only.</summary>
    public static bool IsValidCombination(MemberRole role, bool managesPeople) =>
        !managesPeople || role == MemberRole.Editor;

    /// <summary>Whether the caller may make an invite with this role. Assumes the caller may manage people.</summary>
    public static bool CanInvite(MemberRights caller, bool managesPeople) => !managesPeople || caller.IsOwner;

    /// <summary>
    /// Whether the caller may give <paramref name="target"/> this role and this setting. Nobody changes
    /// their own row or the owner's. A manager who isn't the owner changes viewers and helpers only,
    /// and leaves <c>ManagesPeople</c> alone.
    /// </summary>
    public static bool CanChange(MemberRights caller, MemberRights target, bool newManagesPeople)
    {
        if (target.PersonId == caller.PersonId || target.IsOwner)
            return false;
        if (caller.IsOwner)
            return true;
        return caller.Allows(CollectionRight.Manage) && target.Role != MemberRole.Editor && !newManagesPeople;
    }

    /// <summary>Whether the caller may remove <paramref name="target"/> from the collection.</summary>
    public static bool CanRemove(MemberRights caller, MemberRights target)
    {
        if (target.PersonId == caller.PersonId || target.IsOwner)
            return false;
        if (caller.IsOwner)
            return true;
        return caller.Allows(CollectionRight.Manage) && target.Role != MemberRole.Editor;
    }

    /// <summary>The kinds of record a helper may send.</summary>
    public static readonly IReadOnlyList<string> HelperKinds =
        [RecordKinds.CareLogs.Name, RecordKinds.PropagationCareLogs.Name, SyncKinds.Photos, RecordKinds.Timeline.Name];

    public const string HelperKindProblem = "A helper can't change this kind of record.";
    public const string HelperOwnershipProblem = "Only the person who added it can change it.";

    /// <summary>
    /// Why a helper's record is refused, or null when it is kept. Only some kinds are allowed, a
    /// timeline entry has to be a note, and a record that is already stored can only be changed by
    /// the person who first sent it, which also covers deleting it.
    /// </summary>
    /// <param name="existingCreatedBy">Who first sent the stored record with this kind and id, or null if none or unknown.</param>
    /// <param name="exists">Whether a record with this kind and id is stored.</param>
    public static string? HelperProblem(string kind, JsonElement data, bool exists, Guid? existingCreatedBy, Guid caller)
    {
        if (!HelperKinds.Contains(kind, StringComparer.Ordinal))
            return HelperKindProblem;

        // A deletion may arrive as only an id and dates, with no kind left to look at
        var deleted = data.TryGetProperty("deletedAt", out var at) && at.ValueKind != JsonValueKind.Null;
        var hasKind = data.TryGetProperty("kind", out var entryKind);
        if (kind == RecordKinds.Timeline.Name && (hasKind || !deleted)
            && !(hasKind && entryKind.ValueKind == JsonValueKind.String && entryKind.GetString() == nameof(Stikling.Core.Models.TimelineKind.Note)))
            return HelperKindProblem;

        // The app reads names without looking at case, so a second "Kind" could say something else than the one checked
        if (kind == RecordKinds.Timeline.Name
            && data.EnumerateObject().Count(p => string.Equals(p.Name, "kind", StringComparison.OrdinalIgnoreCase)) > 1)
            return HelperKindProblem;

        if (exists && existingCreatedBy != caller)
            return HelperOwnershipProblem;

        return null;
    }
}
