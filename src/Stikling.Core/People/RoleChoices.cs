namespace Stikling.Core.People;

/// <summary>
/// One of the four things a person can be given in a collection: a role, and for an editor whether they also
/// add people. Screens name them with the same four labels, so what to offer is decided here.
/// </summary>
public readonly record struct RoleChoice(CollectionRole Role, bool ManagesPeople)
{
    public static readonly RoleChoice CanView = new(CollectionRole.Viewer, false);
    public static readonly RoleChoice CanLog = new(CollectionRole.Helper, false);
    public static readonly RoleChoice CanEdit = new(CollectionRole.Editor, false);
    public static readonly RoleChoice CanEditAndManage = new(CollectionRole.Editor, true);

    /// <summary>From least to most.</summary>
    public static IReadOnlyList<RoleChoice> All { get; } = [CanView, CanLog, CanEdit, CanEditAndManage];

    /// <summary>The choice a role and setting make. Managing people only goes with an editor, so it is dropped from the others.</summary>
    public static RoleChoice Of(CollectionRole role, bool managesPeople) => new(role, role == CollectionRole.Editor && managesPeople);

    /// <summary>What the person may give someone with an invite.</summary>
    public static IReadOnlyList<RoleChoice> ForInvite(MemberAccess caller) =>
        [.. All.Where(c => caller.CanInvite(c.ManagesPeople))];

    /// <summary>What the person may change <paramref name="target"/> to. Empty when they can't change them at all.</summary>
    public static IReadOnlyList<RoleChoice> ForChange(MemberAccess caller, MemberAccess target) =>
        [.. All.Where(c => caller.CanChange(target, c.ManagesPeople))];
}

/// <summary>What the API holds people to, as screens say it. <c>PeopleRules</c> and the name lengths in the API are the source.</summary>
public static class PeopleLimits
{
    /// <summary>How many days an invite link works.</summary>
    public const int InviteDays = 7;

    /// <summary>The most people in one collection.</summary>
    public const int MaxMembers = 20;

    public const int MaxPersonNameLength = 40;

    public const int MaxInviteForNameLength = 60;

    public const int MaxCollectionNameLength = 100;
}
