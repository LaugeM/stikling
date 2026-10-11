using Microsoft.JSInterop;
using Stikling.Core.Settings;

namespace Stikling.Web.Services;

/// <summary>How a wait for Google Photos ended. Saved and Failed are only set when Status is "done".</summary>
public sealed record GooglePhotosResult(string Status, List<PhotoService.SavedFile>? Saved = null, int Failed = 0)
{
    public PhotoService.SaveResponse ToSaveResponse() => new(Saved ?? [], Failed);
}

/// <summary>What starting a picking session gave. PickerUri is only set when Status is "ready".</summary>
public sealed record GooglePhotosStart(string Status, string? PickerUri = null);

/// <summary>
/// Picking photos from Google Photos. The only class that calls wwwroot/js/googlephotos.js, which
/// holds Google's token and the session. The token never comes here. Google's script is only loaded
/// by <see cref="PrepareAsync"/>, so nobody who doesn't use it loads anything from Google.
/// </summary>
public sealed class GooglePhotosService(IJSRuntime js, GoogleSettings settings, SettingsService userSettings) : IAsyncDisposable
{
    private const int MaxItems = 50;

    private Task<IJSObjectReference>? module;
    private IJSObjectReference? loaded;

    /// <summary>False when there is no Google client ID, and then the feature is hidden.</summary>
    public bool IsConfigured => settings.IsConfigured;

    /// <summary>True when the client ID is there and the person turned it on in Settings.</summary>
    public async Task<bool> IsEnabledAsync() => IsConfigured && (await userSettings.GetAsync()).GooglePhotos;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/googlephotos.js").AsTask();

    /// <summary>Loads Google's script. Call it before the first tap, so the tap can open Google's popup at once.</summary>
    public async Task<bool> PrepareAsync()
    {
        if (!IsConfigured)
            return false;
        try
        {
            loaded = await Module;
            return await loaded.InvokeAsync<bool>("prepare");
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <summary>
    /// Asks for access if needed and makes a picking session. Call it straight from the click, and
    /// after <see cref="PrepareAsync"/> has finished, so nothing slow comes before Google's popup.
    /// </summary>
    public async Task<GooglePhotosStart> StartAsync()
    {
        if (!IsConfigured)
            return new GooglePhotosStart("failed");

        // A tap before Google's script finished loading waits for it. The popup may then be blocked,
        // which ends as "failed" like any other popup that can't open.
        if (loaded is null && !await PrepareAsync())
            return new GooglePhotosStart("failed");
        try
        {
            return await loaded.InvokeAsync<GooglePhotosStart>("start", settings.ClientId, MaxItems);
        }
        catch (JSException)
        {
            return new GooglePhotosStart("failed");
        }
    }

    /// <summary>
    /// Waits for the person to pick, then fetches and stores the photos. <paramref name="progress"/>
    /// is told which photo (from 1) of how many is being fetched.
    /// </summary>
    public async Task<GooglePhotosResult> WaitAsync(Func<int, int, Task> progress)
    {
        if (loaded is null)
            return new GooglePhotosResult("failed");

        using var reference = DotNetObjectReference.Create(new Callback(progress));
        try
        {
            return await loaded.InvokeAsync<GooglePhotosResult>("wait", reference);
        }
        catch (JSException)
        {
            return new GooglePhotosResult("failed");
        }
    }

    /// <summary>Stops waiting and deletes the session. Safe when nothing is running.</summary>
    public async Task CancelAsync()
    {
        if (loaded is null)
            return;
        try
        {
            await loaded.InvokeVoidAsync("cancel");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            // The page is going away
        }
    }

    private sealed class Callback(Func<int, int, Task> progress)
    {
        [JSInvokable]
        public Task Progress(int number, int total) => progress(number, total);
    }

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
