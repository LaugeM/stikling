using Stikling.Core.People;

namespace Stikling.Core.Tests;

public class CollectionRegistryTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid OwnId = Guid.NewGuid();
    private static readonly Guid SharedId = Guid.NewGuid();

    private static RegisteredCollection Own(string name = "My plants") => new(OwnId, name, CollectionRole.Editor, true, true);

    private static RegisteredCollection Shared(Guid? id = null, string name = "Mum's plants", CollectionRole role = CollectionRole.Helper) =>
        new(id ?? SharedId, name, role, false, false);

    private static CollectionRegistry Merge(CollectionRegistry? previous, Guid? storedMain, params RegisteredCollection[] mine) =>
        CollectionRegistry.Merge(previous, Me, "Anna", mine, storedMain).Registry;

    [Fact]
    public void The_first_time_the_registry_lists_what_me_says_and_the_own_collection_is_the_main_one()
    {
        var registry = Merge(null, null, Shared(), Own());

        Assert.Equal(Me, registry.PersonId);
        Assert.Equal("Anna", registry.PersonName);
        Assert.Equal(OwnId, registry.MainCollectionId);
        Assert.Equal([SharedId, OwnId], registry.Collections.Select(c => c.Id));
        Assert.All(registry.Collections, c => Assert.False(c.Gone));
        Assert.Equal("Anna", registry.Find(OwnId)?.OwnerName);
    }

    [Fact]
    public void A_new_collection_is_added_and_a_renamed_one_keeps_what_was_fetched_about_its_people()
    {
        var friend = Guid.NewGuid();
        var before = Merge(null, null, Own(), Shared());
        before.Find(SharedId)!.People[friend] = "Bo";
        var withOwner = before with
        {
            Collections = [.. before.Collections.Select(c => c.Id == SharedId ? c with { OwnerName = "Mum" } : c)],
        };

        var newcomer = Guid.NewGuid();
        var after = Merge(withOwner, OwnId, Own("Renamed"), Shared(name: "New name", role: CollectionRole.Viewer), Shared(newcomer, "Third"));

        Assert.Equal(3, after.Collections.Count);
        Assert.Equal("Renamed", after.Find(OwnId)?.Name);
        Assert.Equal("New name", after.Find(SharedId)?.Name);
        Assert.Equal(CollectionRole.Viewer, after.Find(SharedId)?.Role);
        Assert.Equal("Bo", after.Find(SharedId)?.People[friend]);
        Assert.Equal("Mum", after.Find(SharedId)?.OwnerName);
        Assert.Empty(after.Find(newcomer)!.People);
    }

    [Fact]
    public void A_collection_that_left_me_is_kept_as_gone_and_comes_back_when_the_person_is_added_again()
    {
        var before = Merge(null, null, Own(), Shared());

        var gone = Merge(before, OwnId, Own());
        Assert.True(gone.Find(SharedId)?.Gone);
        Assert.Equal(CollectionRole.Helper, gone.Find(SharedId)?.Role);
        Assert.Equal(CollectionRole.Viewer, gone.Find(SharedId)!.AccessFor(Me).Role);
        Assert.False(gone.Find(SharedId)!.AccessFor(Me).CanLog);

        var back = Merge(gone, OwnId, Own(), Shared());
        Assert.False(back.Find(SharedId)?.Gone);
    }

    [Fact]
    public void The_main_database_keeps_the_collection_it_has_if_the_person_is_still_in_it()
    {
        var update = CollectionRegistry.Merge(null, Me, "Anna", [Own(), Shared()], storedMain: SharedId);

        Assert.Equal(SharedId, update.Registry.MainCollectionId);
        Assert.False(update.ForgetMainState);
    }

    [Fact]
    public void A_main_database_that_never_synced_gets_the_own_collection_else_the_first()
    {
        var own = CollectionRegistry.Merge(null, Me, "Anna", [Shared(), Own()], storedMain: null);
        var first = CollectionRegistry.Merge(null, Me, "Anna", [Shared(), Shared(Guid.NewGuid())], storedMain: null);

        Assert.Equal(OwnId, own.Registry.MainCollectionId);
        Assert.False(own.ForgetMainState);
        Assert.Equal(SharedId, first.Registry.MainCollectionId);
    }

    [Fact]
    public void A_main_collection_the_person_was_removed_from_is_forgotten_and_dropped_and_the_own_one_takes_over()
    {
        var before = Merge(null, SharedId, Shared(), Own());
        Assert.Equal(SharedId, before.MainCollectionId);

        var update = CollectionRegistry.Merge(before, Me, "Anna", [Own()], storedMain: SharedId);

        Assert.True(update.ForgetMainState);
        Assert.Equal(OwnId, update.Registry.MainCollectionId);
        Assert.Null(update.Registry.Find(SharedId));
        Assert.Empty(update.Discard);
    }

    [Fact]
    public void Another_person_signing_in_starts_a_new_registry_and_discards_the_shared_databases()
    {
        var before = Merge(null, OwnId, Own(), Shared());
        var other = Guid.NewGuid();
        var otherOwn = new RegisteredCollection(Guid.NewGuid(), "Theirs", CollectionRole.Editor, true, true);

        var update = CollectionRegistry.Merge(before, other, "Bo", [otherOwn], storedMain: OwnId);

        Assert.Equal([SharedId], update.Discard);
        Assert.Equal(other, update.Registry.PersonId);
        Assert.Equal([otherOwn.Id], update.Registry.Collections.Select(c => c.Id));
        Assert.Equal(otherOwn.Id, update.Registry.MainCollectionId);

        // The main database's sync state belongs to the person before, so it starts over
        Assert.True(update.ForgetMainState);
    }

    [Fact]
    public void The_registry_is_saved_as_camel_case_text_and_read_back()
    {
        var friend = Guid.NewGuid();
        var registry = Merge(null, null, Own(), Shared());
        registry.Find(SharedId)!.People[friend] = "Bo";

        var json = registry.ToJson();
        var back = CollectionRegistry.FromJson(json)!;

        Assert.Contains("\"personId\"", json);
        Assert.Contains("\"mainCollectionId\"", json);
        Assert.Contains("\"managesPeople\"", json);
        Assert.Contains("\"role\":\"Helper\"", json);
        Assert.Contains($"\"{friend}\":\"Bo\"", json);
        Assert.Equal(registry.PersonId, back.PersonId);
        Assert.Equal(registry.MainCollectionId, back.MainCollectionId);
        Assert.Equal(CollectionRole.Helper, back.Find(SharedId)?.Role);
        Assert.Equal("Bo", back.Find(SharedId)?.People[friend]);
    }

    [Fact]
    public void An_unknown_role_or_broken_text_doesnt_stop_the_registry_from_loading()
    {
        var json = $$"""{"personId":"{{Me}}","collections":[{"id":"{{SharedId}}","name":"X","role":"Commenter","managesPeople":false,"isOwner":false,"gone":false}]}""";

        Assert.Equal(CollectionRole.Viewer, CollectionRegistry.FromJson(json)?.Find(SharedId)?.Role);
        Assert.Null(CollectionRegistry.FromJson("{not json"));
        Assert.Null(CollectionRegistry.FromJson(null));
    }

    [Fact]
    public void The_open_collection_is_only_a_lower_case_guid_with_dashes_and_keys_get_its_id_added()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, OpenCollection.Parse(id.ToString("D")));
        Assert.Null(OpenCollection.Parse(id.ToString("D").ToUpperInvariant()));
        Assert.Null(OpenCollection.Parse(id.ToString("N")));
        Assert.Null(OpenCollection.Parse("stikling"));
        Assert.Null(OpenCollection.Parse(""));
        Assert.Null(OpenCollection.Parse(null));

        Assert.Equal("stikling", OpenCollection.DatabaseName(null));
        Assert.Equal($"stikling-{id}", OpenCollection.DatabaseName(id));
        Assert.Equal("last-sync", OpenCollection.KeyFor("last-sync", null));
        Assert.Equal($"last-sync-{id}", OpenCollection.KeyFor("last-sync", id));
    }
}
