using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>Printing the plant-sitter sheet, sharing text and opening a link in a new tab, which only the browser can do. See wwwroot/js/sitter.js.</summary>
public sealed class SitterSheetActions(IJSRuntime js) : JsModule(js, "./js/sitter.js")
{
    /// <summary>Opens the print dialog, where the sheet can also be saved as a PDF.</summary>
    public async Task PrintAsync() => await (await Module).InvokeVoidAsync("print");

    /// <summary>"shared", "cancelled", "copied" (the browser has no share menu) or "failed".</summary>
    public async Task<string> ShareTextAsync(string text) =>
        await (await Module).InvokeAsync<string>("shareText", text);

    /// <summary>Opens an https link in a new tab. Call it straight from a tap, or the browser may block it.</summary>
    public async Task OpenLinkAsync(string url) =>
        await (await Module).InvokeVoidAsync("openLink", url);
}
