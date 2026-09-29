using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Stikling.Api.Data;
using Stikling.Core.Sync;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class RecordTests(ApiFactory api)
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

    private static SyncRecord Plant(Guid id, DateTimeOffset updatedAt, string nickname = "Monstera", DateTimeOffset? deletedAt = null) =>
        new("plants", JsonSerializer.SerializeToElement(new { id, createdAt = Monday, updatedAt, deletedAt, nickname }, ApiFactory.Json));

    private static Task<HttpResponseMessage> Push(HttpClient client, Guid collectionId, params SyncRecord[] records) =>
        client.PostAsJsonAsync($"/collections/{collectionId}/records", new PushRequest([.. records]), ApiFactory.Json);

    private static async Task<PushResponse> PushOk(HttpClient client, Guid collectionId, params SyncRecord[] records)
    {
        var response = await Push(client, collectionId, records);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PushResponse>(ApiFactory.Json))!;
    }

    private static async Task<PullResponse> Pull(HttpClient client, Guid collectionId, long after = 0) =>
        (await client.GetFromJsonAsync<PullResponse>($"/collections/{collectionId}/records?after={after}", ApiFactory.Json))!;

    private static string? Nickname(SyncRecord record) => record.Data.GetProperty("nickname").GetString();

    [Fact]
    public async Task What_one_device_sends_another_can_fetch()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();

        await PushOk(me.Client, me.CollectionId, Plant(id, Monday));
        var pulled = await Pull(me.Client, me.CollectionId);

        var record = Assert.Single(pulled.Records);
        Assert.Equal("plants", record.Kind);
        Assert.Equal(id, record.Data.GetProperty("id").GetGuid());
        Assert.Equal("Monstera", Nickname(record));
        Assert.False(pulled.More);
    }

    [Fact]
    public async Task Fields_the_server_doesnt_know_are_kept_as_they_are()
    {
        var me = await SignIn();
        var data = JsonSerializer.SerializeToElement(
            new { id = Guid.NewGuid(), updatedAt = Monday, tags = new[] { "rare" }, attention = new { note = "Yellow leaf" } },
            ApiFactory.Json);

        await PushOk(me.Client, me.CollectionId, new SyncRecord("plants", data));
        var record = Assert.Single((await Pull(me.Client, me.CollectionId)).Records);

        Assert.Equal("rare", record.Data.GetProperty("tags")[0].GetString());
        Assert.Equal("Yellow leaf", record.Data.GetProperty("attention").GetProperty("note").GetString());
    }

    [Fact]
    public async Task Fetching_after_a_change_number_gives_only_what_came_later()
    {
        var me = await SignIn();
        await PushOk(me.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday, "First"));
        var first = await Pull(me.Client, me.CollectionId);

        await PushOk(me.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday, "Second"));
        var second = await Pull(me.Client, me.CollectionId, first.Next);
        var nothing = await Pull(me.Client, me.CollectionId, second.Next);

        Assert.Equal("Second", Nickname(Assert.Single(second.Records)));
        Assert.Empty(nothing.Records);
        Assert.Equal(second.Next, nothing.Next);
    }

    [Fact]
    public async Task A_newer_version_replaces_the_one_on_the_server()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();
        await PushOk(me.Client, me.CollectionId, Plant(id, Monday, "Old name"));
        var before = await Pull(me.Client, me.CollectionId);

        var response = await PushOk(me.Client, me.CollectionId, Plant(id, Monday.AddHours(1), "New name"));
        var after = await Pull(me.Client, me.CollectionId, before.Next);

        Assert.Empty(response.Newer);
        Assert.Equal("New name", Nickname(Assert.Single(after.Records)));
        Assert.Equal("New name", Nickname(Assert.Single((await Pull(me.Client, me.CollectionId)).Records)));
    }

    [Fact]
    public async Task An_older_version_is_turned_down_and_the_newer_one_sent_back()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();
        await PushOk(me.Client, me.CollectionId, Plant(id, Monday.AddHours(1), "Newer"));

        var response = await PushOk(me.Client, me.CollectionId, Plant(id, Monday, "Older"));

        Assert.Equal("Newer", Nickname(Assert.Single(response.Newer)));
        Assert.Equal("Newer", Nickname(Assert.Single((await Pull(me.Client, me.CollectionId)).Records)));
    }

    [Fact]
    public async Task The_same_version_sent_twice_is_kept_once()
    {
        var me = await SignIn();
        var plant = Plant(Guid.NewGuid(), Monday);
        await PushOk(me.Client, me.CollectionId, plant);
        var first = await Pull(me.Client, me.CollectionId);

        var again = await PushOk(me.Client, me.CollectionId, plant);

        Assert.Empty(again.Newer);
        Assert.Empty((await Pull(me.Client, me.CollectionId, first.Next)).Records);
    }

    [Fact]
    public async Task A_delete_is_a_change_like_any_other()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();
        await PushOk(me.Client, me.CollectionId, Plant(id, Monday));

        var deletedAt = Monday.AddDays(1);
        await PushOk(me.Client, me.CollectionId, Plant(id, deletedAt, deletedAt: deletedAt));
        var record = Assert.Single((await Pull(me.Client, me.CollectionId)).Records);

        Assert.Equal(deletedAt, record.Data.GetProperty("deletedAt").GetDateTimeOffset());
        await api.WithDbAsync(db =>
        {
            Assert.Equal(deletedAt, db.Records.Single(r => r.Id == id).DeletedAt);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task The_same_record_twice_in_one_upload_keeps_the_newest()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();

        await PushOk(me.Client, me.CollectionId, Plant(id, Monday.AddHours(2), "Newest"), Plant(id, Monday, "Oldest"));

        Assert.Equal("Newest", Nickname(Assert.Single((await Pull(me.Client, me.CollectionId)).Records)));
    }

    [Fact]
    public async Task Fetching_comes_in_pages()
    {
        var me = await SignIn();
        var plants = Enumerable.Range(0, SyncRules.BatchSize + 3).Select(i => Plant(Guid.NewGuid(), Monday, $"Plant {i}")).ToArray();
        await PushOk(me.Client, me.CollectionId, plants[..SyncRules.BatchSize]);
        await PushOk(me.Client, me.CollectionId, plants[SyncRules.BatchSize..]);

        var first = await Pull(me.Client, me.CollectionId);
        var second = await Pull(me.Client, me.CollectionId, first.Next);

        Assert.Equal(SyncRules.BatchSize, first.Records.Count);
        Assert.True(first.More);
        Assert.Equal(3, second.Records.Count);
        Assert.False(second.More);
        Assert.Equal(plants.Length, first.Records.Concat(second.Records).Select(r => r.Data.GetProperty("id").GetGuid()).Distinct().Count());
    }

    [Fact]
    public async Task Uploads_to_one_collection_at_the_same_time_all_get_their_own_change_numbers()
    {
        var me = await SignIn();

        var uploads = Enumerable.Range(0, 10).Select(i => PushOk(me.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday, $"Plant {i}")));
        await Task.WhenAll(uploads);

        await api.WithDbAsync(db =>
        {
            var versions = db.Records.Where(r => r.CollectionId == me.CollectionId).Select(r => r.Version).OrderBy(v => v).ToList();
            Assert.Equal(Enumerable.Range(1, 10).Select(v => (long)v), versions);
            Assert.Equal(10, db.Collections.Single(c => c.Id == me.CollectionId).LastVersion);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Collections_keep_their_records_apart_even_with_the_same_id()
    {
        var me = await SignIn();
        var other = await SignIn();
        var sharedId = Guid.NewGuid();

        await PushOk(me.Client, me.CollectionId, Plant(sharedId, Monday, "Mine"));
        await PushOk(other.Client, other.CollectionId, Plant(sharedId, Monday.AddHours(1), "Theirs"));

        Assert.Equal("Mine", Nickname(Assert.Single((await Pull(me.Client, me.CollectionId)).Records)));
        Assert.Equal("Theirs", Nickname(Assert.Single((await Pull(other.Client, other.CollectionId)).Records)));
    }

    [Fact]
    public async Task Someone_else_can_neither_fetch_nor_send()
    {
        var me = await SignIn();
        var stranger = await SignIn();

        var pull = await stranger.Client.GetAsync($"/collections/{me.CollectionId}/records");
        var push = await Push(stranger.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday));

        Assert.Equal(HttpStatusCode.Forbidden, pull.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, push.StatusCode);
    }

    [Fact]
    public async Task Records_need_a_token()
    {
        var me = await SignIn();
        var anonymous = api.CreateClient();

        var pull = await anonymous.GetAsync($"/collections/{me.CollectionId}/records");
        var push = await Push(anonymous, me.CollectionId, Plant(Guid.NewGuid(), Monday));

        Assert.Equal(HttpStatusCode.Unauthorized, pull.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, push.StatusCode);
    }

    [Fact]
    public async Task A_viewer_can_fetch_but_not_send()
    {
        var me = await SignIn();
        var sitter = await SignIn();
        await AddMember(me.CollectionId, sitter.PersonId, MemberRole.Viewer);
        await PushOk(me.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday));

        var pulled = await Pull(sitter.Client, me.CollectionId);
        var push = await Push(sitter.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday));

        Assert.Single(pulled.Records);
        Assert.Equal(HttpStatusCode.Forbidden, push.StatusCode);
    }

    [Fact]
    public async Task An_added_editor_can_send()
    {
        var me = await SignIn();
        var partner = await SignIn();
        await AddMember(me.CollectionId, partner.PersonId, MemberRole.Editor);

        await PushOk(partner.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday, "From my partner"));

        Assert.Equal("From my partner", Nickname(Assert.Single((await Pull(me.Client, me.CollectionId)).Records)));
    }

    [Fact]
    public async Task Settings_are_not_a_collection_record()
    {
        var me = await SignIn();
        var settings = JsonSerializer.SerializeToElement(new { id = Guid.NewGuid(), updatedAt = Monday }, ApiFactory.Json);

        var response = await PushOk(me.Client, me.CollectionId, new SyncRecord(SyncKinds.Settings, settings));

        Assert.Equal(0, Assert.Single(response.Refused).Index);
        Assert.Empty((await Pull(me.Client, me.CollectionId)).Records);
    }

    [Theory]
    [InlineData("Plants")]
    [InlineData("people")]
    [InlineData("")]
    public async Task Only_known_kinds_are_kept(string kind)
    {
        var me = await SignIn();

        var response = await PushOk(me.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday) with { Kind = kind });

        Assert.Single(response.Refused);
        Assert.Empty((await Pull(me.Client, me.CollectionId)).Records);
    }

    public static TheoryData<string> BrokenRecords => new()
    {
        """{ "updatedAt": "2026-09-28T12:00:00+00:00" }""",
        """{ "id": "00000000-0000-0000-0000-000000000000", "updatedAt": "2026-09-28T12:00:00+00:00" }""",
        """{ "id": "b3a1f7a2-5d0e-4c4f-9d52-2f8f7e0c1a11" }""",
        """{ "id": "not a guid", "updatedAt": "2026-09-28T12:00:00+00:00" }""",
        """{ "id": "b3a1f7a2-5d0e-4c4f-9d52-2f8f7e0c1a11", "updatedAt": "yesterday" }""",
        """{ "id": "b3a1f7a2-5d0e-4c4f-9d52-2f8f7e0c1a11", "updatedAt": "2026-09-28T12:00:00+00:00", "deletedAt": 5 }""",
        """[1, 2, 3]""",
        """ "a plant" """,
    };

    [Theory]
    [MemberData(nameof(BrokenRecords))]
    public async Task A_record_needs_an_id_and_an_updatedAt(string json)
    {
        var me = await SignIn();

        var response = await PushOk(me.Client, me.CollectionId, new SyncRecord("plants", JsonSerializer.Deserialize<JsonElement>(json)));

        Assert.Single(response.Refused);
    }

    [Fact]
    public async Task A_bad_record_is_refused_and_the_rest_are_kept()
    {
        var me = await SignIn();
        var good = Guid.NewGuid();

        var response = await PushOk(me.Client, me.CollectionId,
            Plant(Guid.NewGuid(), Monday) with { Kind = "people" },
            Plant(good, Monday),
            Plant(Guid.NewGuid(), Monday, new string('a', SyncRules.MaxRecordLength)));

        Assert.Equal([0, 2], response.Refused.Select(r => r.Index));
        var kept = Assert.Single((await Pull(me.Client, me.CollectionId)).Records);
        Assert.Equal(good, kept.Data.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task An_empty_entry_is_refused()
    {
        var me = await SignIn();
        using var body = new StringContent("""{ "records": [null] }""", System.Text.Encoding.UTF8, "application/json");

        var response = await me.Client.PostAsync($"/collections/{me.CollectionId}/records", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Content.ReadFromJsonAsync<PushResponse>(ApiFactory.Json))!.Refused);
    }

    [Fact]
    public async Task A_record_can_be_at_most_the_size_limit()
    {
        var me = await SignIn();

        var response = await PushOk(me.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday, new string('a', SyncRules.MaxRecordLength)));

        Assert.Single(response.Refused);
    }

    [Fact]
    public async Task A_record_changed_in_the_future_is_refused()
    {
        var me = await SignIn();
        var now = DateTimeOffset.UtcNow;

        var response = await PushOk(me.Client, me.CollectionId,
            Plant(Guid.NewGuid(), now.AddDays(2), "From a clock two days ahead"),
            Plant(Guid.NewGuid(), now.AddHours(1), "From a clock an hour ahead"));

        Assert.Equal(0, Assert.Single(response.Refused).Index);
        Assert.Equal("From a clock an hour ahead", Nickname(Assert.Single((await Pull(me.Client, me.CollectionId)).Records)));
    }

    [Fact]
    public async Task An_upload_can_hold_at_most_one_batch()
    {
        var me = await SignIn();
        var plants = Enumerable.Range(0, SyncRules.BatchSize + 1).Select(_ => Plant(Guid.NewGuid(), Monday)).ToArray();

        var push = await Push(me.Client, me.CollectionId, plants);

        Assert.Equal(HttpStatusCode.BadRequest, push.StatusCode);
    }

    [Fact]
    public async Task Two_different_versions_from_the_same_moment_settle_on_the_same_one()
    {
        var first = await SignIn();
        var second = await SignIn();
        var id = Guid.NewGuid();

        await PushOk(first.Client, first.CollectionId, Plant(id, Monday, "Aloe"));
        var firstAnswer = await PushOk(first.Client, first.CollectionId, Plant(id, Monday, "Zamioculcas"));
        await PushOk(second.Client, second.CollectionId, Plant(id, Monday, "Zamioculcas"));
        var secondAnswer = await PushOk(second.Client, second.CollectionId, Plant(id, Monday, "Aloe"));

        Assert.Empty(firstAnswer.Newer);
        Assert.Equal("Zamioculcas", Nickname(Assert.Single(secondAnswer.Newer)));
        Assert.Equal("Zamioculcas", Nickname(Assert.Single((await Pull(first.Client, first.CollectionId)).Records)));
        Assert.Equal("Zamioculcas", Nickname(Assert.Single((await Pull(second.Client, second.CollectionId)).Records)));
    }

    [Fact]
    public async Task A_device_ahead_of_the_server_is_told_to_start_over()
    {
        var me = await SignIn();
        await PushOk(me.Client, me.CollectionId, Plant(Guid.NewGuid(), Monday));

        var ahead = await Pull(me.Client, me.CollectionId, after: 1000);
        var caughtUp = await Pull(me.Client, me.CollectionId, after: 1);

        Assert.True(ahead.StartOver);
        Assert.Empty(ahead.Records);
        Assert.False(caughtUp.StartOver);
    }
}
