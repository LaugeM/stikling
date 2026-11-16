using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Stikling.Api.Data;
using Stikling.Core.Sync;
using static Stikling.Api.Collections.CollectionEndpoints;
using static Stikling.Api.Collections.InviteEndpoints;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

/// <summary>
/// A signed-in person in the people tests. <see cref="CollectionId"/> is the collection the test is
/// about, which is the person's own until they join another one. <see cref="OwnCollectionId"/> stays theirs.
/// </summary>
public sealed record Who(HttpClient Client, Guid PersonId, Guid CollectionId, Guid OwnCollectionId, string? Name);

/// <summary>What the tests of invites, members and helpers do over and over.</summary>
public sealed class PeopleSupport(ApiFactory api)
{
    public static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private int _count;

    public async Task<Who> SignIn(string? name = "")
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        if (name == "")
            name = $"Person {Interlocked.Increment(ref _count)}";
        if (name is not null)
        {
            var set = await client.PutAsJsonAsync("/me/name", new NameRequest(name));
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        }

        var own = me!.Collections.Single().Id;
        return new Who(client, me.PersonId, own, own, name);
    }

    public Task<HttpResponseMessage> CreateInvite(Who by, MemberRole role, bool managesPeople = false) =>
        by.Client.PostAsJsonAsync($"/collections/{by.CollectionId}/invites", new CreateInviteRequest(role, managesPeople), ApiFactory.Json);

    public async Task<InviteResponse> InviteOk(Who by, MemberRole role, bool managesPeople = false)
    {
        var response = await CreateInvite(by, role, managesPeople);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InviteResponse>(ApiFactory.Json))!;
    }

    public Task<HttpResponseMessage> Accept(Who who, string token) => who.Client.PostAsync($"/invites/{token}/accept", null);

    public Task<HttpResponseMessage> Preview(Who who, string token) => who.Client.GetAsync($"/invites/{token}");

    /// <summary>A new person who joins the collection <paramref name="by"/> is in, with this role.</summary>
    public async Task<Who> Join(Who by, MemberRole role, bool managesPeople = false, string? name = "")
    {
        var newcomer = await SignIn(name);
        var invite = await InviteOk(by, role, managesPeople);
        var accepted = await Accept(newcomer, invite.Token!);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return newcomer with { CollectionId = by.CollectionId };
    }

    public async Task<CollectionResponse> Collection(Who who) =>
        (await who.Client.GetFromJsonAsync<CollectionResponse>($"/collections/{who.CollectionId}", ApiFactory.Json))!;

    public async Task<MemberResponse> MemberOf(Who viewer, Who member) =>
        (await Collection(viewer)).Members.Single(m => m.PersonId == member.PersonId);

    public Task<HttpResponseMessage> Change(Who by, Who target, MemberRole role, bool managesPeople = false) =>
        by.Client.PutAsJsonAsync(
            $"/collections/{by.CollectionId}/members/{target.PersonId}", new ChangeMemberRequest(role, managesPeople), ApiFactory.Json);

    public Task<HttpResponseMessage> Remove(Who by, Who target) =>
        by.Client.DeleteAsync($"/collections/{by.CollectionId}/members/{target.PersonId}");

    public Task<HttpResponseMessage> Leave(Who who) => who.Client.PostAsync($"/collections/{who.CollectionId}/leave", null);

    public Task<HttpResponseMessage> HandOver(Who by, Who to) =>
        by.Client.PutAsJsonAsync($"/collections/{by.CollectionId}/owner", new HandOverRequest(to.PersonId), ApiFactory.Json);

    public async Task<string?> ProblemOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("problem", out var problem) ? problem.GetString() : null;

    public static SyncRecord CareLog(Guid id, DateTimeOffset updatedAt, DateTimeOffset? deletedAt = null) =>
        new("careLogs", JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt, deletedAt, plantId = Guid.NewGuid(), kind = "Watered" }, ApiFactory.Json));

    public static SyncRecord Plant(Guid id, DateTimeOffset updatedAt) =>
        new("plants", JsonSerializer.SerializeToElement(new { id, createdAt = Monday, updatedAt, nickname = "Monstera" }, ApiFactory.Json));

    public static SyncRecord Timeline(Guid id, string kind, DateTimeOffset updatedAt, DateTimeOffset? deletedAt = null) =>
        new("timeline", JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt, deletedAt, subjectType = "Plant", subjectId = Guid.NewGuid(), kind, text = "Hello" }, ApiFactory.Json));

    public static SyncRecord PhotoRecord(Guid id, DateTimeOffset updatedAt, DateTimeOffset? deletedAt = null) =>
        new(SyncKinds.Photos, JsonSerializer.SerializeToElement(
            new { id, createdAt = Monday, updatedAt, deletedAt, subjectType = "Plant", subjectId = Guid.NewGuid(), width = 1600, height = 1200 }, ApiFactory.Json));

    public Task<HttpResponseMessage> Push(Who who, params SyncRecord[] records) =>
        who.Client.PostAsJsonAsync($"/collections/{who.CollectionId}/records", new PushRequest([.. records]), ApiFactory.Json);

    public async Task<PushResponse> PushOk(Who who, params SyncRecord[] records)
    {
        var response = await Push(who, records);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PushResponse>(ApiFactory.Json))!;
    }
}
