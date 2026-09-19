using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Web.Services;

public sealed record PhotoSaveResult(IReadOnlyList<Photo> Saved, int Failed);

/// <summary>
/// Saves and shows photos. Image data is handled by wwwroot/js/photos.js;
/// the metadata goes through <see cref="IPhotoRepository"/>.
/// </summary>
public sealed class PhotoService(IJSRuntime js, IPhotoRepository photos, TimeProvider time) : IAsyncDisposable
{
    internal const string ModulePath = "./js/photos.js";

    private sealed record SavedFile(string Id, int Width, int Height, DateTimeOffset TakenAt);
    private sealed record SaveResponse(List<SavedFile> Saved, int Failed);

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>Stores the files chosen in <paramref name="input"/> as photos of the given subject.</summary>
    public async Task<PhotoSaveResult> SaveFromInputAsync(ElementReference input, SubjectType subjectType, Guid subjectId)
    {
        var response = await (await Module).InvokeAsync<SaveResponse>("saveFromInput", input);
        var now = time.GetUtcNow();
        var saved = new List<Photo>();

        foreach (var file in response.Saved)
        {
            var photo = new Photo
            {
                Id = Guid.Parse(file.Id),
                SubjectType = subjectType,
                SubjectId = subjectId,
                // A future file date means a wrong device clock; fall back to now
                TakenAt = file.TakenAt > now ? now : file.TakenAt,
                Width = file.Width,
                Height = file.Height
            };
            await photos.AddAsync(photo);
            saved.Add(photo);
        }

        return new PhotoSaveResult(saved, response.Failed);
    }

    /// <summary>An address usable in &lt;img src&gt;, or null if the photo is missing.</summary>
    public async Task<string?> GetUrlAsync(Guid id, bool thumbnail) =>
        await (await Module).InvokeAsync<string?>("getUrl", id, thumbnail);

    /// <summary>Removes the photo: its metadata is soft-deleted and the image data is freed.</summary>
    public async Task DeleteAsync(Guid id)
    {
        await photos.DeleteAsync(id);
        await (await Module).InvokeVoidAsync("remove", id);
    }

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
