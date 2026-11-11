using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>
/// A service that talks to one JavaScript module. The module is imported the first time it's
/// needed and disposed with the service.
/// </summary>
public abstract class JsModule(IJSRuntime js, string path) : IAsyncDisposable
{
    private Task<IJSObjectReference>? module;

    protected Task<IJSObjectReference> Module => module ??= ImportAsync();

    // An import that failed, say offline right after an update, is tried again on the next call
    private async Task<IJSObjectReference> ImportAsync()
    {
        try
        {
            return await js.InvokeAsync<IJSObjectReference>("import", path);
        }
        catch
        {
            module = null;
            throw;
        }
    }

    /// <summary>Runs only when the module has already loaded, for letting go of what it handed out.</summary>
    protected async Task IfLoadedAsync(Func<IJSObjectReference, ValueTask> action)
    {
        if (module is { IsCompletedSuccessfully: true })
            await action(module.Result);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away
            }
        }
    }
}
