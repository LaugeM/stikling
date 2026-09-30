using System.Net;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>
/// Decides when this device syncs, and runs <see cref="SyncService"/> for whoever is signed in.
/// It syncs once the app is open, a moment after a change, when the connection comes back, when
/// the app is switched back to, and every few minutes while it is on screen. Nothing happens on a
/// device where nobody is signed in, so Clerk isn't loaded there.
/// </summary>
public sealed class SyncRunner(
    AccountService account,
    StiklingApi api,
    IndexedDbSyncStore store,
    IndexedDb db,
    ThemeService theme,
    DeviceFiles files,
    IJSRuntime js,
    TimeProvider time) : IAsyncDisposable
{
    internal const string ModulePath = "./js/sync.js";
    internal const string LastSyncKey = "last-sync";

    /// <summary>How long to wait after a change, so a few changes in a row go in one sync.</summary>
    private static readonly TimeSpan AfterChange = TimeSpan.FromSeconds(2);

    private Task<IJSObjectReference>? module;
    private DotNetObjectReference<SyncRunner>? self;
    private bool started;
    private bool signedIn;
    private bool again;
    private CancellationTokenSource? waiting;
    private Task current = Task.CompletedTask;
    private readonly Dictionary<(Guid, PhotoSize), Task<bool>> fetching = [];
    private readonly HashSet<(Guid, PhotoSize)> notOnServer = [];

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    public bool Running { get; private set; }

    public DateTimeOffset? LastSynced { get; private set; }

    /// <summary>Why the last sync didn't finish, in words for the person, or null when it did.</summary>
    public string? Problem { get; private set; }

    /// <summary>Changes the server wouldn't keep in the last sync. They are tried again next time.</summary>
    public int Refused { get; private set; }

    /// <summary>Changes here that haven't reached the server yet.</summary>
    public int Pending { get; private set; }

    /// <summary>True when the last sync couldn't send photos because the collection's space on the server is used up.</summary>
    public bool PhotosFull { get; private set; }

    /// <summary>True when the last sync left something the person should look at, for the dot on the settings gear.</summary>
    public bool HasProblem => !Running && (Problem is not null || Refused > 0 || PhotosFull);

    private Guid? CollectionId { get; set; }

    /// <summary>Raised when a sync starts or ends, for the status in Settings.</summary>
    public event Action? StatusChanged;

    /// <summary>Raised when a sync saved changes from the server, so the page on screen can reload.</summary>
    public event Action? Received;

    /// <summary>Raised when a sync fetched thumbnails, so photos shown as missing can look again.</summary>
    public event Action? PhotosArrived;

    /// <summary>Called once the app is on screen.</summary>
    public async Task StartAsync()
    {
        if (started || !account.IsAvailable)
            return;

        started = true;
        db.Changed += OnLocalChange;
        account.Changed += OnAccountChanged;
        LastSynced = DateTimeOffset.TryParse(await files.GetAsync(LastSyncKey), out var at) ? at : null;

        self = DotNetObjectReference.Create(this);
        await (await Module).InvokeVoidAsync("watch", self);

        signedIn = await account.WasSignedInAsync();
        if (signedIn)
            _ = SyncNowAsync();
    }

    /// <summary>Syncs now, or once more after the sync that is running. Never throws.</summary>
    public Task SyncNowAsync()
    {
        // Also keeps a sync from starting while signing out
        if (!signedIn)
            return current;

        if (Running)
        {
            again = true;
            return current;
        }

        return current = RunAsync();
    }

    /// <summary>How much would be lost by removing the data from this device right now.</summary>
    public async Task<(int Pending, int Photos)> CountUnsentAsync() =>
        (await store.CountPendingAsync(), await store.CountPhotoUploadsAsync());

    /// <summary>
    /// Fetches one of a photo's images from the server and keeps it here, e.g. the full size when
    /// the photo is opened. False when nobody is signed in, the server doesn't have it, or it can't
    /// be reached. Never throws.
    /// </summary>
    public Task<bool> FetchPhotoAsync(Guid photoId, PhotoSize size)
    {
        // A photo on screen twice is fetched once, and one the server didn't have isn't asked for
        // again until the next sync, when it may have arrived
        if (notOnServer.Contains((photoId, size)))
            return Task.FromResult(false);
        if (fetching.TryGetValue((photoId, size), out var running))
            return running;

        // One that finished straight away, e.g. with nobody signed in, has already cleared itself
        var fetch = FetchAsync(photoId, size);
        if (!fetch.IsCompleted)
            fetching[(photoId, size)] = fetch;
        return fetch;
    }

    private async Task<bool> FetchAsync(Guid photoId, PhotoSize size)
    {
        try
        {
            if (!signedIn || await CollectionIdAsync() is not { } collectionId)
                return false;

            var fetched = await api.DownloadAsync(collectionId, photoId, size);
            if (!fetched)
                notOnServer.Add((photoId, size));
            return fetched;
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            return false;
        }
        finally
        {
            fetching.Remove((photoId, size));
        }
    }

    /// <summary>How much space the collection's photos take on the server, or null when it can't be found out now.</summary>
    public async Task<PhotoUsage?> GetPhotoUsageAsync()
    {
        try
        {
            if (!signedIn || await CollectionIdAsync() is not { } collectionId)
                return null;

            return await api.GetPhotoUsageAsync(collectionId);
        }
        catch (Exception e) when (e is HttpRequestException or AccountUnavailableException or JSException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves a ZIP of everything the server holds about the person. It syncs first, so the latest
    /// changes from this device are in it.
    /// </summary>
    /// <exception cref="HttpRequestException">The server couldn't be reached or turned the request away.</exception>
    public async Task DownloadMyDataAsync()
    {
        await SyncNowAsync();
        await api.DownloadMyDataAsync($"stikling-data-{time.Today():yyyy-MM-dd}.zip");
    }

    /// <summary>
    /// Signs out after any sync that is running. The data stays on the device unless
    /// <paramref name="keepData"/> is false. Either way the device starts over when someone signs
    /// in again, so what's here is merged with their collection.
    /// </summary>
    /// <exception cref="JSException">Clerk couldn't sign out, usually because there is no connection.</exception>
    public async Task SignOutAsync(bool keepData, string redirectUrl)
    {
        signedIn = false;
        waiting?.Cancel();
        await current;

        try
        {
            await account.SignOutAsync(redirectUrl);
        }
        catch (JSException)
        {
            signedIn = true;
            throw;
        }

        await ForgetAsync();
        if (!keepData)
            await store.ClearAllAsync();
    }

    /// <summary>
    /// Deletes the account after any sync that is running: first everything on the server, then
    /// the sign-in at Clerk. The data stays on the device unless <paramref name="keepData"/> is
    /// false, and the app carries on without an account. If it stops halfway, asking again
    /// finishes it.
    /// </summary>
    /// <exception cref="HttpRequestException">The server couldn't be reached or didn't delete it.</exception>
    /// <exception cref="JSException">Clerk couldn't delete the sign-in.</exception>
    public async Task DeleteAccountAsync(bool keepData)
    {
        signedIn = false;
        waiting?.Cancel();
        await current;

        try
        {
            await api.DeleteMeAsync();
            await account.DeleteUserAsync();
        }
        catch (Exception e) when (e is HttpRequestException or JSException or TaskCanceledException)
        {
            signedIn = true;
            throw;
        }

        await ForgetAsync();
        if (!keepData)
            await store.ClearAllAsync();
    }

    // Before the first sync since the app opened, the one this device synced with last time
    private async Task<Guid?> CollectionIdAsync() =>
        CollectionId ?? (await store.GetStateAsync())?.CollectionId;

    private async Task RunAsync()
    {
        Running = true;
        StatusChanged?.Invoke();
        try
        {
            // Nothing is awaited between the last check of "again" and Running turning false, so
            // a sync asked for while this one ends is never dropped
            do
            {
                again = false;
                await SyncOnceAsync();
                Pending = await store.CountPendingAsync();
            }
            while (again && signedIn);
        }
        catch (Exception e)
        {
            // Nothing else may stop the app, since nothing waits for a sync that runs by itself
            Problem = $"Something went wrong while syncing: {e.Message}";
        }
        finally
        {
            Running = false;
            StatusChanged?.Invoke();
        }
    }

    private async Task SyncOnceAsync()
    {
        try
        {
            Problem = null;
            if (!(await account.LoadAsync()).SignedIn)
            {
                signedIn = false;
                return;
            }

            // Everyone has their own collection, the first they joined. Picking between
            // collections comes with inviting people
            var me = await api.GetMeAsync();
            if (me.Collections.FirstOrDefault() is not { } collection)
                return;

            CollectionId = collection.Id;
            var canEdit = collection.Role == "Editor";
            var result = await new SyncService(store, api).SyncAsync(collection.Id, canEdit);
            Refused = result.Refused;

            LastSynced = time.GetUtcNow();
            await files.SetAsync(LastSyncKey, LastSynced.Value.ToString("O"));

            if (result.Received > 0)
            {
                await theme.ApplySavedAsync();
                Received?.Invoke();
            }

            // After the records, since the server only takes a photo's images once it has its record.
            // If this fails, the records have still synced, and the problem shows on its own.
            var photos = await new PhotoSyncService(store, api).SyncAsync(collection.Id, canEdit);
            PhotosFull = photos.CollectionFull;
            notOnServer.Clear();
            if (photos.Downloaded > 0)
                PhotosArrived?.Invoke();
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Gone)
        {
            // Deleted on another device, which ends this sign-in within a minute or so
            Problem = "This account has been deleted. Sign out to carry on without it.";
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Clerk had no session to give a token for, or the API didn't accept it
            Problem = "Your sign-in has run out. Sign out and in again to carry on syncing.";
        }
        catch (Exception e) when (e is AccountUnavailableException or HttpRequestException or TaskCanceledException)
        {
            Problem = "The Stikling server can't be reached right now. Your changes are kept here and sent once it can be.";
        }
    }

    private async void OnLocalChange()
    {
        if (!signedIn)
            return;

        waiting?.Cancel();
        var mine = waiting = new CancellationTokenSource();
        try
        {
            await Task.Delay(AfterChange, mine.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await SyncNowAsync();
    }

    private async void OnAccountChanged(AccountState state)
    {
        // Clerk also reports each renewed token
        if (state.SignedIn == signedIn)
            return;

        signedIn = state.SignedIn;
        if (signedIn)
        {
            await SyncNowAsync();
            return;
        }

        // Signed out in another tab. The data stays, like choosing to keep it
        await current;
        try
        {
            await ForgetAsync();
        }
        catch (JSException)
        {
            // Nothing waits for this, so it mustn't stop the app
        }
    }

    private async Task ForgetAsync()
    {
        await store.ForgetStateAsync();
        await files.SetAsync(LastSyncKey, null);
        LastSynced = null;
        Problem = null;
        Refused = 0;
        PhotosFull = false;
        CollectionId = null;
        notOnServer.Clear();
        StatusChanged?.Invoke();
    }

    /// <summary>A moment the browser reports: back online, back on screen, or the timer.</summary>
    [JSInvokable]
    public void OnBrowserNudge()
    {
        if (signedIn)
            _ = SyncNowAsync();
    }

    public async ValueTask DisposeAsync()
    {
        db.Changed -= OnLocalChange;
        account.Changed -= OnAccountChanged;
        waiting?.Cancel();
        if (module is { IsCompletedSuccessfully: true })
        {
            await module.Result.InvokeVoidAsync("forget");
            await module.Result.DisposeAsync();
        }

        self?.Dispose();
    }
}
