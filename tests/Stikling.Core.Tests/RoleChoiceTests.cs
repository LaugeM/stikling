using Stikling.Core.People;

namespace Stikling.Core.Tests;

public class RoleChoiceTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static MemberAccess As(CollectionRole role, bool manages = false, bool owner = false, Guid? id = null) =>
        new(id ?? Me, role, manages, owner);

    [Fact]
    public void The_owner_can_invite_with_all_four()
    {
        var owner = As(CollectionRole.Editor, manages: true, owner: true);

        Assert.Equal(RoleChoice.All, RoleChoices(owner));
        Assert.Equal(4, RoleChoice.ForInvite(owner).Count);
    }

    [Fact]
    public void A_manager_who_is_not_the_owner_cannot_give_the_right_to_add_people()
    {
        var manager = As(CollectionRole.Editor, manages: true);

        Assert.Equal([RoleChoice.CanView, RoleChoice.CanLog, RoleChoice.CanEdit], RoleChoice.ForInvite(manager));
    }

    [Fact]
    public void Someone_who_cannot_manage_has_nothing_to_offer()
    {
        Assert.Empty(RoleChoice.ForInvite(As(CollectionRole.Editor)));
        Assert.Empty(RoleChoice.ForInvite(As(CollectionRole.Helper)));
    }

    [Fact]
    public void The_owner_can_change_anyone_but_themselves_to_all_four()
    {
        var owner = As(CollectionRole.Editor, manages: true, owner: true);

        Assert.Equal(4, RoleChoice.ForChange(owner, As(CollectionRole.Helper, id: Other)).Count);
        Assert.Empty(RoleChoice.ForChange(owner, owner));
    }

    [Fact]
    public void A_manager_changes_viewers_and_helpers_but_not_editors_or_the_owner()
    {
        var manager = As(CollectionRole.Editor, manages: true);

        Assert.Equal(
            [RoleChoice.CanView, RoleChoice.CanLog, RoleChoice.CanEdit],
            RoleChoice.ForChange(manager, As(CollectionRole.Viewer, id: Other)));
        Assert.Empty(RoleChoice.ForChange(manager, As(CollectionRole.Editor, id: Other)));
        Assert.Empty(RoleChoice.ForChange(manager, As(CollectionRole.Editor, manages: true, owner: true, id: Other)));
    }

    [Fact]
    public void Managing_people_goes_with_an_editor_only()
    {
        Assert.Equal(RoleChoice.CanLog, RoleChoice.Of(CollectionRole.Helper, true));
        Assert.Equal(RoleChoice.CanEditAndManage, RoleChoice.Of(CollectionRole.Editor, true));
        Assert.Equal(RoleChoice.CanEdit, RoleChoice.Of(CollectionRole.Editor, false));
    }

    private static IReadOnlyList<RoleChoice> RoleChoices(MemberAccess caller) => RoleChoice.ForInvite(caller);
}
