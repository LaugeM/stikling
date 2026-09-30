using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Web.Services;

/// <param name="Undated">Photos with no date of their own, whose day is only a guess.</param>
public sealed record PhotoSaveResult(IReadOnlyList<Photo> Saved, int Failed, int Undated)
{
    /// <summary>
    /// What to tell the user afterwards, or null when all went well. The guessed days are left out
    /// when they don't matter: a fresh camera photo is taken now, and a day picked by hand dates them all.
    /// </summary>
    public string? Message(bool guessesMatter)
    {
        var lines = new List<string>();

        if (Failed > 0)
            lines.Add(Failed == 1
                ? "1 file couldn't be read as an image and was skipped."
                : $"{Failed} files couldn't be read as images and were skipped.");

        if (Undated > 0 && guessesMatter)
            lines.Add(Undated == 1
                ? "1 photo had no date saved in it, so its day is a guess. Check that it landed on the right day."
                : $"{Undated} photos had no date saved in them, so their days are a guess. Check that they landed on the right days.");

        return lines.Count == 0 ? null : string.Join(" ", lines);
    }
}

/// <summary>
/// Saves and shows photos. Image data is handled by wwwroot/js/photos.js;
/// the metadata goes through <see cref="IPhotoRepository"/>.
/// </summary>
public sealed class PhotoService(IJSRuntime js, IPhotoRepository photos, TimeProvider time) : IAsyncDisposable
{
    internal const string ModulePath = "./js/photos.js";

    private sealed record SavedFile(string Id, int Width, int Height, string? CameraDate, string? FileName, DateTimeOffset FileDate);
    private sealed record SaveResponse(List<SavedFile> Saved, int Failed);

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>Stores the files chosen in <paramref name="input"/> as photos of the given subject.</summary>
    public async Task<PhotoSaveResult> SaveFromInputAsync(ElementReference input, SubjectType subjectType, Guid subjectId)
    {
        var response = await (await Module).InvokeAsync<SaveResponse>("saveFromInput", input);
        var saved = new List<Photo>();
        var undated = 0;

        foreach (var file in response.Saved)
        {
            var (takenAt, source) = PhotoDates.Pick(file.CameraDate, file.FileName, file.FileDate, time);
            if (source != PhotoDateSource.Camera)
                undated++;

            var photo = new Photo
            {
                Id = Guid.Parse(file.Id),
                SubjectType = subjectType,
                SubjectId = subjectId,
                TakenAt = takenAt,
                Width = file.Width,
                Height = file.Height
            };
            await photos.AddAsync(photo);
            saved.Add(photo);
        }

        return new PhotoSaveResult(saved, response.Failed, undated);
    }

    /// <summary>An address usable in &lt;img src&gt;, or null if the photo is missing.</summary>
    public async Task<string?> GetUrlAsync(Guid id, bool thumbnail) =>
        await (await Module).InvokeAsync<string?>("getUrl", id, thumbnail);

    /// <summary>A photo's frame changed, so everywhere it's shown cropped can move it.</summary>
    public event Action<Photo>? FrameChanged;

    /// <summary>The photo's details, like its size and frame, or null when it's deleted or missing.</summary>
    public Task<Photo?> GetDetailsAsync(Guid id) => photos.GetAsync(id);

    /// <summary>Saves how the photo sits where it's cropped. The image itself isn't touched.</summary>
    public async Task SetFrameAsync(Photo photo, PhotoFrame? frame)
    {
        photo.Frame = frame;
        await photos.UpdateAsync(photo);
        FrameChanged?.Invoke(photo);
    }

    /// <summary>The stored image data, for writing into a backup. Null when it's missing.</summary>
    public async Task<byte[]?> GetBytesAsync(Guid id, bool thumbnail) =>
        await (await Module).InvokeAsync<byte[]?>("getBytes", id, thumbnail);

    /// <summary>True when the photo's image data is on this device.</summary>
    public async Task<bool> HasBytesAsync(Guid id) =>
        await (await Module).InvokeAsync<bool>("hasBytes", id);

    /// <summary>Puts image data back from a backup.</summary>
    public async Task PutBytesAsync(Guid id, byte[] bytes, byte[]? thumbnail) =>
        await (await Module).InvokeVoidAsync("putBytes", id, bytes, thumbnail);

    /// <summary>
    /// Sends the image kept here to <paramref name="address"/>. Returns the HTTP status, 0 when the
    /// image isn't on this device, or -1 when the address couldn't be reached.
    /// </summary>
    public async Task<int> UploadAsync(Uri address, string token, Guid id, bool thumbnail) =>
        await (await Module).InvokeAsync<int>("upload", address.ToString(), token, id, thumbnail);

    /// <summary>
    /// Fetches the image from <paramref name="address"/> and keeps it here. Returns the HTTP status,
    /// or -1 when the address couldn't be reached.
    /// </summary>
    public async Task<int> DownloadAsync(Uri address, string token, Guid id, bool thumbnail) =>
        await (await Module).InvokeAsync<int>("download", address.ToString(), token, id, thumbnail);

    /// <summary>Removes the photo: its metadata is soft-deleted and the image data is freed.</summary>
    public async Task DeleteAsync(Guid id)
    {
        await photos.DeleteAsync(id);
        await RemoveBytesAsync(id);
    }

    /// <summary>Frees the image data only, for a photo whose metadata is already marked deleted.</summary>
    public async Task RemoveBytesAsync(Guid id) =>
        await (await Module).InvokeVoidAsync("remove", id);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
