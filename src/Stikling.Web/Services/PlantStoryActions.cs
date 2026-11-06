using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>Printing a plant's story, which only the browser can do. See wwwroot/js/story.js.</summary>
public sealed class PlantStoryActions(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/story.js").AsTask();

    /// <summary>
    /// Waits for the photos on the page, then opens the print dialog, where the page can also be saved
    /// as a PDF. Returns how many photos could not be shown.
    /// </summary>
    public async Task<int> PrintAsync() => await (await Module).InvokeAsync<int>("print");

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await (await module).DisposeAsync();
    }
}
