using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stikling.Api.Data;
using Stikling.Api.Photos;
using Stikling.Core.Sync;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class AccountDeletionTests(ApiFactory api)
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private record SignedIn(string ClerkUserId, HttpClient Client, Guid PersonId, Guid CollectionId);

    private async Task<SignedIn> SignIn()
    {
        var clerkUserId = ApiFactory.NewClerkUserId();
        var client = api.ClientFor(clerkUserId);
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        return new SignedIn(clerkUserId, client, me!.PersonId, me.Collections.Single().Id);
    }

    private static async Task<Guid> AddPhotoWithImage(SignedIn me)
    {
        var id = Guid.NewGuid();
        var record = new SyncRecord(SyncKinds.Photos, JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt = Monday, subjectType = "Plant", subjectId = Guid.NewGuid(), width = 1600, height = 1200 },
            ApiFactory.Json));
        var sent = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/records", new PushRequest([record]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        var image = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0x42]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var uploaded = await me.Client.PutAsync($"/collections/{me.CollectionId}/photos/{id}/{PhotoSize.Thumbnail.PathName()}", image);
        Assert.True(uploaded.IsSuccessStatusCode);
        return id;
    }

    private async Task<bool> ImageIsStored(Guid collectionId, Guid photoId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await using var image = await scope.ServiceProvider.GetRequiredService<PhotoStorage>().OpenAsync(collectionId, photoId, PhotoSize.Thumbnail);
        return image is not null;
    }

    [Fact]
    public async Task Deleting_an_account_erases_its_collection_records_photos_and_settings()
    {
        var me = await SignIn();
        var photoId = await AddPhotoWithImage(me);
        var settings = await me.Client.PutAsJsonAsync("/me/settings", new { id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday });
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        var deleted = await me.Client.DeleteAsync("/me");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(await ImageIsStored(me.CollectionId, photoId));
        await api.WithDbAsync(async db =>
        {
            Assert.False(await db.People.AnyAsync(p => p.Id == me.PersonId));
            Assert.False(await db.Collections.AnyAsync(c => c.Id == me.CollectionId));
            Assert.False(await db.Records.AnyAsync(r => r.CollectionId == me.CollectionId));
            Assert.False(await db.PhotoImages.AnyAsync(i => i.CollectionId == me.CollectionId));
            Assert.False(await db.PersonSettings.AnyAsync(s => s.PersonId == me.PersonId));
        });
    }

    [Fact]
    public async Task Deleting_an_account_removes_its_calendar_link()
    {
        var me = await SignIn();
        var made = await me.Client.PostAsync("/me/calendar", null);
        var token = (await made.Content.ReadFromJsonAsync<Stikling.Core.Calendar.CalendarLinkResponse>(ApiFactory.Json))!.Token;

        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync("/me")).StatusCode);

        await api.WithDbAsync(async db => Assert.False(await db.CalendarFeeds.AnyAsync(f => f.PersonId == me.PersonId)));
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateClient().GetAsync($"/calendar/{token}.ics")).StatusCode);
    }

    [Fact]
    public async Task Deleting_an_account_erases_the_share_links_of_its_collection()
    {
        var me = await SignIn();
        await api.WithDbAsync(async db =>
        {
            db.ShareLinks.Add(new ShareLink
            {
                Token = Guid.NewGuid().ToString("N"), CollectionId = me.CollectionId, SubjectType = Stikling.Core.Models.SubjectType.Plant,
                SubjectId = Guid.NewGuid(), CreatedBy = me.PersonId, CreatedAt = Monday, UpdatedAt = Monday,
            });
            await db.SaveChangesAsync();
        });

        await me.Client.DeleteAsync("/me");

        await api.WithDbAsync(async db => Assert.False(await db.ShareLinks.AnyAsync(l => l.CollectionId == me.CollectionId)));
    }

    [Fact]
    public async Task A_share_link_in_a_collection_that_lives_on_keeps_its_page_when_its_maker_is_deleted()
    {
        var me = await SignIn();
        var other = await SignIn();
        await api.WithDbAsync(async db =>
        {
            db.Memberships.Add(new Membership { CollectionId = me.CollectionId, PersonId = other.PersonId, Role = MemberRole.Editor });
            db.ShareLinks.Add(new ShareLink
            {
                Token = Guid.NewGuid().ToString("N"), CollectionId = me.CollectionId, SubjectType = Stikling.Core.Models.SubjectType.Plant,
                SubjectId = Guid.NewGuid(), CreatedBy = me.PersonId, CreatedAt = Monday, UpdatedAt = Monday,
            });
            await db.SaveChangesAsync();
        });

        await me.Client.DeleteAsync("/me");

        await api.WithDbAsync(async db =>
        {
            var link = await db.ShareLinks.SingleAsync(l => l.CollectionId == me.CollectionId);
            Assert.Null(link.CreatedBy);
        });
    }

    [Fact]
    public async Task A_deleted_account_is_not_made_again_by_a_device_still_signed_in()
    {
        var me = await SignIn();
        await me.Client.DeleteAsync("/me");

        var again = await me.Client.GetAsync("/me");
        var settings = await me.Client.GetAsync("/me/settings");

        Assert.Equal(HttpStatusCode.Gone, again.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, settings.StatusCode);
        await api.WithDbAsync(async db => Assert.False(await db.People.AnyAsync(p => p.ClerkUserId == me.ClerkUserId)));
    }

    [Fact]
    public async Task The_app_in_a_browser_can_read_that_the_account_is_gone()
    {
        var me = await SignIn();
        await me.Client.DeleteAsync("/me");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        request.Headers.Add("Origin", ApiFactory.AppOrigin);
        var response = await me.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(ApiFactory.AppOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task A_device_still_signed_in_can_no_longer_reach_the_collection()
    {
        var me = await SignIn();
        var otherDevice = api.ClientFor(me.ClerkUserId);
        await me.Client.DeleteAsync("/me");

        var pull = await otherDevice.GetAsync($"/collections/{me.CollectionId}/records?after=0");

        Assert.Equal(HttpStatusCode.Forbidden, pull.StatusCode);
    }

    [Fact]
    public async Task A_collection_shared_with_others_stays_and_only_loses_the_person()
    {
        var owner = await SignIn();
        var sitter = await SignIn();
        var photoId = await AddPhotoWithImage(owner);
        await api.WithDbAsync(async db =>
        {
            db.Memberships.Add(new Membership { CollectionId = owner.CollectionId, PersonId = sitter.PersonId, Role = MemberRole.Viewer });
            await db.SaveChangesAsync();
        });

        await sitter.Client.DeleteAsync("/me");

        Assert.True(await ImageIsStored(owner.CollectionId, photoId));
        await api.WithDbAsync(async db =>
        {
            Assert.True(await db.Records.AnyAsync(r => r.CollectionId == owner.CollectionId));
            Assert.Equal([owner.PersonId], await db.Memberships.Where(m => m.CollectionId == owner.CollectionId).Select(m => m.PersonId).ToListAsync());
            Assert.False(await db.Collections.AnyAsync(c => c.Id == sitter.CollectionId));
        });
    }

    private async Task<(Guid? OwnerId, Membership Member)> OwnerAfterDeleting(Who owner, Who member)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync("/me")).StatusCode);
        Guid? ownerId = null;
        Membership? found = null;
        await api.WithDbAsync(async db =>
        {
            ownerId = (await db.Collections.SingleAsync(c => c.Id == owner.CollectionId)).OwnerPersonId;
            found = await db.Memberships.SingleAsync(m => m.CollectionId == owner.CollectionId && m.PersonId == member.PersonId);
        });
        return (ownerId, found!);
    }

    [Fact]
    public async Task An_owner_who_is_deleted_hands_a_shared_collection_to_the_oldest_editor_who_manages_people()
    {
        var people = new PeopleSupport(api);
        var owner = await people.SignIn();
        await people.Join(owner, MemberRole.Viewer);
        await people.Join(owner, MemberRole.Editor);
        var manager = await people.Join(owner, MemberRole.Editor, managesPeople: true);
        await people.Join(owner, MemberRole.Editor, managesPeople: true);

        var (ownerId, member) = await OwnerAfterDeleting(owner, manager);

        Assert.Equal(manager.PersonId, ownerId);
        Assert.Equal(MemberRole.Editor, member.Role);
        Assert.True(member.ManagesPeople);
    }

    [Fact]
    public async Task Without_a_manager_it_goes_to_the_oldest_editor()
    {
        var people = new PeopleSupport(api);
        var owner = await people.SignIn();
        await people.Join(owner, MemberRole.Viewer);
        await people.Join(owner, MemberRole.Helper);
        var editor = await people.Join(owner, MemberRole.Editor);
        await people.Join(owner, MemberRole.Editor);

        var (ownerId, member) = await OwnerAfterDeleting(owner, editor);

        Assert.Equal(editor.PersonId, ownerId);
        Assert.Equal(MemberRole.Editor, member.Role);
        Assert.True(member.ManagesPeople);
    }

    [Fact]
    public async Task Without_an_editor_it_goes_to_the_oldest_member_who_becomes_an_editor()
    {
        var people = new PeopleSupport(api);
        var owner = await people.SignIn();
        var oldest = await people.Join(owner, MemberRole.Helper);
        await people.Join(owner, MemberRole.Viewer);

        var (ownerId, member) = await OwnerAfterDeleting(owner, oldest);

        Assert.Equal(oldest.PersonId, ownerId);
        Assert.Equal(MemberRole.Editor, member.Role);
        Assert.True(member.ManagesPeople);
    }

    [Fact]
    public async Task The_others_keep_what_a_deleted_owner_and_a_deleted_member_made()
    {
        var people = new PeopleSupport(api);
        var owner = await people.SignIn();
        var helper = await people.Join(owner, MemberRole.Helper);
        var editor = await people.Join(owner, MemberRole.Editor);
        var plant = Guid.NewGuid();
        var careLog = Guid.NewGuid();
        await people.PushOk(owner, PeopleSupport.Plant(plant, Monday));
        await people.PushOk(helper, PeopleSupport.CareLog(careLog, Monday));

        await helper.Client.DeleteAsync("/me");
        await owner.Client.DeleteAsync("/me");

        Assert.Equal(HttpStatusCode.OK, (await editor.Client.GetAsync($"/collections/{owner.CollectionId}/records")).StatusCode);
        await api.WithDbAsync(async db =>
        {
            Assert.Equal(2, await db.Records.CountAsync(r => r.CollectionId == owner.CollectionId));
            Assert.Null((await db.Records.SingleAsync(r => r.Id == plant)).CreatedBy);
            Assert.Null((await db.Records.SingleAsync(r => r.Id == careLog)).CreatedBy);
            Assert.Equal(editor.PersonId, (await db.Collections.SingleAsync(c => c.Id == owner.CollectionId)).OwnerPersonId);        });
    }

    [Fact]
    public async Task Invites_used_by_a_deleted_person_forget_who_used_them()
    {
        var people = new PeopleSupport(api);
        var owner = await people.SignIn();
        var invite = await people.InviteOk(owner, MemberRole.Viewer);
        var guest = await people.SignIn();
        await people.Accept(guest, invite.Token!);

        await guest.Client.DeleteAsync("/me");

        await api.WithDbAsync(async db =>
        {
            var stored = await db.Invites.SingleAsync(i => i.Id == invite.Id);
            Assert.Null(stored.UsedBy);
            Assert.NotNull(stored.UsedAt);
        });
    }

    [Fact]
    public async Task A_deleted_person_is_gone_from_the_former_members_and_the_invites_they_made_lose_their_name()
    {
        var people = new PeopleSupport(api);
        var owner = await people.SignIn();
        var manager = await people.Join(owner, MemberRole.Editor, managesPeople: true);
        var guest = await people.Join(manager, MemberRole.Viewer);
        await people.Leave(guest);
        var invite = await people.InviteOk(manager, MemberRole.Viewer);

        await manager.Client.DeleteAsync("/me");
        await guest.Client.DeleteAsync("/me");

        await api.WithDbAsync(async db =>
        {
            Assert.Null((await db.Invites.SingleAsync(i => i.Id == invite.Id)).CreatedBy);
            Assert.False(await db.FormerMembers.AnyAsync(f => f.PersonId == guest.PersonId));
        });
    }

    [Fact]
    public async Task Asking_to_delete_again_is_fine()
    {
        var me = await SignIn();

        var first = await me.Client.DeleteAsync("/me");
        var second = await me.Client.DeleteAsync("/me");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Deleting_needs_a_sign_in()
    {
        var response = await api.CreateClient().DeleteAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
