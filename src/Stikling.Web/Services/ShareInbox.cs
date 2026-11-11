using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;

namespace Stikling.Web.Services;

/// <summary>A photo waiting in the inbox, with an address to show it by.</summary>
public sealed record WaitingPhoto(string Id, string Url, string Name);

/// <param name="Result">What was saved, the same as for a photo from the gallery.</param>
/// <param name="SavedIds">The inbox ids of the photos that were saved, so only those are removed afterwards.</param>
public sealed record InboxSaveResult(PhotoSaveResult Result, IReadOnlyList<string> SavedIds);

/// <summary>
/// The photos waiting to be sent to a plant or propagation, from Android's share menu or the gallery
/// on the Add photos page. They stay in Cache Storage on this device (wwwroot/js/shareinbox.js), so they
/// don't sync and aren't in backups. The image data stays in JavaScript until it is saved as a photo.
/// </summary>
public sealed class ShareInbox(IJSRuntime js, PhotoService photos) : JsModule(js, ModulePath)
{
    private const string ModulePath = "./js/shareinbox.js";

    private sealed record InboxResponse(List<PhotoService.SavedFile> Saved, int Failed, List<string> SavedIds);

    /// <summary>How many photos are waiting.</summary>
    public async Task<int> CountAsync() =>
        await (await Module).InvokeAsync<int>("count");

    /// <summary>The waiting photos, oldest first.</summary>
    public async Task<IReadOnlyList<WaitingPhoto>> ListAsync() =>
        await (await Module).InvokeAsync<List<WaitingPhoto>>("list");

    /// <summary>
    /// Puts the files chosen in <paramref name="input"/> in the inbox. Returns how many were added.
    /// Throws when the browser couldn't store them all.
    /// </summary>
    public async Task<int> AddFromInputAsync(ElementReference input) =>
        await (await Module).InvokeAsync<int>("addFromInput", input);

    /// <summary>
    /// Saves the waiting photos with these ids as photos of the subject. Nothing leaves the inbox,
    /// so the caller removes the saved ones with <see cref="RemoveAsync"/> once it has kept them.
    /// </summary>
    public async Task<InboxSaveResult> SaveAsync(IReadOnlyCollection<string> ids, SubjectType subjectType, Guid subjectId)
    {
        var response = await (await Module).InvokeAsync<InboxResponse>("save", ids);
        var result = await photos.RecordAsync(new PhotoService.SaveResponse(response.Saved, response.Failed), subjectType, subjectId);
        return new InboxSaveResult(result, response.SavedIds);
    }

    /// <summary>Takes the photos with these ids out of the inbox.</summary>
    public async Task RemoveAsync(IReadOnlyCollection<string> ids) =>
        await (await Module).InvokeVoidAsync("remove", ids);

    /// <summary>Lets go of the addresses <see cref="ListAsync"/> handed out, for when the page is left.</summary>
    public async Task ReleaseAsync()
    {
        await IfLoadedAsync(m => m.InvokeVoidAsync("release"));
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await IfLoadedAsync(m => m.InvokeVoidAsync("release"));
        }
        catch (JSDisconnectedException)
        {
            // The page is going away
        }

        await base.DisposeAsync();
    }
}
