using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.People;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class MeTests(ApiFactory api)
{
    private static Task<MeResponse?> GetMe(HttpClient client) =>
        client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);

    [Fact]
    public async Task The_first_sign_in_creates_a_collection_of_your_own()
    {
        var me = await GetMe(api.ClientFor(ApiFactory.NewClerkUserId()));

        var collection = Assert.Single(me!.Collections);
        Assert.Equal(CurrentPerson.FirstCollectionName, collection.Name);
        Assert.Equal(MemberRole.Editor, collection.Role);
    }

    [Fact]
    public async Task A_new_person_owns_their_collection_and_manages_people_in_it()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var me = await GetMe(client);

        var collection = Assert.Single(me!.Collections);
        Assert.True(collection.IsOwner);
        Assert.True(collection.ManagesPeople);
        Assert.Null(me.Name);
        await api.WithDbAsync(async db =>
            Assert.Equal(me.PersonId, (await db.Collections.SingleAsync(c => c.Id == collection.Id)).OwnerPersonId));
    }

    [Fact]
    public async Task Each_collection_says_who_owns_it()
    {
        var owner = api.ClientFor(ApiFactory.NewClerkUserId());
        await owner.PutAsJsonAsync("/me/name", new NameRequest("Anna"));
        var ownerMe = await GetMe(owner);
        var unnamed = await GetMe(api.ClientFor(ApiFactory.NewClerkUserId()));

        Assert.Equal("Anna", Assert.Single(ownerMe!.Collections).OwnerName);
        Assert.Null(Assert.Single(unnamed!.Collections).OwnerName);
    }

    [Fact]
    public async Task The_name_is_set_trimmed_and_shown_in_me()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var response = await client.PutAsJsonAsync("/me/name", new NameRequest("  Anna  "));
        var me = await GetMe(client);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Anna", me!.Name);
    }

    [Fact]
    public async Task Setting_the_name_first_creates_the_person()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var response = await client.PutAsJsonAsync("/me/name", new NameRequest("Anna"));
        var me = await GetMe(client);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(me!.Collections);
        Assert.Equal("Anna", me.Name);
    }

    [Fact]
    public async Task A_name_can_have_1_to_40_characters()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var longest = await client.PutAsJsonAsync("/me/name", new NameRequest(new string('a', Person.MaxNameLength)));
        var tooLong = await client.PutAsJsonAsync("/me/name", new NameRequest(new string('a', Person.MaxNameLength + 1)));
        var empty = await client.PutAsJsonAsync("/me/name", new NameRequest("   "));
        var missing = await client.PutAsJsonAsync("/me/name", new NameRequest(null));

        Assert.Equal(HttpStatusCode.NoContent, longest.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("name", await tooLong.Content.ReadAsStringAsync());
        Assert.Equal(new string('a', Person.MaxNameLength), (await GetMe(client))!.Name);
    }

    [Fact]
    public async Task Setting_the_name_needs_a_token()
    {
        var response = await api.CreateClient().PutAsJsonAsync("/me/name", new NameRequest("Anna"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signing_in_again_finds_the_same_person_and_collection()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var first = await GetMe(client);
        var second = await GetMe(client);

        Assert.Equal(first!.PersonId, second!.PersonId);
        Assert.Equal(first.Collections.Single().Id, second.Collections.Single().Id);
    }

    [Fact]
    public async Task Two_people_get_separate_collections()
    {
        var mine = await GetMe(api.ClientFor(ApiFactory.NewClerkUserId()));
        var theirs = await GetMe(api.ClientFor(ApiFactory.NewClerkUserId()));

        Assert.NotEqual(mine!.PersonId, theirs!.PersonId);
        Assert.NotEqual(mine.Collections.Single().Id, theirs.Collections.Single().Id);
    }

    [Fact]
    public async Task Clerk_ids_that_differ_only_in_case_are_different_people()
    {
        var clerkUserId = $"user_{Guid.NewGuid():N}AbC";

        var one = await GetMe(api.ClientFor(clerkUserId));
        var other = await GetMe(api.ClientFor(clerkUserId.ToUpperInvariant()));

        Assert.NotEqual(one!.PersonId, other!.PersonId);
    }

    [Fact]
    public async Task Devices_signing_in_at_the_same_moment_get_one_person_and_one_collection()
    {
        var clerkUserId = ApiFactory.NewClerkUserId();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => GetMe(api.ClientFor(clerkUserId))));

        Assert.Single(results.Select(me => me!.PersonId).Distinct());
        await api.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.People.CountAsync(p => p.ClerkUserId == clerkUserId));
            Assert.Equal(1, await db.Memberships.CountAsync(m => m.Person.ClerkUserId == clerkUserId));
        });
    }

    [Fact]
    public async Task Collections_you_were_added_to_are_listed_with_your_role()
    {
        var owner = await GetMe(api.ClientFor(ApiFactory.NewClerkUserId()));
        var sitterClient = api.ClientFor(ApiFactory.NewClerkUserId());
        var sitter = await GetMe(sitterClient);
        var shared = owner!.Collections.Single().Id;

        await api.WithDbAsync(async db =>
        {
            db.Memberships.Add(new Membership { CollectionId = shared, PersonId = sitter!.PersonId, Role = MemberRole.Viewer });
            await db.SaveChangesAsync();
        });

        var after = await GetMe(sitterClient);

        Assert.Equal(2, after!.Collections.Count);
        Assert.Equal(MemberRole.Viewer, after.Collections.Single(c => c.Id == shared).Role);
    }
}
