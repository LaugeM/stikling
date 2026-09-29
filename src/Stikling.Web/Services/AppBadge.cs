using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>The number on the installed app's icon. Does nothing where the browser has no badge support.</summary>
public sealed class AppBadge(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/badge.js").AsTask();

    /// <summary>Shows the count on the icon, or clears the badge when it is 0.</summary>
    public async Task SetAsync(int count)
    {
        try
        {
            await (await Module).InvokeVoidAsync("set", count);
        }
        catch (JSException)
        {
            // A badge is only a nicety, so a browser that refuses it is left alone
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await (await module).DisposeAsync();
    }
}
