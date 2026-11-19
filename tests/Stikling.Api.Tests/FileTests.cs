using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stikling.Api.Data;
using Stikling.Api.Files;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Sync;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

/// <summary>The bytes of attached files: sending, fetching, the shared space, who may, and what erases them.</summary>
[Collection(ApiCollection.Name)]
public class FileTests(ApiFactory api)
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private record SignedIn(HttpClient Client, Guid PersonId, Guid CollectionId);

    private async Task<SignedIn> SignIn()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        return new SignedIn(client, me!.PersonId, me.Collections.Single().Id);
    }

    private static SignedIn As(Who who) => new(who.Client, who.PersonId, who.CollectionId);

    private static SyncRecord FileRecord(Guid id, DateTimeOffset updatedAt, DateTimeOffset? deletedAt = null, Guid? subjectId = null, string name = "Receipt.pdf") =>
        new(SyncKinds.Files, JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt, deletedAt, subjectType = "Plant", subjectId = subjectId ?? Guid.NewGuid(), name, contentType = "application/pdf", size = 1000, addedAt = Monday },
            ApiFactory.Json));

    private static async Task<PushResponse> SendRecord(SignedIn me, SyncRecord record)
    {
        var response = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/records", new PushRequest([record]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PushResponse>(ApiFactory.Json))!;
    }

    private static async Task<Guid> AddFile(SignedIn me, Guid? subjectId = null, string name = "Receipt.pdf")
    {
        var id = Guid.NewGuid();
        Assert.Empty((await SendRecord(me, FileRecord(id, Monday, subjectId: subjectId, name: name))).Refused);
        return id;
    }

    private static byte[] Pdf(int length)
    {
        var bytes = Enumerable.Repeat((byte)0x42, length).ToArray();
        "%PDF-1.7"u8.CopyTo(bytes);
        return bytes;
    }

    private static byte[] Jpeg(int length)
    {
        var bytes = Enumerable.Repeat((byte)0x42, length).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static byte[] WebP(int length)
    {
        var bytes = Enumerable.Repeat((byte)0x42, length).ToArray();
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        return bytes;
    }

    private static Task<HttpResponseMessage> Upload(SignedIn me, Guid fileId, byte[] bytes, string type = "application/pdf")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return me.Client.PutAsync($"/collections/{me.CollectionId}/files/{fileId}", content);
    }

    private static async Task<List<Guid>> Stored(SignedIn me, params Guid[] ids)
    {
        var response = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/files/stored", new StoredFilesRequest([.. ids]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StoredFilesResponse>(ApiFactory.Json))!.Files;
    }

    private static Task<HttpResponseMessage> Fetch(SignedIn me, Guid fileId) =>
        me.Client.GetAsync($"/collections/{me.CollectionId}/files/{fileId}");

    private static Task<PhotoUsage?> Usage(SignedIn me) =>
        me.Client.GetFromJsonAsync<PhotoUsage>($"/collections/{me.CollectionId}/photos/usage", ApiFactory.Json);

    private async Task<bool> BytesAreStored(Guid collectionId, Guid fileId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await using var bytes = await scope.ServiceProvider.GetRequiredService<FileStorage>().OpenAsync(collectionId, fileId);
        return bytes is not null;
    }

    // Sending and fetching

    [Fact]
    public async Task A_pdf_sent_from_one_device_can_be_fetched_by_another()
    {
        var me = await SignIn();
        var id = await AddFile(me);
        var pdf = Pdf(3000);

        Assert.Equal(HttpStatusCode.NoContent, (await Upload(me, id, pdf)).StatusCode);
        var fetched = await Fetch(me, id);

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("application/pdf", fetched.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdf, await fetched.Content.ReadAsByteArrayAsync());
        // Opened straight from the API, a PDF is downloaded instead of shown there
        Assert.Equal("attachment", fetched.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", Assert.Single(fetched.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData("webp", "image/webp")]
    [InlineData("jpeg", "image/jpeg")]
    public async Task An_image_comes_back_with_the_type_its_bytes_have(string kind, string type)
    {
        var me = await SignIn();
        var id = await AddFile(me);
        var image = kind == "webp" ? WebP(500) : Jpeg(500);

        // Whatever the sender says it is, the bytes decide
        Assert.Equal(HttpStatusCode.NoContent, (await Upload(me, id, image, "application/octet-stream")).StatusCode);
        var fetched = await Fetch(me, id);

        Assert.Equal(type, fetched.Content.Headers.ContentType?.MediaType);
        Assert.Equal(image, await fetched.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task A_file_that_was_never_sent_is_not_found()
    {
        var me = await SignIn();
        var id = await AddFile(me);

        Assert.Equal(HttpStatusCode.NotFound, (await Fetch(me, id)).StatusCode);
    }

    [Fact]
    public async Task Sending_a_file_again_replaces_it_and_counts_once()
    {
        var me = await SignIn();
        var id = await AddFile(me);
        await Upload(me, id, Pdf(1000));

        var again = await Upload(me, id, Pdf(2000));

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(2000, (await Usage(me))!.UsedBytes);
        Assert.Equal(2000, (await (await Fetch(me, id)).Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Stored_says_which_files_have_their_bytes()
    {
        var me = await SignIn();
        var there = await AddFile(me);
        var notThere = await AddFile(me);
        await Upload(me, there, Pdf(100));

        Assert.Equal([there], await Stored(me, there, notThere, Guid.NewGuid()));
    }

    [Fact]
    public async Task Stored_takes_at_most_a_batch()
    {
        var me = await SignIn();

        var response = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/files/stored",
            new StoredFilesRequest([.. Enumerable.Range(0, FileRules.BatchSize + 1).Select(_ => Guid.NewGuid())]), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bytes_wait_for_their_record_and_a_deleted_record_takes_none()
    {
        var me = await SignIn();
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(me, Guid.NewGuid(), Pdf(100))).StatusCode);

        var deleted = Guid.NewGuid();
        await SendRecord(me, FileRecord(deleted, Monday, deletedAt: Monday));

        Assert.Equal(HttpStatusCode.Gone, (await Upload(me, deleted, Pdf(100))).StatusCode);
    }

    [Fact]
    public async Task The_same_file_id_in_another_collection_is_a_different_file()
    {
        var me = await SignIn();
        var other = await SignIn();
        var id = Guid.NewGuid();
        await SendRecord(me, FileRecord(id, Monday));
        await SendRecord(other, FileRecord(id, Monday));
        await Upload(me, id, Pdf(100));

        Assert.Empty(await Stored(other, id));
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch(other, id)).StatusCode);
    }

    // What is taken

    [Fact]
    public async Task A_file_over_ten_megabytes_is_turned_away()
    {
        var me = await SignIn();
        var id = await AddFile(me);

        var response = await Upload(me, id, Pdf(FileRules.MaxFileBytes + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(await Stored(me, id));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("html")]
    [InlineData("pdf inside html")]
    [InlineData("empty")]
    public async Task Only_pdfs_webps_and_jpegs_are_taken(string kind)
    {
        var me = await SignIn();
        var id = await AddFile(me);
        byte[] bytes = kind switch
        {
            "png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0],
            "html" => "<html>not a file</html>"u8.ToArray(),
            "pdf inside html" => "<html>%PDF-1.7</html>"u8.ToArray(),
            _ => [],
        };

        var response = await Upload(me, id, bytes, "application/pdf");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Empty(await Stored(me, id));
    }

    // The space, which photos and files share

    [Fact]
    public async Task Photos_and_files_count_towards_the_same_space()
    {
        var me = await SignIn();
        var photo = Guid.NewGuid();
        await SendRecord(me, new SyncRecord(SyncKinds.Photos, JsonSerializer.SerializeToElement(
            new { id = photo, createdAt = Monday, updatedAt = Monday, subjectType = "Plant", subjectId = Guid.NewGuid(), width = 1600, height = 1200 }, ApiFactory.Json)));
        var photoImage = new ByteArrayContent(Jpeg((int)ApiFactory.PhotoLimit - 1000));
        photoImage.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.PutAsync($"/collections/{me.CollectionId}/photos/{photo}/full", photoImage)).StatusCode);

        var fits = await AddFile(me);
        var tooMuch = await AddFile(me);

        Assert.Equal(HttpStatusCode.NoContent, (await Upload(me, fits, Pdf(600))).StatusCode);
        var full = await Upload(me, tooMuch, Pdf(600));

        Assert.Equal(HttpStatusCode.InsufficientStorage, full.StatusCode);
        Assert.Empty(await Stored(me, tooMuch));
        var usage = await Usage(me);
        Assert.Equal(ApiFactory.PhotoLimit - 1000 + 600, usage!.UsedBytes);
        Assert.Equal(ApiFactory.PhotoLimit, usage.LimitBytes);
    }

    [Fact]
    public async Task Files_use_up_space_a_photo_would_need()
    {
        var me = await SignIn();
        var id = await AddFile(me);
        await Upload(me, id, Pdf((int)ApiFactory.PhotoLimit - 100));
        var photo = Guid.NewGuid();
        await SendRecord(me, new SyncRecord(SyncKinds.Photos, JsonSerializer.SerializeToElement(
            new { id = photo, createdAt = Monday, updatedAt = Monday, subjectType = "Plant", subjectId = Guid.NewGuid(), width = 1600, height = 1200 }, ApiFactory.Json)));
        var image = new ByteArrayContent(Jpeg(200));
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        var response = await me.Client.PutAsync($"/collections/{me.CollectionId}/photos/{photo}/full", image);

        Assert.Equal(HttpStatusCode.InsufficientStorage, response.StatusCode);
    }

    // Who may

    [Fact]
    public async Task A_viewer_can_fetch_a_file_but_not_send_one()
    {
        var people = new PeopleSupport(api);
        var ownerWho = await people.SignIn();
        var owner = As(ownerWho);
        var id = await AddFile(owner);
        await Upload(owner, id, Pdf(100));
        var viewer = As(await people.Join(ownerWho, MemberRole.Viewer));

        var fetched = await Fetch(viewer, id);
        var sent = await Upload(viewer, id, Pdf(10));
        var stored = await Stored(viewer, id);

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sent.StatusCode);
        Assert.Equal([id], stored);
    }

    [Fact]
    public async Task A_helper_can_fetch_a_file_but_not_send_one_or_add_a_record()
    {
        var people = new PeopleSupport(api);
        var ownerWho = await people.SignIn();
        var owner = As(ownerWho);
        var id = await AddFile(owner);
        await Upload(owner, id, Pdf(100));
        var helper = As(await people.Join(ownerWho, MemberRole.Helper));

        var fetched = await Fetch(helper, id);
        var sent = await Upload(helper, id, Pdf(10));
        var ownRecord = Guid.NewGuid();
        var pushed = await SendRecord(helper, FileRecord(ownRecord, Monday));
        var edited = await SendRecord(helper, FileRecord(id, Monday.AddHours(1), deletedAt: Monday.AddHours(1)));

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sent.StatusCode);
        Assert.Single(pushed.Refused);
        Assert.Single(edited.Refused);

        // Nothing of it reached the collection, so the owner's file and bytes are as they were
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(owner, ownRecord, Pdf(10))).StatusCode);
        Assert.Single(await Stored(owner, id));
    }

    [Fact]
    public async Task Someone_outside_the_collection_gets_nothing()
    {
        var owner = await SignIn();
        var id = await AddFile(owner);
        await Upload(owner, id, Pdf(100));
        var stranger = await SignIn();
        var asStranger = stranger with { CollectionId = owner.CollectionId };

        Assert.Equal(HttpStatusCode.Forbidden, (await Fetch(asStranger, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(asStranger, id, Pdf(10))).StatusCode);
        var stored = await stranger.Client.PostAsJsonAsync($"/collections/{owner.CollectionId}/files/stored", new StoredFilesRequest([id]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Forbidden, stored.StatusCode);
    }

    // What takes them away

    [Fact]
    public async Task A_file_record_that_arrives_deleted_removes_its_bytes_and_its_space()
    {
        var me = await SignIn();
        var id = await AddFile(me);
        await Upload(me, id, Pdf(500));
        Assert.True(await BytesAreStored(me.CollectionId, id));

        await SendRecord(me, FileRecord(id, Monday.AddHours(1), deletedAt: Monday.AddHours(1)));

        Assert.Empty(await Stored(me, id));
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch(me, id)).StatusCode);
        Assert.False(await BytesAreStored(me.CollectionId, id));
        Assert.Equal(0, (await Usage(me))!.UsedBytes);
    }

    [Fact]
    public async Task An_older_delete_that_loses_keeps_the_bytes()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();
        await SendRecord(me, FileRecord(id, Monday.AddHours(2)));
        await Upload(me, id, Pdf(100));

        await SendRecord(me, FileRecord(id, Monday.AddHours(1), deletedAt: Monday.AddHours(1)));

        Assert.Single(await Stored(me, id));
        Assert.True(await BytesAreStored(me.CollectionId, id));
    }

    [Fact]
    public async Task Deleting_an_account_leaves_no_file_bytes()
    {
        var me = await SignIn();
        var id = await AddFile(me);
        await Upload(me, id, Pdf(500));
        Assert.True(await BytesAreStored(me.CollectionId, id));

        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync("/me")).StatusCode);

        Assert.False(await BytesAreStored(me.CollectionId, id));
        await api.WithDbAsync(async db => Assert.False(await db.StoredFiles.AnyAsync(f => f.CollectionId == me.CollectionId)));
    }

    // Who can see them

    [Fact]
    public async Task A_share_link_never_serves_or_lists_a_file()
    {
        var me = await SignIn();
        var plant = Guid.NewGuid();
        await SendRecord(me, new SyncRecord("plants", JsonSerializer.SerializeToElement(
            new { id = plant, createdAt = Monday, updatedAt = Monday, nickname = "Mona", genus = "monstera" }, ApiFactory.Json)));
        var id = await AddFile(me, plant, name: "Secret receipt.pdf");
        await Upload(me, id, Pdf(500));
        var created = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/shares",
            new CreateShareLinkRequest(SubjectType.Plant, plant, "Europe/Copenhagen"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var link = (await created.Content.ReadFromJsonAsync<ShareLinkInfo>(ApiFactory.Json))!;
        var token = link.Url[(link.Url.LastIndexOf('/') + 1)..];
        var anonymous = api.CreateClient();

        var page = await anonymous.GetAsync($"/s/{token}");
        var html = await page.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Mona", html);
        Assert.DoesNotContain("Secret receipt", html);
        Assert.DoesNotContain(id.ToString(), html);
        foreach (var size in new[] { "full", "thumb" })
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/s/{token}/photos/{id}/{size}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/s/{token}/files/{id}")).StatusCode);

        // The ordinary file address needs a sign-in
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/collections/{me.CollectionId}/files/{id}")).StatusCode);
    }

    [Fact]
    public async Task The_data_export_has_the_files_and_their_bytes()
    {
        var me = await SignIn();
        var id = await AddFile(me, name: "Tag.pdf");
        var pdf = Pdf(700);
        await Upload(me, id, pdf);

        var response = await me.Client.PostAsync("/me/export", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);

        var records = zip.GetEntry($"collections/{me.CollectionId}/files.json");
        Assert.NotNull(records);
        await using (var stream = records.Open())
            Assert.Contains("Tag.pdf", await new StreamReader(stream, Encoding.UTF8).ReadToEndAsync());

        var bytes = zip.GetEntry($"collections/{me.CollectionId}/files/{id}.pdf");
        Assert.NotNull(bytes);
        using var memory = new MemoryStream();
        await using (var stream = bytes.Open())
            await stream.CopyToAsync(memory);
        Assert.Equal(pdf, memory.ToArray());
    }
}
