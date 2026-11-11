using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Stikling.Core.Identification;
using Stikling.Core.Names;
using Stikling.Core.Sync;
using static Stikling.Core.Text.Format;

namespace Stikling.Web.Services;

/// <summary>A suggestion that was tapped. <paramref name="PhotoInput"/> is the file input the photo came from when it should be kept on the plant.</summary>
public sealed record IdentifyPick(IdentificationSuggestion Suggestion, ElementReference? PhotoInput, bool FromCamera);

/// <summary>
/// Names the plant in a photo. The photo is made into a JPEG and sent in identify.js, so the image
/// never crosses into .NET and a Pl@ntNet key never shows up in an exception or a log. A key of the
/// person's own on this device (Settings) goes straight to Pl@ntNet. Without one it goes through
/// Stikling's API, which needs a sign-in. Without either there is nothing to ask.
/// </summary>
public sealed class IdentificationService(
    IJSRuntime js,
    DeviceFiles files,
    AccountService account,
    AccountSettings accountSettings,
    PhotoService photos,
    SyncRunner sync,
    PlantNameService names) : JsModule(js, ModulePath)
{
    /// <summary>The <see cref="DeviceFiles"/> key of the person's own Pl@ntNet key. It belongs to this device, so it isn't synced or backed up.</summary>
    internal const string KeyName = "plantnet-key";

    private const string ModulePath = "./js/identify.js";

    /// <summary>What identify.js hands back: the HTTP status (-1 offline, 0 photo missing) and the text of the answer.</summary>
    private sealed record Reply(int Status, string? Body);

    public async Task<string?> GetKeyAsync() =>
        await files.GetAsync(KeyName) is { } key && !string.IsNullOrWhiteSpace(key) ? key.Trim() : null;

    /// <summary>Saves the key, or removes it when the text is empty.</summary>
    public Task SetKeyAsync(string? key) =>
        files.SetAsync(KeyName, Clean(key));

    /// <summary>True when the button is worth showing: there is somewhere to sign in, or a key of the person's own.</summary>
    public async Task<bool> IsOfferedAsync() => account.IsAvailable || await GetKeyAsync() is not null;

    /// <summary>Identifies the photo chosen in <paramref name="input"/>. The input is left as it is.</summary>
    public async Task<IdentificationOutcome> IdentifyInputAsync(ElementReference input) =>
        await RunAsync(async (module, route) => await module.InvokeAsync<Reply>("identifyInput", input, route));

    /// <summary>Empties a file input, so the same file can be chosen again.</summary>
    public async Task ClearInputAsync(ElementReference input) =>
        await (await Module).InvokeVoidAsync("clearInput", input);

    /// <summary>Identifies a photo kept on this device, fetching its full size first when only the thumbnail is here.</summary>
    public async Task<IdentificationOutcome> IdentifyPhotoAsync(Guid photoId) =>
        await RunAsync(async (module, route) =>
        {
            if (!await photos.HasBytesAsync(photoId))
                await sync.FetchPhotoAsync(photoId, PhotoSize.Full);
            return await module.InvokeAsync<Reply>("identifyStored", photoId, route);
        });

    private async Task<IdentificationOutcome> RunAsync(Func<IJSObjectReference, object, Task<Reply>> send)
    {
        var language = (await names.GetEverydayNamesAsync()).Language == "da" ? "da" : "en";

        object route;
        var key = await GetKeyAsync();
        if (key is not null)
            route = new { key, lang = language };
        else if (account.IsAvailable && accountSettings.ApiAddress is { } api && await account.WasSignedInAsync())
        {
            string? token;
            try
            {
                token = await account.GetTokenAsync();
            }
            catch (Exception e) when (e is AccountUnavailableException or JSException)
            {
                // Clerk's scripts couldn't be loaded, which is mostly being offline
                return IdentificationOutcome.Failure(IdentificationProblem.Offline);
            }

            if (token is null)
                return IdentificationOutcome.Failure(IdentificationProblem.NeedsAccount);
            route = new { address = new Uri(api, "identify").ToString(), token, lang = language };
        }
        else
            return IdentificationOutcome.Failure(IdentificationProblem.NeedsAccount);

        try
        {
            var reply = await send(await Module, route);
            return IdentificationOutcome.From(reply.Status, reply.Body, ownKey: key is not null);
        }
        catch (JSException)
        {
            // identify.js catches its own errors, so this is the module failing to load. Nothing of the
            // route is kept in the message that is logged.
            return IdentificationOutcome.Failure(IdentificationProblem.Failed);
        }
    }
}
