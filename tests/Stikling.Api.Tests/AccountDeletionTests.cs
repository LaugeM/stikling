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
