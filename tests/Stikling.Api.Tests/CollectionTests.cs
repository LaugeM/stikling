using System.Net;
using System.Net.Http.Json;
using Stikling.Api.Data;
using static Stikling.Api.Collections.CollectionEndpoints;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class CollectionTests(ApiFactory api)
{
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

    private static Task<HttpResponseMessage> Rename(HttpClient client, Guid collectionId, string? name) =>
        client.PutAsJsonAsync($"/collections/{collectionId}/name", new RenameRequest(name));

    [Fact]
    public async Task You_can_read_your_own_collection()
    {
        var me = await SignIn();

        var collection = await me.Client.GetFromJsonAsync<CollectionResponse>($"/collections/{me.CollectionId}", ApiFactory.Json);

        Assert.Equal(me.CollectionId, collection!.Id);
        var member = Assert.Single(collection.Members);
        Assert.Equal(me.PersonId, member.PersonId);
        Assert.Equal(MemberRole.Editor, member.Role);
    }

    [Fact]
    public async Task Someone_else_cannot_read_or_change_your_collection()
    {
        var me = await SignIn();
        var stranger = await SignIn();

        var read = await stranger.Client.GetAsync($"/collections/{me.CollectionId}");
        var rename = await Rename(stranger.Client, me.CollectionId, "Mine now");

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
    }

    [Fact]
    public async Task A_collection_needs_a_token()
    {
        var me = await SignIn();
        var anonymous = api.CreateClient();

        var read = await anonymous.GetAsync($"/collections/{me.CollectionId}");
        var rename = await Rename(anonymous, me.CollectionId, "Mine now");

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rename.StatusCode);
    }

    [Fact]
    public async Task Someone_who_never_signed_in_before_cannot_read_a_collection()
    {
        var me = await SignIn();
        var newcomer = api.ClientFor(ApiFactory.NewClerkUserId());

        var read = await newcomer.GetAsync($"/collections/{me.CollectionId}");

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task A_viewer_can_read_but_not_change()
    {
        var me = await SignIn();
        var sitter = await SignIn();
        await AddMember(me.CollectionId, sitter.PersonId, MemberRole.Viewer);

        var read = await sitter.Client.GetFromJsonAsync<CollectionResponse>($"/collections/{me.CollectionId}", ApiFactory.Json);
        var rename = await Rename(sitter.Client, me.CollectionId, "Sitter's plants");

        Assert.Equal(2, read!.Members.Count);
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
    }

    [Fact]
    public async Task An_added_editor_can_change_it()
    {
        var me = await SignIn();
        var partner = await SignIn();
        await AddMember(me.CollectionId, partner.PersonId, MemberRole.Editor);

        var rename = await Rename(partner.Client, me.CollectionId, "Our plants");

        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);
    }

    [Fact]
    public async Task Renaming_trims_the_name()
    {
        var me = await SignIn();

        await Rename(me.Client, me.CollectionId, "  Balcony  ");
        var collection = await me.Client.GetFromJsonAsync<CollectionResponse>($"/collections/{me.CollectionId}", ApiFactory.Json);

        Assert.Equal("Balcony", collection!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_collection_needs_a_name(string? name)
    {
        var me = await SignIn();

        var rename = await Rename(me.Client, me.CollectionId, name);

        Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);
    }

    [Fact]
    public async Task A_name_can_be_at_most_100_characters()
    {
        var me = await SignIn();

        var longest = await Rename(me.Client, me.CollectionId, new string('a', Collection.MaxNameLength));
        var tooLong = await Rename(me.Client, me.CollectionId, new string('a', Collection.MaxNameLength + 1));

        Assert.Equal(HttpStatusCode.NoContent, longest.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }
}
