using Microsoft.JSInterop;
using Stikling.Core.Today;

namespace Stikling.Web.Services;

/// <summary>
/// Saving a file to the device, and the small settings that belong to this device only
/// (when the last backup was taken, what has been put off on Today, whether the photo reminder is on).
/// </summary>
public sealed class DeviceFiles(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./js/files.js";
    internal const string LastBackupKey = "last-backup";
    internal const string PutOffsKey = "put-offs";
    internal const string PhotoReminderKey = "photo-reminder";

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

    public async Task<PutOffs> GetPutOffsAsync(DateOnly today) =>
        PutOffs.Parse(await GetAsync(PutOffsKey), today);

    public Task SetPutOffsAsync(PutOffs putOffs) => SetAsync(PutOffsKey, putOffs.ToJson());

    /// <summary>The monthly photo reminder on Today. Off until turned on in Settings.</summary>
    public async Task<bool> GetPhotoReminderAsync() => await GetAsync(PhotoReminderKey) == "on";

    public Task SetPhotoReminderAsync(bool on) => SetAsync(PhotoReminderKey, on ? "on" : null);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
