using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Labels;

namespace Stikling.Web.Services;

/// <summary>
/// Drawing, sharing and printing labels, which only the browser can do. The labels are drawn in
/// wwwroot/js/labels.js; only the words and the size cross into JavaScript.
/// </summary>
public sealed class LabelPrinter(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/labels.js").AsTask();

    /// <summary>Draws each label on its canvas in the container, in order.</summary>
    public async Task DrawAsync(ElementReference container, IReadOnlyList<LabelContent> labels, LabelParts parts, LabelShape shape) =>
        await (await Module).InvokeVoidAsync("render", container, labels, parts, shape);

    /// <summary>True when the browser can hand picture files to the phone's share menu.</summary>
    public async Task<bool> CanShareFilesAsync() =>
        await (await Module).InvokeAsync<bool>("canShareFiles");

    /// <summary>"shared", "cancelled" (closed the share menu), "saved" (it couldn't share, so they were saved) or "failed".</summary>
    public async Task<string> SharePicturesAsync(IReadOnlyList<LabelContent> labels, LabelParts parts, LabelShape shape) =>
        await (await Module).InvokeAsync<string>("sharePictures", labels, parts, shape, LabelContent.FileNames(labels));

    /// <summary>Opens the print dialog with the page the size the rule says.</summary>
    public async Task PrintAsync(string pageRule) =>
        await (await Module).InvokeVoidAsync("print", pageRule);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
