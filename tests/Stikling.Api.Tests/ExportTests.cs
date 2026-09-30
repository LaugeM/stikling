using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Stikling.Core.Sync;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class ExportTests(ApiFactory api)
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private record SignedIn(HttpClient Client, Guid PersonId, Guid CollectionId);

    private async Task<SignedIn> SignIn()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        return new SignedIn(client, me!.PersonId, me.Collections.Single().Id);
    }

    private static SyncRecord Record(string kind, Guid id, string nickname, DateTimeOffset? deletedAt = null) =>
        new(kind, JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt = Monday, deletedAt, nickname }, ApiFactory.Json));

    private static async Task Push(SignedIn me, params SyncRecord[] records)
    {
        var response = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/records", new PushRequest([.. records]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static byte[] Jpeg(int length)
    {
        var bytes = Enumerable.Repeat((byte)0x42, length).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static async Task Upload(SignedIn me, Guid photoId, string size, byte[] image)
    {
        var content = new ByteArrayContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var response = await me.Client.PutAsync($"/collections/{me.CollectionId}/photos/{photoId}/{size}", content);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<ZipArchive> Export(SignedIn me, string? body = null)
    {
        var response = await me.Client.PostAsync("/me/export",
            body is null ? null : new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("stikling-data-", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        return new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
    }

    private static byte[] Bytes(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        Assert.True(entry is not null, $"{name} is not in the ZIP. It has: {string.Join(", ", zip.Entries.Select(e => e.FullName))}");
        using var stream = entry.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static JsonElement Read(ZipArchive zip, string name) => JsonSerializer.Deserialize<JsonElement>(Bytes(zip, name));

    [Fact]
    public async Task It_needs_a_token()
    {
        var response = await api.CreateClient().PostAsync("/me/export", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Someone_who_never_called_me_is_not_found_and_is_not_created()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var response = await client.PostAsync("/me/export", null);
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(Guid.Empty, me!.PersonId);
    }

    [Fact]
    public async Task The_zip_has_the_person_their_collection_settings_records_and_photos()
    {
        var me = await SignIn();
        var live = Guid.NewGuid();
        var gone = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        await Push(me, Record("plants", live, "Monstera"), Record("plants", gone, "Ficus", deletedAt: Monday),
            new SyncRecord(SyncKinds.Photos, JsonSerializer.SerializeToElement(
                new { id = photoId, createdAt = Monday, updatedAt = Monday, deletedAt = (DateTimeOffset?)null, subjectType = "Plant", subjectId = live }, ApiFactory.Json)));
        var full = Jpeg(500);
        var thumb = Jpeg(50);
        await Upload(me, photoId, "full", full);
        await Upload(me, photoId, "thumb", thumb);
        var put = await me.Client.PutAsJsonAsync("/me/settings",
            new { id = Guid.NewGuid(), updatedAt = Monday, theme = "Dark" }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var zip = await Export(me);

        Assert.Contains("Stikling", Encoding.UTF8.GetString(Bytes(zip, "README.txt")));
        Assert.Equal(me.PersonId, Read(zip, "person.json").GetProperty("id").GetGuid());
        var collection = Assert.Single(Read(zip, "collections.json").EnumerateArray());
        Assert.Equal(me.CollectionId, collection.GetProperty("id").GetGuid());
        Assert.Equal("Editor", collection.GetProperty("role").GetString());
        Assert.Equal("Dark", Read(zip, "settings.json").GetProperty("theme").GetString());

        var plants = Read(zip, $"collections/{me.CollectionId}/plants.json").EnumerateArray().ToList();
        Assert.Equal(2, plants.Count);
        Assert.Equal("Monstera", plants.Single(p => p.GetProperty("id").GetGuid() == live).GetProperty("nickname").GetString());
        Assert.NotEqual(JsonValueKind.Null, plants.Single(p => p.GetProperty("id").GetGuid() == gone).GetProperty("deletedAt").ValueKind);
        Assert.Single(Read(zip, $"collections/{me.CollectionId}/photos.json").EnumerateArray());

        Assert.Equal(full, Bytes(zip, $"collections/{me.CollectionId}/photos/{photoId}.jpg"));
        Assert.Equal(thumb, Bytes(zip, $"collections/{me.CollectionId}/photos/{photoId}-small.jpg"));
    }

    [Fact]
    public async Task Settings_are_left_out_when_there_are_none()
    {
        var me = await SignIn();

        using var zip = await Export(me);

        Assert.Null(zip.GetEntry("settings.json"));
    }

    [Fact]
    public async Task The_account_details_from_the_body_are_in_the_zip()
    {
        var me = await SignIn();

        using var zip = await Export(me, """{ "account": { "email": "a@example.com", "firstName": "Ada" } }""");

        var account = Read(zip, "account.json");
        Assert.Equal("a@example.com", account.GetProperty("email").GetString());
        Assert.Equal("Ada", account.GetProperty("firstName").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("""{ "account": "text" }""")]
    public async Task Without_account_details_there_is_no_account_file(string? body)
    {
        var me = await SignIn();

        using var zip = await Export(me, body);

        Assert.Null(zip.GetEntry("account.json"));
    }

    [Fact]
    public async Task Another_persons_collection_records_and_photos_are_not_in_it()
    {
        var mine = await SignIn();
        var theirs = await SignIn();
        var theirPhoto = Guid.NewGuid();
        await Push(theirs, Record("plants", Guid.NewGuid(), "Theirs"),
            new SyncRecord(SyncKinds.Photos, JsonSerializer.SerializeToElement(
                new { id = theirPhoto, createdAt = Monday, updatedAt = Monday, deletedAt = (DateTimeOffset?)null }, ApiFactory.Json)));
        await Upload(theirs, theirPhoto, "full", Jpeg(100));
        await Push(mine, Record("plants", Guid.NewGuid(), "Mine"));

        using var zip = await Export(mine);

        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.DoesNotContain(names, n => n.Contains(theirs.CollectionId.ToString()));
        Assert.DoesNotContain(names, n => n.Contains(theirPhoto.ToString()));
        Assert.Contains($"collections/{mine.CollectionId}/plants.json", names);
        Assert.Equal(mine.CollectionId, Assert.Single(Read(zip, "collections.json").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.DoesNotContain("Theirs", Encoding.UTF8.GetString(Bytes(zip, $"collections/{mine.CollectionId}/plants.json")));
    }

    [Fact]
    public async Task A_body_over_64_KB_is_refused()
    {
        var me = await SignIn();
        var body = JsonSerializer.Serialize(new { account = new { notes = new string('x', 65 * 1024) } });

        var response = await me.Client.PostAsync("/me/export", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
