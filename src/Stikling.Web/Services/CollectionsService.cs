using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Stikling.Core.People;

namespace Stikling.Web.Services;

/// <summary>
/// The collections this device holds and which one is open. Each collection the person is invited into
/// has a database of its own on the device, and the main database holds the person's own. Only the open
/// one is read, written and synced, and switching reloads the app so nothing keeps state from the other.
/// The list comes from the server on every sync and is kept in <see cref="DeviceFiles"/>, so it can be
/// shown offline. Everything that depends on the person's role in the open collection asks
/// <see cref="GetAccessAsync"/>.
/// </summary>
public sealed class CollectionsService(
    DeviceFiles files, AccountService account, IServiceProvider services, IndexedDbSyncStore store, NavigationManager nav, IJSRuntime js)
    : JsModule(js, IndexedDb.ModulePath)
{
    // Asked for when needed: the repositories use this service, and the API reaches them through the photo service, so it can't be a constructor parameter
    private StiklingApi Api => services.GetRequiredService<StiklingApi>();

    private CollectionRegistry? registry;
    private Guid? openId;
    private bool loaded;

    /// <summary>The shared collection that is open, or null when the main database is. Read once at the start.</summary>
    public Guid? OpenId => openId;

    /// <summary>True when a shared collection's database is the one open, not the main one.</summary>
    public bool IsSharedOpen => openId is not null;

    /// <summary>The shared collection that is open, or null when the main database is. Kept up to date by <see cref="LoadAsync"/> and every sync.</summary>
    public RegisteredCollection? Open => openId is { } id ? registry?.Find(id) : null;

    /// <summary>
    /// Reads what the device kept, once. Nobody is signed in on a device that was signed out in another tab or
    /// before the app was updated, so it opens the main database: the choice is cleared before anything
    /// has opened a database, so no reload is needed.
    /// </summary>
    public async Task LoadAsync()
    {
        if (loaded)
            return;

        // Each person has a registry of their own. The device remembers whose is in use, until someone else's sync takes over
        registry = await ReadRegistryAsync(await files.GetAsync(PersonKey));
        openId = await files.GetOpenCollectionAsync();
        loaded = true;

        // Signed out, or a collection that isn't in this person's list, opens the main database
        if (openId is not null && (!await account.WasSignedInAsync() || registry?.Find(openId.Value) is null))
        {
            openId = null;
            await files.SetAsync(OpenCollection.Key, null);
        }
    }

    /// <summary>The key in <see cref="DeviceFiles"/> for the id of the person whose registry is in use.</summary>
    internal const string PersonKey = "current-person";

    private async Task<CollectionRegistry?> ReadRegistryAsync(string? personText)
    {
        if (!Guid.TryParse(personText, out var person))
            return null;

        var read = CollectionRegistry.FromJson(await files.GetAsync(CollectionRegistry.KeyFor(person)));
        return read?.PersonId == person ? read : null;
    }

    /// <summary>The collection whose database is open: the shared one, or else the one the main database holds. Null before the first sync.</summary>
    public RegisteredCollection? Current => (openId ?? registry?.MainCollectionId) is { } id ? registry?.Find(id) : null;

    /// <summary>
    /// Asks the server for the person's collections and name and brings the list up to date, so a collection
    /// that was just joined can be opened and a new name shows. Returns what the server said, or null when it
    /// listed no collections. Sync does the same on every run.
    /// </summary>
    /// <exception cref="HttpRequestException">The API couldn't be reached or turned the request away.</exception>
    public async Task<Me?> RefreshAsync()
    {
        var me = await Api.GetMeAsync();
        if (me.Collections.Count == 0)
            return null;

        var openBefore = openId;
        await ApplyMeAsync(me);

        // Someone else is signed in than on the last sync, so the shared collection that was open isn't theirs
        if (openBefore is not null && openId is null)
            await SwitchToAsync(null);
        return me;
    }

    /// <summary>
    /// Merges what <c>/me</c> says into the registry and saves it, see <see cref="CollectionRegistry.Merge"/>.
    /// When the main database's collection is gone it forgets its sync state, so it starts over with the person's own.
    /// </summary>
    internal async Task<CollectionRegistry> ApplyMeAsync(Me me)
    {
        await LoadAsync();

        // Someone else is signed in than the person whose list this was. Theirs is read, and the shared
        // collection that was open isn't in it, so the main database opens
        if (registry?.PersonId != me.PersonId)
        {
            registry = await ReadRegistryAsync(me.PersonId.ToString("D"));
            await files.SetAsync(PersonKey, me.PersonId.ToString("D"));
            if (openId is { } open && registry?.Find(open) is null)
            {
                openId = null;
                await files.SetAsync(OpenCollection.Key, null);
            }
        }

        var storedMain = openId is null ? (await store.GetStateAsync())?.CollectionId : registry?.MainCollectionId;
        var mine = me.Collections
            .Select(c => new RegisteredCollection(c.Id, c.Name, CollectionRoles.Parse(c.Role), c.ManagesPeople, c.IsOwner, c.OwnerName))
            .ToList();
        var update = CollectionRegistry.Merge(registry, me.PersonId, me.Name, mine, storedMain);

        // The main database's collection is gone, so it starts over and its data merges into the person's own
        if (update.ForgetMainState && openId is null)
        {
            await store.ForgetStateAsync();
            await files.SetAsync(await files.KeyForOpenAsync(SyncRunner.LastSyncKey), null);
        }

        await SaveRegistryAsync(update.Registry);
        return update.Registry;
    }

    public async Task<CollectionRegistry?> GetRegistryAsync()
    {
        await LoadAsync();
        return registry;
    }

    public async Task<RegisteredCollection?> GetOpenAsync()
    {
        await LoadAsync();
        return Open;
    }

    /// <summary>
    /// What the person may do in the open collection. Everything while nobody is signed in and in their own
    /// collection. A collection they were removed from reads as a viewer's, and so does one the registry
    /// doesn't know, which is the safest.
    /// </summary>
    public async Task<MemberAccess> GetAccessAsync()
    {
        await LoadAsync();
        if (registry is null || !await account.WasSignedInAsync())
            return MemberAccess.Full(Guid.Empty);

        if (openId is { } id)
            return registry.Find(id)?.AccessFor(registry.PersonId) ?? new MemberAccess(registry.PersonId, CollectionRole.Viewer, false, false);

        return registry.MainCollectionId is { } main && registry.Find(main) is { IsOwner: false } shared
            ? shared.AccessFor(registry.PersonId)
            : MemberAccess.Full(registry.PersonId);
    }

    /// <summary>The signed-in person's id, or null when nobody is. It is what new care entries, photos and notes are marked with.</summary>
    public async Task<Guid?> GetPersonIdAsync()
    {
        await LoadAsync();
        return registry is not null && await account.WasSignedInAsync() ? registry.PersonId : null;
    }

    /// <summary>
    /// The name of a person in the open collection, as it was when the device last synced it, or null when
    /// nobody is named. People who have left are named too.
    /// </summary>
    public string? NameOf(Guid? personId)
    {
        if (personId is not { } id || registry is null)
            return null;

        var here = openId is { } open ? registry.Find(open) : registry.MainCollectionId is { } main ? registry.Find(main) : null;
        if (here is not null && here.People.TryGetValue(id, out var name))
            return name;
        return id == registry.PersonId ? registry.PersonName : null;
    }

    /// <summary>
    /// The name to show after an entry as "by {name}": who made it, when it wasn't the person and the open
    /// collection knows them. Null otherwise, so nothing is shown in a collection nobody else is in.
    /// </summary>
    public string? LoggedBy(Guid? createdBy) =>
        createdBy is { } id && registry is not null && id != registry.PersonId ? NameOf(id) : null;

    /// <summary>
    /// Opens another collection and reloads the app at the front page. Null, or the collection the main
    /// database holds, opens the main database.
    /// </summary>
    public async Task SwitchToAsync(Guid? collectionId)
    {
        await LoadAsync();
        if (collectionId is { } id && id == registry?.MainCollectionId)
            collectionId = null;

        await files.SetAsync(OpenCollection.Key, collectionId?.ToString("D"));
        nav.NavigateTo("/", forceLoad: true);
    }

    /// <summary>
    /// Takes a shared collection off this device: its database, the entry in the list and what was kept about
    /// it. If it is open, the main database opens. The main database isn't a shared one and stays.
    /// </summary>
    public async Task RemoveFromDeviceAsync(Guid collectionId)
    {
        await LoadAsync();
        if (collectionId == registry?.MainCollectionId)
            return;

        var wasOpen = openId == collectionId;
        if (wasOpen)
        {
            // If the reload doesn't happen, the main database is what opens next
            openId = null;
            await files.SetAsync(OpenCollection.Key, null);
        }

        await DeleteDatabasesAsync([collectionId]);
        if (registry is not null)
        {
            registry = registry with { Collections = [.. registry.Collections.Where(c => c.Id != collectionId)] };
            await SaveRegistryAsync(registry);
        }

        if (wasOpen)
            nav.NavigateTo("/", forceLoad: true);
    }

    /// <summary>
    /// Deletes every shared collection's database and the list of collections, for removing the data from this
    /// device and for deleting the account. The main database is left to the caller. If a shared collection was
    /// open, the app has to be reloaded afterwards, which <see cref="SwitchToAsync"/> does.
    /// </summary>
    public async Task ForgetAllSharedAsync()
    {
        await LoadAsync();
        var shared = (registry?.Collections ?? []).Where(c => c.Id != registry!.MainCollectionId).Select(c => c.Id).ToList();
        if (openId is { } open && !shared.Contains(open))
            shared.Add(open);

        await files.SetAsync(OpenCollection.Key, null);
        openId = null;
        await DeleteDatabasesAsync(shared);

        // Only the signed-in person's. What someone else kept on this device is theirs to remove
        if (registry is not null)
            await files.SetAsync(CollectionRegistry.KeyFor(registry.PersonId), null);
        await files.SetAsync(PersonKey, null);
        registry = null;
    }

    internal async Task SaveRegistryAsync(CollectionRegistry updated)
    {
        registry = updated;
        await files.SetAsync(CollectionRegistry.KeyFor(updated.PersonId), updated.ToJson());
        await files.SetAsync(PersonKey, updated.PersonId.ToString("D"));
    }

    private async Task DeleteDatabasesAsync(IEnumerable<Guid> collectionIds)
    {
        foreach (var id in collectionIds)
        {
            await (await Module).InvokeVoidAsync("deleteCollectionDatabase", id.ToString("D"));
            await files.SetAsync(OpenCollection.KeyFor(SyncRunner.LastSyncKey, id), null);
            await files.SetAsync(OpenCollection.KeyFor(DeviceFiles.LastBackupKey, id), null);
        }
    }
}
