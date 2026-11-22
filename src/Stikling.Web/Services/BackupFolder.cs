using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>
/// The folder on this device that backups are saved to, for browsers that can write into one (Chrome and Edge).
/// The folder belongs to this device only: its handle is kept in a database of its own, not in the app's
/// data, so it isn't synced and isn't part of a backup. Other browsers don't offer it and keep the download.
/// </summary>
public sealed class BackupFolder(IJSRuntime js) : JsModule(js, ModulePath)
{
    internal const string ModulePath = "./js/backupfolder.js";

    /// <summary>True when the browser can write into a folder the person picks.</summary>
    public async Task<bool> IsSupportedAsync() =>
        await (await Module).InvokeAsync<bool>("isSupported");

    /// <summary>The name of the chosen folder, or null when none is set.</summary>
    public async Task<string?> GetNameAsync() =>
        await (await Module).InvokeAsync<string?>("getName");

    /// <summary>Lets the person pick a folder. Returns its name, or null when they closed the picker.</summary>
    public async Task<string?> ChooseAsync() =>
        await (await Module).InvokeAsync<string?>("choose");

    /// <summary>Forgets the folder, so backups are downloaded again. Nothing in the folder is touched.</summary>
    public async Task ForgetAsync() =>
        await (await Module).InvokeVoidAsync("forget");

    /// <summary>
    /// Asks the browser for permission to write to the folder, which it takes back when the app is closed. This has to be
    /// the first thing a tap does, since the tap stops counting after a few seconds. False when no folder is set or the
    /// answer was no.
    /// </summary>
    public async Task<bool> RequestPermissionAsync() =>
        await (await Module).InvokeAsync<bool>("requestPermission");

    /// <summary>
    /// Writes the file into the folder. A name that is already there gets (2), (3) and so on, so an old backup is never
    /// replaced. Returns the name it was saved as, or null when it couldn't be written.
    /// </summary>
    public async Task<string?> WriteAsync(string fileName, byte[] bytes) =>
        await (await Module).InvokeAsync<string?>("write", fileName, bytes);
}
