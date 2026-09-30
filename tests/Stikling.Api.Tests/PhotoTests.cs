using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Stikling.Api.Data;
using Stikling.Core.Sync;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class PhotoTests(ApiFactory api)
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private record SignedIn(HttpClient Client, Guid PersonId, Guid CollectionId);

    private async Task<SignedIn> SignIn()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        return new SignedIn(client, me!.PersonId, me.Collections.Single().Id);
    }

    private Task AddMember(Guid collectionId, Guid personId, MemberRole role) =>
        api.WithDbAsync(async db =>
        {
            db.Memberships.Add(new Membership { CollectionId = collectionId, PersonId = personId, Role = role });
            await db.SaveChangesAsync();
        });

    private static SyncRecord PhotoRecord(Guid id, DateTimeOffset updatedAt, DateTimeOffset? deletedAt = null) =>
        new(SyncKinds.Photos, JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt, deletedAt, subjectType = "Plant", subjectId = Guid.NewGuid(), width = 1600, height = 1200 },
            ApiFactory.Json));

    private static async Task SendRecord(SignedIn me, SyncRecord record)
    {
        var response = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/records", new PushRequest([record]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> AddPhoto(SignedIn me)
    {
        var id = Guid.NewGuid();
        await SendRecord(me, PhotoRecord(id, Monday));
        return id;
    }

    /// <summary>Bytes that start like a JPEG, which is what the server checks.</summary>
    private static byte[] Jpeg(int length, byte fill = 0x42)
    {
        var bytes = Enumerable.Repeat(fill, length).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    /// <summary>Bytes that start like a WebP file.</summary>
    private static byte[] WebP(int length)
    {
        var bytes = Enumerable.Repeat((byte)0x42, length).ToArray();
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        return bytes;
    }

    private static Task<HttpResponseMessage> Upload(SignedIn me, Guid photoId, string size, byte[] image)
    {
        var content = new ByteArrayContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return me.Client.PutAsync($"/collections/{me.CollectionId}/photos/{photoId}/{size}", content);
    }

    private static async Task<List<StoredPhoto>> Stored(SignedIn me, params Guid[] ids)
    {
        var response = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/photos/stored", new StoredPhotosRequest([.. ids]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StoredPhotosResponse>(ApiFactory.Json))!.Photos;
    }

    [Fact]
    public async Task An_image_sent_from_one_device_can_be_fetched_by_another()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);
        var image = Jpeg(1000);

        Assert.Equal(HttpStatusCode.NoContent, (await Upload(me, id, "full", image)).StatusCode);
        var fetched = await me.Client.GetAsync($"/collections/{me.CollectionId}/photos/{id}/full");

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("image/jpeg", fetched.Content.Headers.ContentType?.MediaType);
        Assert.Equal(image, await fetched.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Stored_says_which_sizes_are_there()
    {
        var me = await SignIn();
        var both = await AddPhoto(me);
        var thumbOnly = await AddPhoto(me);
        var none = await AddPhoto(me);
        await Upload(me, both, "full", Jpeg(100));
        await Upload(me, both, "thumb", Jpeg(10));
        await Upload(me, thumbOnly, "thumb", Jpeg(10));

        var stored = await Stored(me, both, thumbOnly, none);

        Assert.Equal(2, stored.Count);
        Assert.Contains(new StoredPhoto(both, Full: true, Thumbnail: true), stored);
        Assert.Contains(new StoredPhoto(thumbOnly, Full: false, Thumbnail: true), stored);
    }

    [Fact]
    public async Task An_image_waits_for_its_photo_record()
    {
        var me = await SignIn();

        var response = await Upload(me, Guid.NewGuid(), "full", Jpeg(100));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_deleted_photo_takes_no_images()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();
        await SendRecord(me, PhotoRecord(id, Monday, deletedAt: Monday));

        var response = await Upload(me, id, "full", Jpeg(100));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_photo_removes_its_images()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);
        await Upload(me, id, "full", Jpeg(100));
        await Upload(me, id, "thumb", Jpeg(10));

        await SendRecord(me, PhotoRecord(id, Monday.AddHours(1), deletedAt: Monday.AddHours(1)));

        Assert.Empty(await Stored(me, id));
        Assert.Equal(HttpStatusCode.NotFound, (await me.Client.GetAsync($"/collections/{me.CollectionId}/photos/{id}/full")).StatusCode);
        var usage = await me.Client.GetFromJsonAsync<PhotoUsage>($"/collections/{me.CollectionId}/photos/usage", ApiFactory.Json);
        Assert.Equal(0, usage!.UsedBytes);
    }

    [Fact]
    public async Task An_older_delete_that_loses_keeps_the_images()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();
        await SendRecord(me, PhotoRecord(id, Monday.AddHours(2)));
        await Upload(me, id, "full", Jpeg(100));

        await SendRecord(me, PhotoRecord(id, Monday.AddHours(1), deletedAt: Monday.AddHours(1)));

        Assert.Single(await Stored(me, id));
    }

    [Fact]
    public async Task A_full_collection_turns_images_away()
    {
        var me = await SignIn();
        var first = await AddPhoto(me);
        var second = await AddPhoto(me);
        Assert.Equal(HttpStatusCode.NoContent, (await Upload(me, first, "full", Jpeg((int)ApiFactory.PhotoLimit - 100))).StatusCode);

        var response = await Upload(me, second, "full", Jpeg(200));

        Assert.Equal(HttpStatusCode.InsufficientStorage, response.StatusCode);
        Assert.Empty(await Stored(me, second));
    }

    [Fact]
    public async Task Sending_an_image_again_counts_it_once()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);
        var image = Jpeg((int)ApiFactory.PhotoLimit - 100);

        await Upload(me, id, "full", image);
        var again = await Upload(me, id, "full", image);

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        var usage = await me.Client.GetFromJsonAsync<PhotoUsage>($"/collections/{me.CollectionId}/photos/usage", ApiFactory.Json);
        Assert.Equal(image.Length, usage!.UsedBytes);
        Assert.Equal(ApiFactory.PhotoLimit, usage.LimitBytes);
    }

    [Fact]
    public async Task A_webp_is_taken_and_comes_back_as_a_webp()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);
        var image = WebP(1000);

        var response = await Upload(me, id, "full", image);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var fetched = await me.Client.GetAsync($"/collections/{me.CollectionId}/photos/{id}/full");
        Assert.Equal("image/webp", fetched.Content.Headers.ContentType?.MediaType);
        Assert.Equal(image, await fetched.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task A_jpeg_comes_back_as_a_jpeg()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);
        await Upload(me, id, "thumb", Jpeg(500));

        var fetched = await me.Client.GetAsync($"/collections/{me.CollectionId}/photos/{id}/thumb");

        Assert.Equal("image/jpeg", fetched.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_png_is_turned_away()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        var response = await Upload(me, id, "full", png);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Only_webps_and_jpegs_are_taken()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);

        var response = await Upload(me, id, "full", "<html>not a photo</html>"u8.ToArray());

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task An_image_larger_than_the_limit_is_turned_away()
    {
        var me = await SignIn();
        var id = await AddPhoto(me);

        var response = await Upload(me, id, "full", Jpeg(PhotoRules.MaxImageBytes + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task A_viewer_can_fetch_images_but_not_send_them()
    {
        var owner = await SignIn();
        var id = await AddPhoto(owner);
        await Upload(owner, id, "full", Jpeg(100));
        var viewer = await SignIn();
        await AddMember(owner.CollectionId, viewer.PersonId, MemberRole.Viewer);
        var asViewer = viewer with { CollectionId = owner.CollectionId };

        var fetched = await asViewer.Client.GetAsync($"/collections/{owner.CollectionId}/photos/{id}/full");
        var sent = await Upload(asViewer, id, "thumb", Jpeg(10));

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sent.StatusCode);
    }

    [Fact]
    public async Task Someone_outside_the_collection_gets_nothing()
    {
        var owner = await SignIn();
        var id = await AddPhoto(owner);
        await Upload(owner, id, "full", Jpeg(100));
        var stranger = await SignIn();

        var fetched = await stranger.Client.GetAsync($"/collections/{owner.CollectionId}/photos/{id}/full");
        var stored = await stranger.Client.PostAsJsonAsync($"/collections/{owner.CollectionId}/photos/stored", new StoredPhotosRequest([id]), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Forbidden, fetched.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, stored.StatusCode);
    }

    [Fact]
    public async Task The_same_photo_id_in_another_collection_is_a_different_photo()
    {
        var me = await SignIn();
        var other = await SignIn();
        var id = Guid.NewGuid();
        await SendRecord(me, PhotoRecord(id, Monday));
        await SendRecord(other, PhotoRecord(id, Monday));
        await Upload(me, id, "full", Jpeg(100, fill: 1));

        Assert.Empty(await Stored(other, id));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/collections/{other.CollectionId}/photos/{id}/full")).StatusCode);
    }
}
