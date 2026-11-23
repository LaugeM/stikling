using Microsoft.JSInterop;
using Stikling.Core.Settings;

namespace Stikling.Web.Services;

/// <summary>
/// Keeps the screen on while a page that is used with the phone in one hand is open, like Water plants
/// and Go round. The work is in wwwroot/js/wakelock.js. It does nothing where the browser can't do it,
/// and when the person turned it off in Settings.
/// </summary>
public sealed class ScreenAwake(IJSRuntime js, SettingsService settings)
{
    /// <summary>Holds the screen on until the returned handle is disposed. Never throws.</summary>
    public async Task<IAsyncDisposable> HoldAsync()
    {
        try
        {
            // Null and true both mean on
            if ((await settings.GetAsync()).KeepScreenOn == false)
                return new Handle(null);

            var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/wakelock.js");
            await module.InvokeVoidAsync("hold");
            return new Handle(module);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
        {
            return new Handle(null);
        }
    }

    /// <summary>A hold a page switches on and off as it needs, which lets go of it when the page closes.</summary>
    public AwakeKeeper Keeper() => new(this);

    private sealed class Handle(IJSObjectReference? module) : IAsyncDisposable
    {
        private IJSObjectReference? module = module;

        public async ValueTask DisposeAsync()
        {
            var held = module;
            module = null;
            if (held is null)
                return;

            try
            {
                await held.InvokeVoidAsync("release");
                await held.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException)
            {
                // The page is going away
            }
        }
    }
}

/// <summary>
/// Follows whether a page wants the screen kept on, which pages say after each render. Asking for what it
/// already has does nothing, and a page closing while the hold is being asked for lets go of it again.
/// </summary>
public sealed class AwakeKeeper(ScreenAwake awake) : IAsyncDisposable
{
    private IAsyncDisposable? hold;
    private bool wanted;
    private bool working;
    private bool disposed;

    public async Task SetAsync(bool want)
    {
        wanted = want && !disposed;
        if (working)
            return;

        working = true;
        try
        {
            while (wanted != (hold is not null))
            {
                if (wanted)
                {
                    hold = await awake.HoldAsync();
                }
                else
                {
                    var held = hold;
                    hold = null;
                    if (held is not null)
                        await held.DisposeAsync();
                }
            }
        }
        finally
        {
            working = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        wanted = false;
        if (hold is { } held && !working)
        {
            hold = null;
            await held.DisposeAsync();
        }
    }
}
