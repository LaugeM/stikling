using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stikling.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using static Stikling.Api.Collections.InviteEndpoints;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class InviteTests(ApiFactory api)
{
    private readonly PeopleSupport _people = new(api);

    private Task<Who> SignIn(string? name = "") => _people.SignIn(name);

    private async Task<List<InviteResponse>> OpenInvites(Who who) =>
        (await who.Client.GetFromJsonAsync<List<InviteResponse>>($"/collections/{who.CollectionId}/invites", ApiFactory.Json))!;

    private Task<HttpResponseMessage> Withdraw(Who who, Guid inviteId) =>
        who.Client.DeleteAsync($"/collections/{who.CollectionId}/invites/{inviteId}");

    private Task ExpireInvite(Guid inviteId) =>
        api.WithDbAsync(async db =>
            await db.Invites.Where(i => i.Id == inviteId).ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

    [Fact]
    public async Task An_invite_needs_the_name_of_the_person_making_it()
    {
        var owner = await SignIn(name: null);

        var response = await _people.CreateInvite(owner, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", await response.Content.ReadAsStringAsync());
        Assert.Empty(await OpenInvites(owner));
    }

    [Fact]
    public async Task The_owner_makes_an_invite_that_runs_out_in_a_week()
    {
        var owner = await SignIn("Anna");

        var invite = await _people.InviteOk(owner, MemberRole.Helper);

        Assert.Equal(MemberRole.Helper, invite.Role);
        Assert.False(invite.ManagesPeople);
        Assert.Equal("Anna", invite.CreatedByName);
        Assert.Equal(22, invite.Token!.Length);
        Assert.Equal(TimeSpan.FromDays(7), invite.ExpiresAt - invite.CreatedAt);
        Assert.Equal(invite.Id, Assert.Single(await OpenInvites(owner)).Id);
    }

    [Fact]
    public async Task Viewers_helpers_and_plain_editors_cannot_make_invites()
    {
        var owner = await SignIn();
        var viewer = await _people.Join(owner, MemberRole.Viewer);
        var helper = await _people.Join(owner, MemberRole.Helper);
        var editor = await _people.Join(owner, MemberRole.Editor);

        foreach (var who in new[] { viewer, helper, editor })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _people.CreateInvite(who, MemberRole.Viewer)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await who.Client.GetAsync($"/collections/{owner.CollectionId}/invites")).StatusCode);
        }
    }

    [Fact]
    public async Task Someone_outside_the_collection_cannot_make_invites()
    {
        var owner = await SignIn();
        var stranger = await SignIn();

        var response = await _people.CreateInvite(stranger with { CollectionId = owner.CollectionId }, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_editor_who_manages_people_invites_any_role_but_cannot_give_the_right_to_manage()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);

        foreach (var role in new[] { MemberRole.Viewer, MemberRole.Helper, MemberRole.Editor })
            Assert.Equal(HttpStatusCode.Created, (await _people.CreateInvite(manager, role)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await _people.CreateInvite(manager, MemberRole.Editor, managesPeople: true)).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_gives_the_right_to_manage_and_only_to_an_editor()
    {
        var owner = await SignIn();

        var invite = await _people.InviteOk(owner, MemberRole.Editor, managesPeople: true);
        var viewerWithRight = await _people.CreateInvite(owner, MemberRole.Viewer, managesPeople: true);
        var helperWithRight = await _people.CreateInvite(owner, MemberRole.Helper, managesPeople: true);

        Assert.True(invite.ManagesPeople);
        Assert.Equal(HttpStatusCode.BadRequest, viewerWithRight.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, helperWithRight.StatusCode);
    }

    [Fact]
    public async Task A_collection_has_at_most_10_open_invites()
    {
        var owner = await SignIn();
        var invites = new List<InviteResponse>();
        for (var i = 0; i < PeopleRules.MaxOpenInvites; i++)
            invites.Add(await _people.InviteOk(owner, MemberRole.Viewer));

        var eleventh = await _people.CreateInvite(owner, MemberRole.Viewer);
        await Withdraw(owner, invites[0].Id);
        var afterWithdrawing = await _people.CreateInvite(owner, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterWithdrawing.StatusCode);
    }

    [Fact]
    public async Task A_person_can_make_20_invites_a_day()
    {
        var owner = await SignIn();
        for (var i = 0; i < PeopleRules.MaxInvitesPerPersonPerDay; i++)
        {
            var invite = await _people.InviteOk(owner, MemberRole.Viewer);
            await Withdraw(owner, invite.Id);
        }

        var next = await _people.CreateInvite(owner, MemberRole.Viewer);

        Assert.Equal(HttpStatusCode.TooManyRequests, next.StatusCode);
    }

    [Fact]
    public async Task The_list_has_only_open_invites_newest_first()
    {
        var owner = await SignIn();
        var withdrawn = await _people.InviteOk(owner, MemberRole.Viewer);
        var used = await _people.InviteOk(owner, MemberRole.Viewer);
        var expired = await _people.InviteOk(owner, MemberRole.Viewer);
        var older = await _people.InviteOk(owner, MemberRole.Helper);
        var newer = await _people.InviteOk(owner, MemberRole.Editor);
        await Withdraw(owner, withdrawn.Id);
        await ExpireInvite(expired.Id);
        Assert.Equal(HttpStatusCode.OK, (await _people.Accept(await SignIn(), used.Token!)).StatusCode);

        var open = await OpenInvites(owner);

        Assert.Equal([newer.Id, older.Id], open.Select(i => i.Id));
    }

    [Fact]
    public async Task Withdrawing_an_invite_is_204_every_time_and_the_link_stops_working()
    {
        var owner = await SignIn();
        var other = await SignIn();
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);

        var first = await Withdraw(owner, invite.Id);
        var second = await Withdraw(owner, invite.Id);
        var preview = await _people.Preview(other, invite.Token!);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, preview.StatusCode);
    }

    [Fact]
    public async Task An_invite_of_another_collection_cannot_be_withdrawn_here()
    {
        var owner = await SignIn();
        var other = await SignIn();
        var invite = await _people.InviteOk(other, MemberRole.Viewer);

        var response = await Withdraw(owner, invite.Id);
        var unknown = await Withdraw(owner, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Single(await OpenInvites(other));
    }

    [Fact]
    public async Task A_helper_cannot_withdraw_an_invite()
    {
        var owner = await SignIn();
        var helper = await _people.Join(owner, MemberRole.Helper);
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);

        var response = await Withdraw(helper, invite.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_invite_can_say_who_it_is_for_and_the_list_returns_it_trimmed()
    {
        var owner = await SignIn();

        var invite = await _people.InviteOk(owner, MemberRole.Viewer, forName: "  Plant-sitter in July ");

        Assert.Equal("Plant-sitter in July", invite.ForName);
        Assert.Equal("Plant-sitter in July", Assert.Single(await OpenInvites(owner)).ForName);
    }

    [Fact]
    public async Task A_blank_for_name_is_stored_as_null()
    {
        var owner = await SignIn();

        var invite = await _people.InviteOk(owner, MemberRole.Viewer, forName: "   ");

        Assert.Null(invite.ForName);
        Assert.Null(Assert.Single(await OpenInvites(owner)).ForName);
        string? stored = "not read";
        await api.WithDbAsync(async db => stored = await db.Invites.Where(i => i.Id == invite.Id).Select(i => i.ForName).SingleAsync());
        Assert.Null(stored);
    }

    [Fact]
    public async Task A_for_name_over_60_characters_is_400_and_makes_no_invite()
    {
        var owner = await SignIn();

        var tooLong = await _people.CreateInvite(owner, MemberRole.Viewer, forName: new string('a', 61));
        var longest = await _people.CreateInvite(owner, MemberRole.Viewer, forName: new string('a', 60));

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("forName", await tooLong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
        Assert.Single(await OpenInvites(owner));
    }

    [Fact]
    public async Task The_preview_never_contains_who_the_invite_is_for()
    {
        var owner = await SignIn();
        var invite = await _people.InviteOk(owner, MemberRole.Viewer, forName: "Zebediah Quux");
        var other = await SignIn();

        var signedIn = await (await _people.Preview(other, invite.Token!)).Content.ReadAsStringAsync();
        var anonymous = await (await api.CreateClient().GetAsync($"/invites/{invite.Token!}")).Content.ReadAsStringAsync();

        Assert.Contains("collectionName", signedIn, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zebediah", signedIn);
        Assert.DoesNotContain("Zebediah", anonymous);
        Assert.DoesNotContain("forName", anonymous, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unknown_token_is_404()
    {
        var who = await SignIn();

        Assert.Equal(HttpStatusCode.NotFound, (await _people.Preview(who, "nosuchtoken")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _people.Accept(who, "nosuchtoken")).StatusCode);
    }

    [Fact]
    public async Task Anyone_with_the_link_sees_what_it_is_for_but_joining_needs_a_sign_in()
    {
        var owner = await SignIn("Anna");
        var invite = await _people.InviteOk(owner, MemberRole.Helper);
        var anonymous = api.CreateClient();

        var preview = await anonymous.GetAsync($"/invites/{invite.Token!}");
        var accept = await anonymous.PostAsync($"/invites/{invite.Token!}/accept", null);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(
            new InvitePreview(owner.CollectionId, Stikling.Api.People.CurrentPerson.FirstCollectionName, "Anna", MemberRole.Helper, false, AlreadyMember: false, OwnerName: "Anna", Plants: 0, Cuttings: 0),
            await preview.Content.ReadFromJsonAsync<InvitePreview>(ApiFactory.Json));
        Assert.Equal(HttpStatusCode.Unauthorized, accept.StatusCode);
        Assert.Single((await _people.Collection(owner)).Members);
    }

    private static SyncRecord PropagationRecord(Guid id, DateTimeOffset updatedAt, DateTimeOffset? deletedAt = null) =>
        new("propagations", JsonSerializer.SerializeToElement(new { id, createdAt = PeopleSupport.Monday, updatedAt, deletedAt }, ApiFactory.Json));

    [Fact]
    public async Task The_preview_counts_plants_and_cuttings_and_leaves_out_a_deleted_one()
    {
        var owner = await SignIn();
        var at = PeopleSupport.Monday;
        await _people.PushOk(
            owner,
            PeopleSupport.Plant(Guid.NewGuid(), at), PeopleSupport.Plant(Guid.NewGuid(), at), PeopleSupport.Plant(Guid.NewGuid(), at),
            PropagationRecord(Guid.NewGuid(), at), PropagationRecord(Guid.NewGuid(), at));
        var gonePlant = Guid.NewGuid();
        var goneCutting = Guid.NewGuid();
        await _people.PushOk(owner, PeopleSupport.Plant(gonePlant, at), PropagationRecord(goneCutting, at));
        await _people.PushOk(owner, PlantDeleted(gonePlant, at.AddHours(1)), PropagationRecord(goneCutting, at.AddHours(1), at.AddHours(1)));
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);

        var preview = await (await _people.Preview(await SignIn(), invite.Token!)).Content.ReadFromJsonAsync<InvitePreview>(ApiFactory.Json);

        Assert.Equal(3, preview!.Plants);
        Assert.Equal(2, preview.Cuttings);
    }

    private static SyncRecord PlantDeleted(Guid id, DateTimeOffset at) =>
        new("plants", JsonSerializer.SerializeToElement(new { id, createdAt = PeopleSupport.Monday, updatedAt = at, deletedAt = at }, ApiFactory.Json));

    [Fact]
    public async Task The_preview_gives_the_same_counts_without_a_sign_in()
    {
        var owner = await SignIn();
        await _people.PushOk(owner, PeopleSupport.Plant(Guid.NewGuid(), PeopleSupport.Monday), PropagationRecord(Guid.NewGuid(), PeopleSupport.Monday));
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);

        var preview = await api.CreateClient().GetFromJsonAsync<InvitePreview>($"/invites/{invite.Token!}", ApiFactory.Json);

        Assert.Equal(1, preview!.Plants);
        Assert.Equal(1, preview.Cuttings);
    }

    [Fact]
    public async Task Without_a_sign_in_an_unknown_or_closed_invite_answers_as_it_does_signed_in()
    {
        var owner = await SignIn();
        var withdrawn = await _people.InviteOk(owner, MemberRole.Viewer);
        await Withdraw(owner, withdrawn.Id);
        var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/invites/nosuchtoken")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/invites/{new string('a', 22)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await anonymous.GetAsync($"/invites/{withdrawn.Token!}")).StatusCode);
    }

    [Fact]
    public async Task A_sign_in_that_has_run_out_gets_the_preview_without_being_counted_as_in()
    {
        var owner = await SignIn();
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-real-token");

        var preview = await client.GetAsync($"/invites/{invite.Token!}");

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.False((await preview.Content.ReadFromJsonAsync<InvitePreview>(ApiFactory.Json))!.AlreadyMember);
    }

    [Fact]
    public async Task The_preview_says_who_invited_to_what_and_as_whom()
    {
        var owner = await SignIn("Anna");
        await owner.Client.PutAsJsonAsync($"/collections/{owner.CollectionId}/name", new { name = "Balcony" });
        var invite = await _people.InviteOk(owner, MemberRole.Editor, managesPeople: true);
        var guest = await SignIn(name: null);

        var preview = await (await _people.Preview(guest, invite.Token!)).Content.ReadFromJsonAsync<InvitePreview>(ApiFactory.Json);
        var ownPreview = await (await _people.Preview(owner, invite.Token!)).Content.ReadFromJsonAsync<InvitePreview>(ApiFactory.Json);

        Assert.Equal(new InvitePreview(owner.CollectionId, "Balcony", "Anna", MemberRole.Editor, true, AlreadyMember: false, OwnerName: "Anna"), preview);
        Assert.True(ownPreview!.AlreadyMember);
    }

    [Fact]
    public async Task A_new_person_who_opens_an_invite_is_created_like_after_a_sign_in()
    {
        var owner = await SignIn();
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var preview = await client.GetAsync($"/invites/{invite.Token!}");
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Single(me!.Collections);
    }

    [Fact]
    public async Task A_used_withdrawn_or_expired_invite_is_410_to_preview_and_accept()
    {
        var owner = await SignIn();
        var used = await _people.InviteOk(owner, MemberRole.Viewer);
        var withdrawn = await _people.InviteOk(owner, MemberRole.Viewer);
        var expired = await _people.InviteOk(owner, MemberRole.Viewer);
        await _people.Accept(await SignIn(), used.Token!);
        await Withdraw(owner, withdrawn.Id);
        await ExpireInvite(expired.Id);
        var guest = await SignIn();

        foreach (var invite in new[] { used, withdrawn, expired })
        {
            Assert.Equal(HttpStatusCode.Gone, (await _people.Preview(guest, invite.Token!)).StatusCode);
            Assert.Equal(HttpStatusCode.Gone, (await _people.Accept(guest, invite.Token!)).StatusCode);
        }

        Assert.DoesNotContain((await _people.Collection(owner)).Members, m => m.PersonId == guest.PersonId);
    }

    [Theory]
    [InlineData(MemberRole.Viewer, false)]
    [InlineData(MemberRole.Helper, false)]
    [InlineData(MemberRole.Editor, false)]
    [InlineData(MemberRole.Editor, true)]
    public async Task Accepting_adds_the_person_with_the_role_of_the_invite(MemberRole role, bool managesPeople)
    {
        var owner = await SignIn();
        var invite = await _people.InviteOk(owner, role, managesPeople);
        var guest = await SignIn();

        var response = await _people.Accept(guest, invite.Token!);
        var summary = await response.Content.ReadFromJsonAsync<CollectionSummary>(ApiFactory.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new CollectionSummary(owner.CollectionId, "My plants", role, managesPeople, IsOwner: false, owner.Name), summary);
        var member = await _people.MemberOf(owner, guest with { CollectionId = owner.CollectionId });
        Assert.Equal(role, member.Role);
        Assert.Equal(managesPeople, member.ManagesPeople);
        Assert.False(member.IsOwner);
        var me = await guest.Client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        Assert.Equal(2, me!.Collections.Count);
        await api.WithDbAsync(async db => Assert.Equal(guest.PersonId, (await db.Invites.SingleAsync(i => i.Id == invite.Id)).UsedBy));
    }

    [Fact]
    public async Task Someone_who_is_in_already_gets_their_place_as_it_is_and_the_invite_stays_open()
    {
        var owner = await SignIn();
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);

        var response = await _people.Accept(owner, invite.Token!);
        var summary = await response.Content.ReadFromJsonAsync<CollectionSummary>(ApiFactory.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MemberRole.Editor, summary!.Role);
        Assert.True(summary.IsOwner);
        Assert.Single(await OpenInvites(owner));
    }

    [Fact]
    public async Task Accepting_needs_a_name_and_leaves_the_invite_open()
    {
        var owner = await SignIn();
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);
        var guest = await SignIn(name: null);

        var response = await _people.Accept(guest, invite.Token!);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", await response.Content.ReadAsStringAsync());
        Assert.Single(await OpenInvites(owner));
    }

    [Fact]
    public async Task A_collection_with_20_people_is_full()
    {
        var owner = await SignIn();
        await api.WithDbAsync(async db =>
        {
            for (var i = 1; i < PeopleRules.MaxMembers; i++)
            {
                var person = new Person { ClerkUserId = ApiFactory.NewClerkUserId(), Name = $"Member {i}", CreatedAt = PeopleSupport.Monday };
                db.Memberships.Add(new Membership { CollectionId = owner.CollectionId, Person = person, Role = MemberRole.Viewer, CreatedAt = PeopleSupport.Monday });
            }
            await db.SaveChangesAsync();
        });
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);
        var guest = await SignIn();

        var response = await _people.Accept(guest, invite.Token!);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("full", await _people.ProblemOf(response));
        Assert.Single(await OpenInvites(owner));
    }

    [Fact]
    public async Task Joining_again_removes_the_former_member_row()
    {
        var owner = await SignIn();
        var guest = await _people.Join(owner, MemberRole.Viewer);
        await _people.Leave(guest);
        Assert.Single((await _people.Collection(owner)).FormerMembers);

        var invite = await _people.InviteOk(owner, MemberRole.Helper);
        var again = await _people.Accept(guest, invite.Token!);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var collection = await _people.Collection(owner);
        Assert.Empty(collection.FormerMembers);
        Assert.Equal(MemberRole.Helper, collection.Members.Single(m => m.PersonId == guest.PersonId).Role);
    }

    [Fact]
    public async Task Two_people_accepting_the_same_invite_at_once_get_one_place()
    {
        var owner = await SignIn();
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);
        var one = await SignIn();
        var two = await SignIn();

        var results = await Task.WhenAll(_people.Accept(one, invite.Token!), _people.Accept(two, invite.Token!));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Gone], results.Select(r => r.StatusCode).Order());
        Assert.Equal(2, (await _people.Collection(owner)).Members.Count);
    }

    [Fact]
    public async Task The_invites_of_someone_who_left_was_removed_or_stopped_managing_stop_working()
    {
        var owner = await SignIn();
        var leaves = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var removed = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var demoted = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var fromLeaver = await _people.InviteOk(leaves, MemberRole.Editor);
        var fromRemoved = await _people.InviteOk(removed, MemberRole.Editor);
        var fromDemoted = await _people.InviteOk(demoted, MemberRole.Editor);
        var fromOwner = await _people.InviteOk(owner, MemberRole.Viewer);

        await _people.Leave(leaves);
        await _people.Remove(owner, removed);
        await _people.Change(owner, demoted, MemberRole.Editor, managesPeople: false);

        var guest = await SignIn();
        Assert.Equal(HttpStatusCode.Gone, (await _people.Preview(guest, fromLeaver.Token!)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await _people.Preview(guest, fromRemoved.Token!)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await _people.Preview(guest, fromDemoted.Token!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _people.Preview(guest, fromOwner.Token!)).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_sees_the_link_of_an_invite_that_lets_someone_manage_people()
    {
        var owner = await SignIn();
        var manager = await _people.Join(owner, MemberRole.Editor, managesPeople: true);
        var managing = await _people.InviteOk(owner, MemberRole.Editor, managesPeople: true);
        var plain = await _people.InviteOk(owner, MemberRole.Viewer);

        var forManager = await OpenInvites(manager);
        var forOwner = await OpenInvites(owner);

        Assert.Null(forManager.Single(i => i.Id == managing.Id).Token);
        Assert.Equal(plain.Token, forManager.Single(i => i.Id == plain.Id).Token);
        Assert.Equal(managing.Token, forOwner.Single(i => i.Id == managing.Id).Token);
    }

    [Fact]
    public async Task Someone_left_in_no_collection_gets_one_the_next_time_they_ask_who_they_are()
    {
        var person = await SignIn();
        await api.WithDbAsync(async db =>
        {
            await db.Collections.Where(c => c.Id == person.OwnCollectionId).ExecuteDeleteAsync();
        });

        var me = await person.Client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);

        var collection = Assert.Single(me!.Collections);
        Assert.True(collection.IsOwner);
        Assert.NotEqual(person.OwnCollectionId, collection.Id);
    }
    [Fact]
    public async Task An_invite_is_stored_as_a_hash_and_an_encrypted_token_never_in_plain_text()
    {
        var owner = await SignIn("Anna");
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);

        await api.WithDbAsync(async db =>
        {
            var stored = await db.Invites.AsNoTracking().SingleAsync(i => i.Id == invite.Id);
            Assert.Equal(InviteTokens.Hash(invite.Token!), stored.TokenHash);
            Assert.DoesNotContain(invite.Token!, Encoding.ASCII.GetString(stored.TokenCiphertext));
            Assert.Equal(0, await PlainColumnsAsync(db));
        });
    }

    [Fact]
    public async Task An_invite_is_found_by_the_hash_of_its_token()
    {
        var owner = await SignIn("Anna");
        var invite = await _people.InviteOk(owner, MemberRole.Viewer);
        var other = $"x{Guid.NewGuid():N}"[..22];

        // Point the row at another token's hash. The old token no longer finds it and the other one does
        await api.WithDbAsync(async db =>
            await db.Invites.Where(i => i.Id == invite.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.TokenHash, InviteTokens.Hash(other))));

        Assert.Equal(HttpStatusCode.NotFound, (await _people.Preview(await SignIn(), invite.Token!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _people.Preview(await SignIn(), other)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _people.Accept(await SignIn("Bo"), other)).StatusCode);
    }

    [Fact]
    public async Task The_list_of_open_invites_gives_back_the_token_it_was_made_with()
    {
        var owner = await SignIn("Anna");
        var one = await _people.InviteOk(owner, MemberRole.Viewer);
        var two = await _people.InviteOk(owner, MemberRole.Editor);

        var open = await OpenInvites(owner);

        Assert.Equal(one.Token, open.Single(i => i.Id == one.Id).Token);
        Assert.Equal(two.Token, open.Single(i => i.Id == two.Id).Token);
        Assert.NotEqual(one.Token, two.Token);
    }

    [Fact]
    public void The_api_does_not_start_without_an_invite_key()
    {
        using var factory = api.InEnvironment("Testing", new() { [InviteTokens.KeySetting] = "" });

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains(InviteTokens.KeySetting, error.Message);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("c2hvcnQ=")]
    public void An_invite_key_that_is_not_32_bytes_of_base64_is_refused(string key)
    {
        Assert.Throws<InvalidOperationException>(() => TokensWithKey(key));
    }

    [Fact]
    public async Task Another_key_than_the_one_the_open_invites_were_made_with_stops_the_start()
    {
        var owner = await SignIn("Anna");
        await _people.InviteOk(owner, MemberRole.Viewer);
        var otherKey = TokensWithKey(Convert.ToBase64String(new byte[32]));

        await api.WithDbAsync(async db =>
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseStartup.CheckKeyAsync(db, otherKey, TimeProvider.System));
            Assert.Contains("doesn't fit", error.Message);

            // The right key passes
            await DatabaseStartup.CheckKeyAsync(db, Tokens, TimeProvider.System);
        });
    }

    [Fact]
    public Task Invites_stored_in_plain_text_keep_their_links_after_the_migration() => InEmptyDatabaseAsync(async db =>
    {
        await db.GetService<IMigrator>().MigrateAsync("AddStoredFiles");

        var open = (Id: Guid.NewGuid(), Token: "Abc-def_ghiJKLmno12345");
        var used = (Id: Guid.NewGuid(), Token: "zyxWVU-987_tsrqpONML00");
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Invites NOCHECK CONSTRAINT ALL");
        await InsertPlainInviteAsync(db, open.Id, open.Token, now.AddDays(6), usedAt: null);
        await InsertPlainInviteAsync(db, used.Id, used.Token, now.AddDays(6), usedAt: now);

        // The first migration computes the hash in SQL, and it has to be the one the API computes
        await db.GetService<IMigrator>().MigrateAsync("EncryptInviteTokens");
        var sqlHashes = await db.Database
            .SqlQuery<HashRow>($"SELECT Id, TokenHash FROM Invites")
            .ToDictionaryAsync(r => r.Id, r => r.TokenHash);
        Assert.Equal(InviteTokens.Hash(open.Token), sqlHashes[open.Id]);
        Assert.Equal(InviteTokens.Hash(used.Token), sqlHashes[used.Id]);

        await DatabaseStartup.MigrateAsync(db, Tokens, TimeProvider.System);

        foreach (var (id, token) in new[] { open, used })
        {
            var stored = await db.Invites.AsNoTracking().SingleAsync(i => i.Id == id);
            Assert.Equal(InviteTokens.Hash(token), stored.TokenHash);
            Assert.Equal(token, Tokens.Decrypt(id, stored.TokenCiphertext));
        }

        Assert.Equal(0, await PlainColumnsAsync(db));

        // Starting again finds nothing left to do
        await DatabaseStartup.MigrateAsync(db, Tokens, TimeProvider.System);
    });

    [Fact]
    public Task The_plain_column_is_not_dropped_while_a_token_in_it_is_not_encrypted() => InEmptyDatabaseAsync(async db =>
    {
        await db.GetService<IMigrator>().MigrateAsync("AddStoredFiles");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Invites NOCHECK CONSTRAINT ALL");
        var id = Guid.NewGuid();
        await InsertPlainInviteAsync(db, id, "Abc-def_ghiJKLmno12345", DateTimeOffset.UtcNow.AddDays(6), usedAt: null);

        // Every migration at once, without the step that encrypts
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.MigrateAsync());
        Assert.Contains("not encrypted", error.Message);

        var token = await db.Database.SqlQuery<string>($"SELECT Token AS Value FROM Invites WHERE Id = {id}").SingleAsync();
        Assert.Equal("Abc-def_ghiJKLmno12345", token);
    });

    private sealed record HashRow(Guid Id, string TokenHash);

    private InviteTokens Tokens => api.Services.GetRequiredService<InviteTokens>();

    private static InviteTokens TokensWithKey(string key) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [InviteTokens.KeySetting] = key }).Build());

    private static Task<int> PlainColumnsAsync(StiklingDbContext db) =>
        db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Invites' AND COLUMN_NAME = 'Token'")
            .SingleAsync();

    /// <summary>Runs the test in a database of its own on the same server, which is dropped afterwards.</summary>
    private async Task InEmptyDatabaseAsync(Func<StiklingDbContext, Task> test)
    {
        var connection = new SqlConnectionStringBuilder(api.Services.GetRequiredService<IConfiguration>().GetConnectionString("Stikling"))
        {
            InitialCatalog = $"migration_{Guid.NewGuid():N}",
        };
        await using var db = new StiklingDbContext(new DbContextOptionsBuilder<StiklingDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await test(db);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static Task InsertPlainInviteAsync(StiklingDbContext db, Guid id, string token, DateTimeOffset expiresAt, DateTimeOffset? usedAt) =>
        db.Database.ExecuteSqlAsync($"""
            INSERT INTO Invites (Id, Token, CollectionId, Role, ManagesPeople, CreatedAt, ExpiresAt, UsedAt)
            VALUES ({id}, {token}, {Guid.NewGuid()}, 'Viewer', 0, {expiresAt.AddDays(-7)}, {expiresAt}, {usedAt})
            """);
}
