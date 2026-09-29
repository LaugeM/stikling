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
