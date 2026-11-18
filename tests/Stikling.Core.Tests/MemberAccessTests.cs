using Stikling.Core.People;

namespace Stikling.Core.Tests;

public class MemberAccessTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static MemberAccess As(CollectionRole role, bool manages = false, bool owner = false, Guid? id = null) =>
        new(id ?? Me, role, manages, owner);

    [Fact]
    public void A_viewer_can_only_read()
    {
        var viewer = As(CollectionRole.Viewer);

        Assert.False(viewer.CanEdit);
        Assert.False(viewer.CanLog);
        Assert.False(viewer.CanManage);
        Assert.False(viewer.CanChangeEntry(Me));
        Assert.False(viewer.CanChangeEntry(null));
    }

    [Fact]
    public void A_helper_can_log_and_change_only_their_own_entries()
    {
        var helper = As(CollectionRole.Helper);

        Assert.False(helper.CanEdit);
        Assert.True(helper.CanLog);
        Assert.False(helper.CanManage);
        Assert.True(helper.CanChangeEntry(Me));
        Assert.False(helper.CanChangeEntry(Other));
        Assert.False(helper.CanChangeEntry(null));
    }

    [Fact]
    public void An_editor_can_change_everything()
    {
        var editor = As(CollectionRole.Editor);

        Assert.True(editor.CanEdit);
        Assert.True(editor.CanLog);
        Assert.False(editor.CanManage);
        Assert.True(editor.CanChangeEntry(Other));
        Assert.True(editor.CanChangeEntry(null));
    }

    [Fact]
    public void Managing_people_is_for_the_owner_and_an_editor_who_manages()
    {
        Assert.True(As(CollectionRole.Editor, manages: true).CanManage);
        Assert.True(As(CollectionRole.Editor, manages: true, owner: true).CanManage);
        Assert.True(As(CollectionRole.Editor, owner: true).CanManage);
        Assert.False(As(CollectionRole.Helper, manages: true).CanManage);
        Assert.False(As(CollectionRole.Viewer, manages: true).CanManage);
    }

    [Fact]
    public void Only_the_owner_hands_over_and_everyone_else_can_leave()
    {
        var owner = MemberAccess.Full(Me);

        Assert.True(owner.CanHandOver);
        Assert.False(owner.CanLeave);
        Assert.False(As(CollectionRole.Editor, manages: true).CanHandOver);
        Assert.True(As(CollectionRole.Editor, manages: true).CanLeave);
    }

    [Fact]
    public void Someone_who_isnt_signed_in_can_do_everything()
    {
        var alone = MemberAccess.Full(Guid.Empty);

        Assert.True(alone.CanEdit);
        Assert.True(alone.CanLog);
        Assert.True(alone.CanManage);
        Assert.True(alone.IsOwner);
    }

    [Fact]
    public void A_manager_who_isnt_the_owner_only_invites_without_managing_rights()
    {
        var manager = As(CollectionRole.Editor, manages: true);

        Assert.True(manager.CanInvite(managesPeople: false));
        Assert.False(manager.CanInvite(managesPeople: true));
        Assert.True(MemberAccess.Full(Me).CanInvite(managesPeople: true));
        Assert.False(As(CollectionRole.Editor).CanInvite(managesPeople: false));
    }

    [Fact]
    public void Nobody_changes_or_removes_themselves_or_the_owner()
    {
        var owner = MemberAccess.Full(Other);
        var manager = As(CollectionRole.Editor, manages: true);

        Assert.False(manager.CanChange(manager, false));
        Assert.False(manager.CanRemove(manager));
        Assert.False(manager.CanChange(owner, false));
        Assert.False(manager.CanRemove(owner));
        Assert.False(MemberAccess.Full(Me).CanRemove(MemberAccess.Full(Me)));
    }

    [Fact]
    public void The_owner_changes_and_removes_anyone_else()
    {
        var owner = MemberAccess.Full(Me);
        var editor = As(CollectionRole.Editor, manages: true, id: Other);

        Assert.True(owner.CanChange(editor, newManagesPeople: true));
        Assert.True(owner.CanChange(editor, newManagesPeople: false));
        Assert.True(owner.CanRemove(editor));
    }

    [Fact]
    public void A_manager_who_isnt_the_owner_only_handles_viewers_and_helpers_and_leaves_managing_alone()
    {
        var manager = As(CollectionRole.Editor, manages: true);
        var helper = As(CollectionRole.Helper, id: Other);
        var viewer = As(CollectionRole.Viewer, id: Other);
        var editor = As(CollectionRole.Editor, id: Other);

        Assert.True(manager.CanChange(helper, false));
        Assert.True(manager.CanChange(viewer, false));
        Assert.False(manager.CanChange(viewer, true));
        Assert.False(manager.CanChange(editor, false));
        Assert.True(manager.CanRemove(helper));
        Assert.True(manager.CanRemove(viewer));
        Assert.False(manager.CanRemove(editor));
    }

    [Fact]
    public void Someone_who_cant_manage_changes_nobody()
    {
        var helper = As(CollectionRole.Helper);
        var target = As(CollectionRole.Viewer, id: Other);

        Assert.False(helper.CanChange(target, false));
        Assert.False(helper.CanRemove(target));
        Assert.False(As(CollectionRole.Editor).CanRemove(target));
    }

    [Theory]
    [InlineData("Viewer", CollectionRole.Viewer)]
    [InlineData("Helper", CollectionRole.Helper)]
    [InlineData("Editor", CollectionRole.Editor)]
    [InlineData("editor", CollectionRole.Editor)]
    [InlineData("Commenter", CollectionRole.Viewer)]
    [InlineData("", CollectionRole.Viewer)]
    [InlineData(null, CollectionRole.Viewer)]
    [InlineData("7", CollectionRole.Viewer)]
    public void A_role_is_read_from_the_apis_text_and_unknown_text_is_a_viewer(string? text, CollectionRole expected) =>
        Assert.Equal(expected, CollectionRoles.Parse(text));
}
