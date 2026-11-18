using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Files;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>What <see cref="AttachmentService.AddFromInputAsync"/> did with the files someone chose.</summary>
/// <param name="Added">The files that were kept and have a record now.</param>
/// <param name="Unsupported">Files that are neither a PDF nor an image the browser can read.</param>
/// <param name="TooLarge">Files over <see cref="FileRules.MaxFileBytes"/>, or images that are after being shrunk.</param>
/// <param name="NoRoom">Files left out because the plant, propagation or order already has <see cref="FileRules.MaxFilesPerRecord"/>.</param>
public sealed record FileAddResult(IReadOnlyList<AttachedFile> Added, int Unsupported, int TooLarge, int NoRoom)
{
    /// <summary>How many files were left out, for whatever reason.</summary>
    public int Failed => Unsupported + TooLarge + NoRoom;
}

/// <summary>
/// Keeps the bytes of attached files in the browser and opens, saves and sends them. The bytes are
/// handled by wwwroot/js/attachments.js and never reach C# except for a backup. The records go through
/// <see cref="FileService"/>.
/// </summary>
public sealed class AttachmentService(IJSRuntime js, FileService files) : JsModule(js, ModulePath)
{
    internal const string ModulePath = "./js/attachments.js";

    /// <summary>What attachments.js returns for one kept file.</summary>
    private sealed record Prepared(Guid Id, string Name, string ContentType, long Size);

    private sealed record InputResult(List<Prepared> Saved, List<string> Errors, int Skipped);

    /// <summary>
    /// Keeps the files chosen in <paramref name="input"/> (PDFs as they are, images shrunk) and attaches them
    /// to the plant, propagation or order. Only as many as there is room for are taken, and the rest are
    /// counted in <see cref="FileAddResult.NoRoom"/>.
    /// </summary>
    public async Task<FileAddResult> AddFromInputAsync(ElementReference input, FileSubject subjectType, Guid subjectId)
    {
        var room = await files.RoomForAsync(subjectType, subjectId);
        var result = await (await Module).InvokeAsync<InputResult>("saveFromInput", input, room);

        var added = new List<AttachedFile>();
        foreach (var kept in result.Saved)
        {
            try
            {
                added.Add(await files.AddAsync(subjectType, subjectId, kept.Id, kept.Name, kept.ContentType, kept.Size));
            }
            catch
            {
                // The record couldn't be written, so the bytes are let go instead of left without one
                await DiscardAsync(kept.Id);
                throw;
            }
        }

        return new FileAddResult(
            added,
            result.Errors.Count(e => e == "unsupported"),
            result.Errors.Count(e => e == "tooLarge"),
            result.Skipped);
    }

    /// <summary>True when the file's bytes are on this device.</summary>
    public async Task<bool> HasBytesAsync(Guid id) =>
        await (await Module).InvokeAsync<bool>("has", id);

    /// <summary>Opens the file in a new tab. False when its bytes aren't on this device.</summary>
    public async Task<bool> OpenAsync(Guid id) =>
        await (await Module).InvokeAsync<bool>("open", id);

    /// <summary>Saves the file to the device under <paramref name="name"/>. False when its bytes aren't on this device.</summary>
    public async Task<bool> SaveAsync(Guid id, string name) =>
        await (await Module).InvokeAsync<bool>("save", id, name);

    /// <summary>Frees bytes that were kept but never got a record.</summary>
    public async Task DiscardAsync(Guid id) =>
        await (await Module).InvokeVoidAsync("discard", id);

    /// <summary>The stored bytes, for writing into a backup. Null when they are missing.</summary>
    public async Task<byte[]?> GetBytesAsync(Guid id) =>
        await (await Module).InvokeAsync<byte[]?>("getBytes", id);

    /// <summary>Puts bytes back from a backup. They are listed to be sent to the server, like a new file.</summary>
    public async Task PutBytesAsync(Guid id, byte[] bytes) =>
        await (await Module).InvokeVoidAsync("putBytes", id, bytes);

    /// <summary>Frees the bytes only, for a file whose record is already marked deleted.</summary>
    public async Task RemoveBytesAsync(Guid id) =>
        await (await Module).InvokeVoidAsync("remove", id);

    /// <summary>
    /// Sends the bytes kept here to <paramref name="address"/>. Returns the HTTP status, 0 when they
    /// aren't on this device, or -1 when the address couldn't be reached.
    /// </summary>
    public async Task<int> UploadAsync(Uri address, string token, Guid id) =>
        await (await Module).InvokeAsync<int>("upload", address.ToString(), token, id);

    /// <summary>
    /// Fetches the bytes from <paramref name="address"/> and keeps them here. Returns the HTTP status,
    /// or -1 when the address couldn't be reached.
    /// </summary>
    public async Task<int> DownloadAsync(Uri address, string token, Guid id) =>
        await (await Module).InvokeAsync<int>("download", address.ToString(), token, id);
}

/// <summary>What <see cref="AttachmentActions"/> did.</summary>
public enum AttachmentOutcome
{
    Done,

    /// <summary>The bytes aren't on this device and couldn't be fetched, e.g. offline or not signed in. Nothing is lost, and it works once they can be.</summary>
    NotHere,
}

/// <summary>
/// Opens or saves an attached file, fetching its bytes first when it was added on another device. It is
/// apart from <see cref="AttachmentService"/> because fetching goes through <see cref="SyncRunner"/>,
/// which needs that service to talk to the API.
/// </summary>
public sealed class AttachmentActions(AttachmentService attachments, SyncRunner sync)
{
    public async Task<AttachmentOutcome> OpenAsync(AttachedFile file)
    {
        if (!await HaveBytesAsync(file.Id))
            return AttachmentOutcome.NotHere;
        return await attachments.OpenAsync(file.Id) ? AttachmentOutcome.Done : AttachmentOutcome.NotHere;
    }

    public async Task<AttachmentOutcome> SaveAsync(AttachedFile file)
    {
        if (!await HaveBytesAsync(file.Id))
            return AttachmentOutcome.NotHere;
        return await attachments.SaveAsync(file.Id, file.Name) ? AttachmentOutcome.Done : AttachmentOutcome.NotHere;
    }

    private async Task<bool> HaveBytesAsync(Guid id) =>
        await attachments.HasBytesAsync(id) || (await sync.FetchFileAsync(id) && await attachments.HasBytesAsync(id));
}
