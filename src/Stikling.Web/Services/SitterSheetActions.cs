using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>Printing the plant-sitter sheet and sharing it as text, which only the browser can do. See wwwroot/js/sitter.js.</summary>
public sealed class SitterSheetActions(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/sitter.js").AsTask();

    /// <summary>Opens the print dialog, where the sheet can also be saved as a PDF.</summary>
    public async Task PrintAsync() => await (await Module).InvokeVoidAsync("print");

    /// <summary>"shared", "cancelled", "copied" (the browser has no share menu) or "failed".</summary>
    public async Task<string> ShareTextAsync(string text) =>
        await (await Module).InvokeAsync<string>("shareText", text);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await (await module).DisposeAsync();
    }
}
