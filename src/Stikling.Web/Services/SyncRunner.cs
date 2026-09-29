using System.Net;
using Microsoft.JSInterop;
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

    /// <summary>Raised when a sync starts or ends, for the status in Settings.</summary>
    public event Action? StatusChanged;

    /// <summary>Raised when a sync saved changes from the server, so the page on screen can reload.</summary>
    public event Action? Received;

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
        if (Running)
        {
            again = true;
            return current;
        }

        return current = RunAsync();
    }

    /// <summary>How much would be lost by removing the data from this device right now.</summary>
    public async Task<(int Pending, int Photos)> CountUnsentAsync() =>
        (await store.CountPendingAsync(), await store.CountPhotoImagesAsync());

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

    private async Task RunAsync()
    {
        Running = true;
        StatusChanged?.Invoke();
        try
        {
            do
            {
                again = false;
                await SyncOnceAsync();
            }
            while (again && signedIn);

            Pending = await store.CountPendingAsync();
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

            var result = await new SyncService(store, api).SyncAsync(collection.Id, collection.Role == "Editor");
            Problem = null;
            Refused = result.Refused;
            LastSynced = time.GetUtcNow();
            await files.SetAsync(LastSyncKey, LastSynced.Value.ToString("O"));

            if (result.Received > 0)
            {
                await theme.ApplySavedAsync();
                Received?.Invoke();
            }
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
            await SyncNowAsync();
        else
        {
            // Signed out in another tab. The data stays, like choosing to keep it
            await current;
            await ForgetAsync();
        }
    }

    private async Task ForgetAsync()
    {
        await store.ForgetStateAsync();
        await files.SetAsync(LastSyncKey, null);
        LastSynced = null;
        Problem = null;
        Refused = 0;
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
