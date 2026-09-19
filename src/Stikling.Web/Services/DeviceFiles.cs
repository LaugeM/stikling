using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>
/// Saving a file to the device, and the small settings that belong to this device only
/// (when the last backup was taken, whether the tour has been seen).
/// </summary>
public sealed class DeviceFiles(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./js/files.js";
    internal const string LastBackupKey = "last-backup";

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    /// <summary>Offers the bytes to the browser as a download.</summary>
    public async Task DownloadAsync(string fileName, byte[] bytes) =>
        await (await Module).InvokeVoidAsync("download", fileName, bytes);

    public async Task<string?> GetAsync(string key) =>
        await (await Module).InvokeAsync<string?>("get", key);

    public async Task SetAsync(string key, string? value) =>
        await (await Module).InvokeVoidAsync("set", key, value);

    public async Task<DateOnly?> GetLastBackupAsync() =>
        DateOnly.TryParse(await GetAsync(LastBackupKey), out var date) ? date : null;

    public Task SetLastBackupAsync(DateOnly date) =>
        SetAsync(LastBackupKey, date.ToString("yyyy-MM-dd"));

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
