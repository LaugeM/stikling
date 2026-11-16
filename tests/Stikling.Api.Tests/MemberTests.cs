using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using static Stikling.Api.Collections.InviteEndpoints;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class MemberTests(ApiFactory api)
{
    private readonly PeopleSupport _people = new(api);

    private Task<Who> SignIn(string? name = "") => _people.SignIn(name);

    private static async Task<MeResponse> Me(Who who) =>
        (await who.Client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json))!;

    [Fact]
    public async Task The_collection_lists_its_members_with_names_and_rights()
    {
        var owner = await SignIn("Anna");
        var helper = await _people.Join(owner, MemberRole.Helper, name: "Bo");
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true, name: "Carl");

        var collection = await _people.Collection(helper);

        Assert.Equal(owner.PersonId, collection.OwnerPersonId);
        Assert.Equal(["Anna", "Bo", "Carl"], collection.Members.Select(m => m.Name));
        Assert.Equal([true, false, true], collection.Members.Select(m => m.ManagesPeople));
        Assert.Equal([true, false, false], collection.Members.Select(m => m.IsOwner));
        Assert.Equal([MemberRole.Editor, MemberRole.Helper, MemberRole.Editor], collection.Members.Select(m => m.Role));
        Assert.Empty(collection.FormerMembers);
        Assert.Contains(collection.Members, m => m.PersonId == manager.PersonId);
    }

    [Fact]
    public async Task The_owner_changes_a_viewer_to_a_helper_and_an_editor_to_one_who_manages()
    {
        var owner = await SignIn();
        var viewer = await _people.Join(owner, MemberRole.Viewer);
        var editor = await _people.Join(owner, MemberRole.Editor);

        var toHelper = await _people.Change(owner, viewer, MemberRole.Helper);
        var toManager = await _people.Change(owner, editor, MemberRole.Editor, managesPeople: true);

        Assert.Equal(HttpStatusCode.NoContent, toHelper.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, toManager.StatusCode);
        Assert.Equal(MemberRole.Helper, (await _people.MemberOf(owner, viewer)).Role);
        Assert.True((await _people.MemberOf(owner, editor)).ManagesPeople);
    }

    [Fact]
    public async Task The_owner_changes_an_editor_who_manages_to_a_viewer()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);

        var response = await _people.Change(owner, manager, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var member = await _people.MemberOf(owner, manager);
        Assert.Equal(MemberRole.Viewer, member.Role);
        Assert.False(member.ManagesPeople);
    }

    [Fact]
    public async Task Managing_people_goes_with_the_editor_role_only()
    {
        var owner = await SignIn();
        var viewer = await _people.Join(owner, MemberRole.Viewer);

        var response = await _people.Change(owner, viewer, MemberRole.Helper, managesPeople: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(MemberRole.Viewer, (await _people.MemberOf(owner, viewer)).Role);
    }

    [Fact]
    public async Task A_manager_changes_viewers_and_helpers_to_any_role_without_the_right_to_manage()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var viewer = await _people.Join(owner, MemberRole.Viewer);
        var helper = await _people.Join(owner, MemberRole.Helper);

        var viewerToEditor = await _people.Change(manager, viewer, MemberRole.Editor);
        var helperToViewer = await _people.Change(manager, helper, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.NoContent, viewerToEditor.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, helperToViewer.StatusCode);
        Assert.Equal(MemberRole.Editor, (await _people.MemberOf(owner, viewer)).Role);
        Assert.Equal(MemberRole.Viewer, (await _people.MemberOf(owner, helper)).Role);
    }

    [Fact]
    public async Task A_manager_cannot_give_the_right_to_manage()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var viewer = await _people.Join(owner, MemberRole.Viewer);

        var response = await _people.Change(manager, viewer, MemberRole.Editor, managesPeople: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(MemberRole.Viewer, (await _people.MemberOf(owner, viewer)).Role);
    }

    [Fact]
    public async Task A_manager_cannot_change_an_editor_or_the_owner_or_themselves()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var editor = await _people.Join(owner, MemberRole.Editor);

        var editorToViewer = await _people.Change(manager, editor, MemberRole.Viewer);
        var ownerToViewer = await _people.Change(manager, owner, MemberRole.Viewer);
        var selfToViewer = await _people.Change(manager, manager, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.Forbidden, editorToViewer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ownerToViewer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, selfToViewer.StatusCode);
        var collection = await _people.Collection(owner);
        Assert.All(collection.Members, m => Assert.Equal(MemberRole.Editor, m.Role));
    }

    [Fact]
    public async Task Nobody_changes_their_own_row_not_even_the_owner()
    {
        var owner = await SignIn();

        var response = await _people.Change(owner, owner, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(MemberRole.Editor, (await _people.MemberOf(owner, owner)).Role);
    }

    [Fact]
    public async Task Viewers_helpers_and_plain_editors_cannot_change_or_remove_anyone()
    {
        var owner = await SignIn();
        var viewer = await _people.Join(owner, MemberRole.Viewer);
        var helper = await _people.Join(owner, MemberRole.Helper);
        var editor = await _people.Join(owner, MemberRole.Editor);

        foreach (var who in new[] { viewer, helper, editor })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _people.Change(who, viewer, MemberRole.Helper)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _people.Remove(who, helper)).StatusCode);
        }
    }

    [Fact]
    public async Task Changing_someone_who_is_not_a_member_is_404()
    {
        var owner = await SignIn();
        var stranger = await SignIn();

        var change = await _people.Change(owner, stranger, MemberRole.Viewer);
        var remove = await _people.Remove(owner, stranger);

        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }

    [Fact]
    public async Task The_owner_removes_an_editor_who_is_then_shown_as_a_former_member()
    {
        var owner = await SignIn();
        var editor = await _people.Join(owner, MemberRole.Editor, name: "Dora");

        var response = await _people.Remove(owner, editor);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var collection = await _people.Collection(owner);
        Assert.DoesNotContain(collection.Members, m => m.PersonId == editor.PersonId);
        var former = Assert.Single(collection.FormerMembers);
        Assert.Equal(editor.PersonId, former.PersonId);
        Assert.Equal("Dora", former.Name);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.Client.GetAsync($"/collections/{owner.CollectionId}")).StatusCode);
        Assert.Single((await Me(editor)).Collections);
    }

    [Fact]
    public async Task A_manager_removes_viewers_and_helpers_but_not_editors_or_the_owner_or_themselves()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var viewer = await _people.Join(owner, MemberRole.Viewer);
        var helper = await _people.Join(owner, MemberRole.Helper);
        var editor = await _people.Join(owner, MemberRole.Editor);

        Assert.Equal(HttpStatusCode.NoContent, (await _people.Remove(manager, viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _people.Remove(manager, helper)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _people.Remove(manager, editor)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _people.Remove(manager, owner)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _people.Remove(manager, manager)).StatusCode);

        var collection = await _people.Collection(owner);
        Assert.Equal(3, collection.Members.Count);
        Assert.Equal(2, collection.FormerMembers.Count);
    }

    [Fact]
    public async Task The_owner_cannot_be_removed_by_anyone_or_remove_themselves()
    {
        var owner = await SignIn();

        var response = await _people.Remove(owner, owner);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single((await _people.Collection(owner)).Members);
    }

    [Fact]
    public async Task A_member_leaves_and_is_shown_as_a_former_member_with_their_name()
    {
        var owner = await SignIn();
        var helper = await _people.Join(owner, MemberRole.Helper, name: "Ea");

        var response = await _people.Leave(helper);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var collection = await _people.Collection(owner);
        Assert.Single(collection.Members);
        var former = Assert.Single(collection.FormerMembers);
        Assert.Equal(new FormerMemberView(helper.PersonId, "Ea"), new FormerMemberView(former.PersonId, former.Name));
        Assert.Equal(HttpStatusCode.Forbidden, (await helper.Client.GetAsync($"/collections/{owner.CollectionId}")).StatusCode);
    }

    private sealed record FormerMemberView(Guid PersonId, string? Name);

    [Fact]
    public async Task The_owner_cannot_leave_alone_or_with_others()
    {
        var owner = await SignIn();

        var alone = await _people.Leave(owner);
        await _people.Join(owner, MemberRole.Viewer);
        var withOthers = await _people.Leave(owner);

        Assert.Equal(HttpStatusCode.Conflict, alone.StatusCode);
        Assert.Equal("owner", await _people.ProblemOf(alone));
        Assert.Equal(HttpStatusCode.Conflict, withOthers.StatusCode);
        Assert.Equal("owner", await _people.ProblemOf(withOthers));
        Assert.Equal(2, (await _people.Collection(owner)).Members.Count);
    }

    [Fact]
    public async Task Someone_left_with_no_collection_gets_a_new_one_of_their_own()
    {
        var owner = await SignIn();
        var guest = await _people.Join(owner, MemberRole.Viewer);
        // Their own collection is gone, as if they had left it earlier
        await api.WithDbAsync(async db =>
        {
            await db.Memberships.Where(m => m.CollectionId == guest.OwnCollectionId).ExecuteDeleteAsync();
            await db.Collections.Where(c => c.Id == guest.OwnCollectionId).ExecuteDeleteAsync();
        });
        Assert.Single((await Me(guest)).Collections);

        await _people.Leave(guest);

        var collection = Assert.Single((await Me(guest)).Collections);
        Assert.NotEqual(owner.CollectionId, collection.Id);
        Assert.NotEqual(guest.OwnCollectionId, collection.Id);
        Assert.Equal("My plants", collection.Name);
        Assert.Equal(MemberRole.Editor, collection.Role);
        Assert.True(collection.ManagesPeople);
        Assert.True(collection.IsOwner);
    }

    [Fact]
    public async Task Someone_removed_with_no_collection_gets_a_new_one_too()
    {
        var owner = await SignIn();
        var guest = await _people.Join(owner, MemberRole.Viewer);
        await api.WithDbAsync(async db =>
        {
            await db.Memberships.Where(m => m.CollectionId == guest.OwnCollectionId).ExecuteDeleteAsync();
            await db.Collections.Where(c => c.Id == guest.OwnCollectionId).ExecuteDeleteAsync();
        });

        await _people.Remove(owner, guest);

        var collection = Assert.Single((await Me(guest)).Collections);
        Assert.True(collection.IsOwner);
        Assert.NotEqual(owner.CollectionId, collection.Id);
    }

    [Fact]
    public async Task The_owner_hands_the_collection_over_and_the_old_owner_can_then_leave()
    {
        var owner = await SignIn();
        var viewer = await _people.Join(owner, MemberRole.Viewer);

        var response = await _people.HandOver(owner, viewer);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var collection = await _people.Collection(viewer);
        Assert.Equal(viewer.PersonId, collection.OwnerPersonId);
        var newOwner = collection.Members.Single(m => m.PersonId == viewer.PersonId);
        Assert.Equal(MemberRole.Editor, newOwner.Role);
        Assert.True(newOwner.ManagesPeople);
        Assert.True(newOwner.IsOwner);
        var oldOwner = collection.Members.Single(m => m.PersonId == owner.PersonId);
        Assert.Equal(MemberRole.Editor, oldOwner.Role);
        Assert.True(oldOwner.ManagesPeople);
        Assert.False(oldOwner.IsOwner);

        Assert.Equal(HttpStatusCode.NoContent, (await _people.Leave(owner)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _people.Leave(viewer)).StatusCode);
    }

    [Fact]
    public async Task The_new_owner_has_the_rights_of_an_owner()
    {
        var owner = await SignIn();
        var editor = await _people.Join(owner, MemberRole.Editor);
        var other = await _people.Join(owner, MemberRole.Editor);
        await _people.HandOver(owner, editor);

        Assert.Equal(HttpStatusCode.Created, (await _people.CreateInvite(editor, MemberRole.Editor, managesPeople: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _people.Change(editor, other, MemberRole.Viewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _people.Change(owner, editor, MemberRole.Viewer)).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_hands_over_and_only_to_a_member()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var stranger = await SignIn();

        Assert.Equal(HttpStatusCode.Forbidden, (await _people.HandOver(manager, manager)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _people.HandOver(owner, stranger)).StatusCode);
        Assert.Equal(owner.PersonId, (await _people.Collection(owner)).OwnerPersonId);
    }

    [Fact]
    public async Task Handing_over_to_yourself_changes_nothing()
    {
        var owner = await SignIn();

        var response = await _people.HandOver(owner, owner);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(owner.PersonId, (await _people.Collection(owner)).OwnerPersonId);
    }
}
