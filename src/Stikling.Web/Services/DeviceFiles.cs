using Microsoft.JSInterop;
using Stikling.Core.People;

namespace Stikling.Web.Services;

/// <summary>
/// Saving a file to the device, and the small values that belong to this device only: when the
/// last backup was taken, and whether someone is signed in here (<see cref="AccountService"/>).
/// Anything that belongs to the person is a record instead.
/// </summary>
public sealed class DeviceFiles(IJSRuntime js) : JsModule(js, ModulePath)
{
    internal const string ModulePath = "./js/files.js";
    internal const string LastBackupKey = "last-backup";

    /// <summary>Offers the bytes to the browser as a download.</summary>
    public async Task DownloadAsync(string fileName, byte[] bytes) =>
        await (await Module).InvokeVoidAsync("download", fileName, bytes);

    /// <summary>
    /// POSTs the body to the address and offers what comes back as a download. The file goes
    /// straight from the response to the download in JavaScript, so it can be large.
    /// </summary>
    /// <returns>The status the server answered with, or -1 when it couldn't be reached.</returns>
    public async Task<int> DownloadFromAsync(Uri address, string token, object body, string fileName) =>
        await (await Module).InvokeAsync<int>("downloadFrom", address.ToString(), token, body, fileName);

    public async Task<string?> GetAsync(string key) =>
        await (await Module).InvokeAsync<string?>("get", key);

    public async Task SetAsync(string key, string? value) =>
        await (await Module).InvokeVoidAsync("set", key, value);

    /// <summary>The shared collection this device has open, or null when it is the main database.</summary>
    public async Task<Guid?> GetOpenCollectionAsync() =>
        OpenCollection.Parse(await GetAsync(OpenCollection.Key));

    /// <summary>
    /// The key for something kept per database, like when it last synced or was backed up. The main database
    /// keeps the plain key, and a shared collection's database gets its id added.
    /// </summary>
    public async Task<string> KeyForOpenAsync(string key) =>
        OpenCollection.KeyFor(key, await GetOpenCollectionAsync());

    public async Task<DateOnly?> GetLastBackupAsync() =>
        DateOnly.TryParse(await GetAsync(await KeyForOpenAsync(LastBackupKey)), out var date) ? date : null;

    public async Task SetLastBackupAsync(DateOnly date) =>
        await SetAsync(await KeyForOpenAsync(LastBackupKey), date.ToString("yyyy-MM-dd"));
}
