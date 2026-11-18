namespace Stikling.Core.People;

/// <summary>
/// The person's place in the collection that is open on this device, and what it lets them do. The
/// server checks the same rules on every request (<c>PeopleRules</c> in the API), and a test keeps
/// the two the same. Here they decide what is sent and, in the screens, what is offered.
/// </summary>
public sealed record MemberAccess(Guid PersonId, CollectionRole Role, bool ManagesPeople, bool IsOwner)
{
    /// <summary>Own collection, and signed out: everything is allowed. Signed out uses <see cref="Guid.Empty"/>.</summary>
    public static MemberAccess Full(Guid personId) => new(personId, CollectionRole.Editor, ManagesPeople: true, IsOwner: true);

    /// <summary>Change anything.</summary>
    public bool CanEdit => Role == CollectionRole.Editor;

    /// <summary>Log care, add notes and photos.</summary>
    public bool CanLog => Role is CollectionRole.Helper or CollectionRole.Editor;

    /// <summary>Invite people and change the roles of viewers and helpers.</summary>
    public bool CanManage => IsOwner || (Role == CollectionRole.Editor && ManagesPeople);

    /// <summary>Whether an entry someone made can be changed or removed by this person.</summary>
    public bool CanChangeEntry(Guid? createdBy) =>
        CanEdit || (Role == CollectionRole.Helper && createdBy is { } by && by == PersonId);

    /// <summary>Whether this person may make an invite that gives <paramref name="managesPeople"/>.</summary>
    public bool CanInvite(bool managesPeople) => CanManage && (!managesPeople || IsOwner);

    /// <summary>
    /// Whether this person may give <paramref name="target"/> a role and this setting. Nobody changes their
    /// own row or the owner's. A manager who isn't the owner changes viewers and helpers only.
    /// </summary>
    public bool CanChange(MemberAccess target, bool newManagesPeople)
    {
        if (target.PersonId == PersonId || target.IsOwner)
            return false;
        if (IsOwner)
            return true;
        return CanManage && target.Role != CollectionRole.Editor && !newManagesPeople;
    }

    public bool CanRemove(MemberAccess target)
    {
        if (target.PersonId == PersonId || target.IsOwner)
            return false;
        if (IsOwner)
            return true;
        return CanManage && target.Role != CollectionRole.Editor;
    }

    public bool CanLeave => !IsOwner;

    public bool CanHandOver => IsOwner;
}
